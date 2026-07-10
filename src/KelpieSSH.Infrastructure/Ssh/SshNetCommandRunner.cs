using System.Net.Sockets;
using System.Text;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace KelpieSSH.Infrastructure.Ssh;

/// <summary>
/// Executes SSH commands using SSH.NET.
/// </summary>
public sealed class SshNetCommandRunner : ISshCommandRunner
{
    private readonly SshNetAuthenticationFactory _authenticationFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshNetCommandRunner"/> class.
    /// </summary>
    public SshNetCommandRunner()
        : this(new NullSshPasswordProvider())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshNetCommandRunner"/> class.
    /// </summary>
    /// <param name="passwordProvider">The SSH password provider.</param>
    public SshNetCommandRunner(ISshPasswordProvider passwordProvider)
    {
        _authenticationFactory = new SshNetAuthenticationFactory(passwordProvider);
    }

    /// <inheritdoc />
    public async Task<SshCommandResult> ExecuteAsync(
        SshCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Profile.Validate();

        using var timeoutSource = new CancellationTokenSource(request.Timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        var startedAt = DateTimeOffset.UtcNow;
        WarnIfHostKeyIsNotPinned(request.Profile);
        KpLog.Info(
            $"SSH command started. profile={request.Profile.Name}, command={request.CommandName}, timeoutSeconds={request.Timeout.TotalSeconds:0.###}");

        try
        {
            var authenticationMethod = await _authenticationFactory.CreateAsync(request.Profile, linkedSource.Token);
            var result = await Task.Run(
                () => Execute(request, startedAt, authenticationMethod, linkedSource.Token),
                linkedSource.Token);
            KpLog.Info(
                $"SSH command completed. profile={request.Profile.Name}, command={request.CommandName}, exitCode={result.ExitCode}, timedOut={result.TimedOut}, durationMs={(result.CompletedAt - result.StartedAt).TotalMilliseconds:0.###}");
            return result;
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            var completedAt = DateTimeOffset.UtcNow;
            KpLog.Warn(
                $"SSH command timed out. profile={request.Profile.Name}, command={request.CommandName}, timeoutSeconds={request.Timeout.TotalSeconds:0.###}, durationMs={(completedAt - startedAt).TotalMilliseconds:0.###}");

            return new SshCommandResult(
                request.CommandName,
                request.CommandText,
                -1,
                string.Empty,
                "SSH command timed out.",
                startedAt,
                completedAt,
                TimedOut: true);
        }
        catch (Exception ex) when (ex is SocketException or SshOperationTimeoutException or SshException)
        {
            var completedAt = DateTimeOffset.UtcNow;
            var message = ex switch
            {
                SocketException => $"SSH host is unreachable: {ex.Message}",
                SshOperationTimeoutException => $"SSH connection timed out: {ex.Message}",
                _ => $"SSH connection failed: {ex.Message}",
            };
            KpLog.Warn(
                $"SSH command connection failed. profile={request.Profile.Name}, command={request.CommandName}, exceptionType={ex.GetType().FullName ?? "UnknownException"}, durationMs={(completedAt - startedAt).TotalMilliseconds:0.###}");
            throw new SshConnectionException(message, ex);
        }
        catch (Exception ex)
        {
            KpLog.Err(
                $"SSH command failed. profile={request.Profile.Name}, command={request.CommandName}, exceptionType={ex.GetType().FullName ?? "UnknownException"}");
            throw;
        }
    }

    private static void WarnIfHostKeyIsNotPinned(SshConnectionProfile profile)
    {
        if (!SshHostKeyVerifier.HasPinnedFingerprint(profile.HostKeyFingerprintSha256))
        {
            KpLog.Warn($"SSH host key is not pinned. profile={profile.Name}, host={profile.Host}. Verify the first connection out of band and set Host.HostKeyFingerprintSha256.");
        }
    }

    private static SshCommandResult Execute(
        SshCommandRequest request,
        DateTimeOffset startedAt,
        AuthenticationMethod authenticationMethod,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var connectionInfo = new ConnectionInfo(
            request.Profile.Host,
            request.Profile.Port,
            request.Profile.UserName,
            authenticationMethod)
        {
            Timeout = request.Profile.ConnectionTimeout,
        };

        using var client = new SshClient(connectionInfo);
        client.HostKeyReceived += (_, args) =>
        {
            args.CanTrust = SshHostKeyVerifier.IsTrusted(
                request.Profile.HostKeyFingerprintSha256,
                args.FingerPrintSHA256);
        };

        client.Connect();
        cancellationToken.ThrowIfCancellationRequested();

        var commandText = BuildExecutionCommandText(request);
        var standardInput = BuildExecutionStandardInput(request);
        using var command = client.CreateCommand(commandText);
        command.CommandTimeout = request.Timeout;
        try
        {
            if (standardInput is null)
            {
                _ = command.Execute();
            }
            else
            {
                var executeTask = command.ExecuteAsync(cancellationToken);
                using (var inputStream = command.CreateInputStream())
                {
                    var inputBytes = Encoding.UTF8.GetBytes(standardInput);
                    inputStream.Write(inputBytes, 0, inputBytes.Length);
                }

                executeTask.GetAwaiter().GetResult();
            }
        }
        catch (SshOperationTimeoutException)
        {
            return new SshCommandResult(
                request.CommandName,
                request.CommandText,
                -1,
                string.Empty,
                "SSH command timed out.",
                startedAt,
                DateTimeOffset.UtcNow,
                TimedOut: true);
        }

        return new SshCommandResult(
            request.CommandName,
            request.CommandText,
            command.ExitStatus ?? -1,
            command.Result,
            command.Error,
            startedAt,
            DateTimeOffset.UtcNow,
            TimedOut: false);
    }

    private static string BuildExecutionCommandText(SshCommandRequest request)
    {
        if (request.EnvironmentOverrides is null || request.EnvironmentOverrides.Count == 0)
        {
            return request.CommandText;
        }

        var builder = new StringBuilder();
        var index = 0;
        foreach (var item in request.EnvironmentOverrides.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            ValidateEnvironmentKey(item.Key);
            var variableName = "__kelpie_env_" + index++;
            builder.Append("IFS= read -r ");
            builder.Append(variableName);
            builder.Append("; export ");
            builder.Append(item.Key);
            builder.Append("=\"$");
            builder.Append(variableName);
            builder.Append("\"; unset ");
            builder.Append(variableName);
            builder.Append("; ");
        }

        builder.Append(request.CommandText);
        return builder.ToString();
    }

    private static string? BuildExecutionStandardInput(SshCommandRequest request)
    {
        if (request.EnvironmentOverrides is null || request.EnvironmentOverrides.Count == 0)
        {
            return request.StandardInput;
        }

        var builder = new StringBuilder();
        foreach (var item in request.EnvironmentOverrides.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (item.Value.Contains('\r', StringComparison.Ordinal) || item.Value.Contains('\n', StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Environment variable value must not contain newline characters: {item.Key}");
            }

            builder.Append(item.Value);
            builder.Append('\n');
        }

        if (request.StandardInput is not null)
        {
            builder.Append(request.StandardInput);
        }

        return builder.ToString();
    }

    private static void ValidateEnvironmentKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || !key.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_')
            || char.IsDigit(key[0]))
        {
            throw new InvalidOperationException($"Environment variable key is invalid: {key}");
        }
    }

}
