using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using Renci.SshNet;

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
        catch (Exception ex)
        {
            KpLog.Err(
                $"SSH command failed. profile={request.Profile.Name}, command={request.CommandName}, exceptionType={ex.GetType().FullName ?? "UnknownException"}");
            throw;
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
        client.Connect();
        cancellationToken.ThrowIfCancellationRequested();

        using var command = client.CreateCommand(request.CommandText);
        command.CommandTimeout = request.Timeout;
        var standardOutput = command.Execute();

        return new SshCommandResult(
            request.CommandName,
            request.CommandText,
            command.ExitStatus ?? -1,
            standardOutput,
            command.Error,
            startedAt,
            DateTimeOffset.UtcNow,
            TimedOut: false);
    }

}
