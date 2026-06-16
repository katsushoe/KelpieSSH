using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KelpieMCPServer;

/// <summary>
/// Listens for local NamedPipe control messages and stops the MCP server gracefully.
/// </summary>
public sealed class NamedPipeShutdownService : BackgroundService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ISshConnectionProfileCatalog _profileCatalog;
    private readonly ISshPasswordSessionStore _passwordSessionStore;
    private readonly SshCommandService _sshCommandService;
    private readonly ILogger<NamedPipeShutdownService> _logger;
    private readonly KelpieServerControlOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeShutdownService"/> class.
    /// </summary>
    /// <param name="applicationLifetime">The host lifetime used to request graceful shutdown.</param>
    /// <param name="logger">The logger for control channel diagnostics.</param>
    /// <param name="options">The local control channel options.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="passwordSessionStore">The SSH password session store.</param>
    /// <param name="sshCommandService">The SSH command service.</param>
    public NamedPipeShutdownService(
        IHostApplicationLifetime applicationLifetime,
        ILogger<NamedPipeShutdownService> logger,
        KelpieServerControlOptions options,
        ISshConnectionProfileCatalog profileCatalog,
        ISshPasswordSessionStore passwordSessionStore,
        SshCommandService sshCommandService)
    {
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        _options = options;
        _profileCatalog = profileCatalog;
        _passwordSessionStore = passwordSessionStore;
        _sshCommandService = sshCommandService;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreateControlPipe(_options.PipeName);

                await pipe.WaitForConnectionAsync(stoppingToken);

                using var reader = new StreamReader(pipe);
                var writer = new StreamWriter(pipe)
                {
                    AutoFlush = true,
                };
                var message = await reader.ReadLineAsync(stoppingToken);

                if (string.Equals(message, "stop", StringComparison.OrdinalIgnoreCase))
                {
                    KpLog.Info("Stop command received.");
                    _logger.LogInformation("Stop command received.");
                    await writer.WriteLineAsync("stopping");
                    await writer.FlushAsync(stoppingToken);
                    _applicationLifetime.StopApplication();
                    return;
                }

                if (string.Equals(message, "ping", StringComparison.OrdinalIgnoreCase))
                {
                    KpLog.Debug("Ping command received.");
                    await writer.WriteLineAsync("pong");
                    await writer.FlushAsync(stoppingToken);
                    continue;
                }

                if (string.Equals(message, "sessions", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleSessionsAsync(writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "open-session", out var openSessionProfileName))
                {
                    await HandleOpenSessionAsync(openSessionProfileName, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "close-session", out var closeSessionHandle))
                {
                    await HandleKillAsync(closeSessionHandle, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "send-command", out var sendCommandHandle))
                {
                    await HandleSendCommandAsync(sendCommandHandle, reader, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "login", out var loginProfileName))
                {
                    await HandleLoginAsync(loginProfileName, reader, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "logout", out var logoutProfileName))
                {
                    await HandleLogoutAsync(logoutProfileName, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "kill", out var sessionHandle))
                {
                    await HandleKillAsync(sessionHandle, writer, stoppingToken);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    KpLog.Debug($"Control command ignored: {message}");
                    _logger.LogDebug("Control command ignored: {Command}", message);
                    await writer.WriteLineAsync("unknown");
                    await writer.FlushAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                var message = "KelpieMCPServer control pipe could not be opened. Another instance may already be running, or the pipe is owned by a process with different permissions.";
                KpLog.Warn(message);
                _logger.LogWarning(ex, "{Message}", message);
                _applicationLifetime.StopApplication();
                return;
            }
            catch (IOException ex) when (IsClientDisconnected(ex))
            {
                KpLog.Debug("NamedPipe control client disconnected before response was completed.");
                _logger.LogDebug(ex, "NamedPipe control client disconnected before response was completed.");
            }
            catch (Exception ex)
            {
                KpLog.Err("NamedPipe control listener failed.", ex);
                _logger.LogError(ex, "NamedPipe control listener failed.");
            }
        }
    }

    private async Task HandleLoginAsync(
        string profileName,
        TextReader reader,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        if (!_profileCatalog.TryGet(profileName, out var profile))
        {
            await writer.WriteLineAsync("profile-not-found");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (!string.Equals(profile.AuthenticationMethod, "password", StringComparison.OrdinalIgnoreCase))
        {
            await writer.WriteLineAsync("password-authentication-not-configured");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.PasswordSecretName))
        {
            await writer.WriteLineAsync("password-secret-not-configured");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        await writer.WriteLineAsync("password-required");
        await writer.FlushAsync(cancellationToken);

        var password = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(password))
        {
            await writer.WriteLineAsync("password-empty");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        _passwordSessionStore.SetPasswordSession(profile.Name, profile.PasswordSecretName, password);
        KpLog.Info($"SSH password session stored. profile={profile.Name}");
        await writer.WriteLineAsync("logged-in");
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleLogoutAsync(
        string profileName,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        if (!_profileCatalog.TryGet(profileName, out var profile))
        {
            await writer.WriteLineAsync("profile-not-found");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.PasswordSecretName))
        {
            await writer.WriteLineAsync("password-secret-not-configured");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        _passwordSessionStore.ClearPassword(profile.PasswordSecretName);
        KpLog.Info($"SSH password session cleared. profile={profile.Name}");
        await writer.WriteLineAsync("logged-out");
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleOpenSessionAsync(
        string profileName,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        if (!_profileCatalog.TryGet(profileName, out var profile))
        {
            await writer.WriteLineAsync("profile-not-found");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        var session = _passwordSessionStore.OpenInteractiveSession(profile.Name);
        KpLog.Info($"Interactive SSH session opened. profile={profile.Name}, handle={session.Handle}");
        var response = JsonSerializer.Serialize(session);
        await writer.WriteLineAsync(response);
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleSendCommandAsync(
        string sessionHandle,
        TextReader reader,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        SendCommandRequest? request;
        try
        {
            var requestJson = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                await WriteSendCommandResponseAsync(
                    writer,
                    SendCommandResponse.FromError(sessionHandle, string.Empty, string.Empty, "invalid-request"),
                    cancellationToken);
                return;
            }

            request = JsonSerializer.Deserialize<SendCommandRequest>(requestJson);
        }
        catch (JsonException)
        {
            await WriteSendCommandResponseAsync(
                writer,
                SendCommandResponse.FromError(sessionHandle, string.Empty, string.Empty, "invalid-request"),
                cancellationToken);
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.CommandName))
        {
            await WriteSendCommandResponseAsync(
                writer,
                SendCommandResponse.FromError(sessionHandle, string.Empty, string.Empty, "invalid-request"),
                cancellationToken);
            return;
        }

        var session = _passwordSessionStore.ListSessions()
            .FirstOrDefault(item => string.Equals(item.Handle, sessionHandle, StringComparison.OrdinalIgnoreCase));
        if (session is null)
        {
            await WriteSendCommandResponseAsync(
                writer,
                SendCommandResponse.FromError(sessionHandle, string.Empty, request.CommandName, "session-not-found"),
                cancellationToken);
            return;
        }

        if (!string.Equals(session.Kind, "interactive", StringComparison.OrdinalIgnoreCase))
        {
            await WriteSendCommandResponseAsync(
                writer,
                SendCommandResponse.FromError(session.Handle, session.ProfileName, request.CommandName, "session-is-not-interactive"),
                cancellationToken);
            return;
        }

        if (!_profileCatalog.TryGet(session.ProfileName, out var profile))
        {
            await WriteSendCommandResponseAsync(
                writer,
                SendCommandResponse.FromError(session.Handle, session.ProfileName, request.CommandName, "profile-not-found"),
                cancellationToken);
            return;
        }

        try
        {
            var timeout = request.TimeoutSeconds is > 0
                ? TimeSpan.FromSeconds(request.TimeoutSeconds.Value)
                : (TimeSpan?)null;
            var result = await _sshCommandService.ExecuteAsync(
                profile,
                request.CommandName,
                request.Arguments,
                timeout,
                KelpieExecutionChannel.Cli,
                cancellationToken);

            await WriteSendCommandResponseAsync(
                writer,
                SendCommandResponse.FromResult(session.Handle, session.ProfileName, result),
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsUnknownAllowedCommandError(ex))
        {
            var timeout = request.TimeoutSeconds is > 0
                ? TimeSpan.FromSeconds(request.TimeoutSeconds.Value)
                : (TimeSpan?)null;
            var result = await _sshCommandService.ExecuteRawAsync(
                profile,
                request.CommandName,
                timeout,
                KelpieExecutionChannel.Cli,
                cancellationToken);

            await WriteSendCommandResponseAsync(
                writer,
                SendCommandResponse.FromResult(session.Handle, session.ProfileName, result),
                cancellationToken);
        }
        catch (KelpiePolicyError ex)
        {
            await WriteSendCommandResponseAsync(
                writer,
                SendCommandResponse.FromPolicyError(session.Handle, session.ProfileName, request.CommandName, ex.Message),
                cancellationToken);
        }
        catch (Exception ex)
        {
            KpLog.Err($"send-command failed. handle={session.Handle}, profile={session.ProfileName}, command={request.CommandName}", ex);
            _logger.LogError(ex, "send-command failed. handle={Handle}, profile={ProfileName}, command={CommandName}", session.Handle, session.ProfileName, request.CommandName);
            await WriteSendCommandResponseAsync(
                writer,
                SendCommandResponse.FromError(session.Handle, session.ProfileName, request.CommandName, ex.Message),
                cancellationToken);
        }
    }

    private static bool IsUnknownAllowedCommandError(InvalidOperationException ex)
    {
        return ex.Message.StartsWith("SSH command is not allowed:", StringComparison.Ordinal);
    }

    private static async Task WriteSendCommandResponseAsync(
        TextWriter writer,
        SendCommandResponse response,
        CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(JsonSerializer.Serialize(response));
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleKillAsync(
        string sessionHandle,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        if (_passwordSessionStore.ClearSession(sessionHandle))
        {
            KpLog.Info($"SSH session killed. handle={sessionHandle}");
            await writer.WriteLineAsync("killed");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        await writer.WriteLineAsync("session-not-found");
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleSessionsAsync(TextWriter writer, CancellationToken cancellationToken)
    {
        var sessions = _passwordSessionStore.ListSessions();
        var response = JsonSerializer.Serialize(sessions);
        await writer.WriteLineAsync(response);
        await writer.FlushAsync(cancellationToken);
    }

    private static bool TryGetArgument(string? message, string command, out string argument)
    {
        argument = string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var prefix = command + " ";
        if (!message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        argument = message[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(argument);
    }

    private static bool IsClientDisconnected(IOException ex)
    {
        return ex.Message.Contains("Pipe is broken", StringComparison.OrdinalIgnoreCase);
    }

    private static NamedPipeServerStream CreateControlPipe(string pipeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        var security = new PipeSecurity();
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        security.AddAccessRule(new PipeAccessRule(users, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(admins, PipeAccessRights.FullControl, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            0,
            0,
            security);
    }

    private sealed record SendCommandRequest(
        string CommandName,
        IReadOnlyDictionary<string, string>? Arguments,
        int? TimeoutSeconds);

    private sealed record SendCommandResponse(
        string Handle,
        string ProfileName,
        string CommandName,
        string CommandText,
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        DateTimeOffset? StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        bool TimedOut,
        string PolicyError,
        string Error)
    {
        public static SendCommandResponse FromResult(
            string handle,
            string profileName,
            SshCommandResult result)
        {
            return new SendCommandResponse(
                handle,
                profileName,
                result.CommandName,
                result.CommandText,
                result.ExitCode,
                result.StandardOutput,
                result.StandardError,
                result.StartedAt,
                result.CompletedAt,
                result.TimedOut,
                string.Empty,
                string.Empty);
        }

        public static SendCommandResponse FromPolicyError(
            string handle,
            string profileName,
            string commandName,
            string policyError)
        {
            return new SendCommandResponse(
                handle,
                profileName,
                commandName,
                string.Empty,
                null,
                string.Empty,
                string.Empty,
                null,
                null,
                false,
                policyError,
                string.Empty);
        }

        public static SendCommandResponse FromError(
            string handle,
            string profileName,
            string commandName,
            string error)
        {
            return new SendCommandResponse(
                handle,
                profileName,
                commandName,
                string.Empty,
                null,
                string.Empty,
                string.Empty,
                null,
                null,
                false,
                string.Empty,
                error);
        }
    }
}
