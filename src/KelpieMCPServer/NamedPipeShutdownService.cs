using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;

namespace KelpieMCPServer;

/// <summary>
/// Listens for local NamedPipe control messages and stops the MCP server gracefully.
/// </summary>
public sealed class NamedPipeShutdownService : BackgroundService
{
    private static readonly UTF8Encoding ControlPipeEncoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ISshConnectionProfileCatalog _profileCatalog;
    private readonly ISshPasswordSessionStore _passwordSessionStore;
    private readonly IKelpieSecretStore _secretStore;
    private readonly IKelpieEnvironmentOverrideStore _environmentOverrideStore;
    private readonly SshCommandService _sshCommandService;
    private readonly ILogger<NamedPipeShutdownService> _logger;
    private readonly KelpieServerControlOptions _options;
    private readonly KelpieProfileOperationsOptions _profileOperations;
    private readonly Func<bool> _isWindowsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeShutdownService"/> class.
    /// </summary>
    /// <param name="applicationLifetime">The host lifetime used to request graceful shutdown.</param>
    /// <param name="logger">The logger for control channel diagnostics.</param>
    /// <param name="options">The local control channel options.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="passwordSessionStore">The SSH password session store.</param>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="isWindowsService">The optional Windows Service execution detector.</param>
    /// <param name="secretStore">The optional short-lived secret store.</param>
    /// <param name="environmentOverrideStore">The optional short-lived environment override store.</param>
    public NamedPipeShutdownService(
        IHostApplicationLifetime applicationLifetime,
        ILogger<NamedPipeShutdownService> logger,
        KelpieServerControlOptions options,
        ISshConnectionProfileCatalog profileCatalog,
        ISshPasswordSessionStore passwordSessionStore,
        SshCommandService sshCommandService,
        Func<bool>? isWindowsService = null,
        KelpieProfileOperationsOptions? profileOperations = null,
        IKelpieSecretStore? secretStore = null,
        IKelpieEnvironmentOverrideStore? environmentOverrideStore = null)
    {
        _applicationLifetime = applicationLifetime;
        _logger = logger;
        _options = options;
        _profileCatalog = profileCatalog;
        _passwordSessionStore = passwordSessionStore;
        _secretStore = secretStore ?? new InMemoryKelpieSecretStore();
        _environmentOverrideStore = environmentOverrideStore ?? new InMemoryKelpieEnvironmentOverrideStore();
        _sshCommandService = sshCommandService;
        _profileOperations = profileOperations ?? KelpieProfileOperationsOptions.Default;
        _isWindowsService = isWindowsService ?? WindowsServiceHelpers.IsWindowsService;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreateControlPipe(_options.PipeName, _isWindowsService());

                await pipe.WaitForConnectionAsync(stoppingToken);

                using var reader = new StreamReader(pipe, ControlPipeEncoding);
                var writer = new StreamWriter(pipe, ControlPipeEncoding)
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
                    await writer.WriteLineAsync(CreatePingResponse());
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

                if (TryGetArgument(message, "secret-put", out var secretPutRequestJson))
                {
                    await HandleSecretPutAsync(secretPutRequestJson, reader, writer, stoppingToken);
                    continue;
                }

                if (string.Equals(message, "secret-list", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleSecretListAsync(writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "secret-forget", out var secretName))
                {
                    await HandleSecretForgetAsync(secretName, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "env-put", out var envPutRequestJson))
                {
                    await HandleEnvPutAsync(envPutRequestJson, reader, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "env-list", out var envListRequestJson))
                {
                    await HandleEnvListAsync(envListRequestJson, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "env-forget", out var envForgetRequestJson))
                {
                    await HandleEnvForgetAsync(envForgetRequestJson, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "env-clear", out var envClearRequestJson))
                {
                    await HandleEnvClearAsync(envClearRequestJson, writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "profile-add", out var profileAddName))
                {
                    await HandleProfileTrustOperationAsync(profileAddName, "add", writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "profile-reload", out var profileReloadName))
                {
                    await HandleProfileTrustOperationAsync(profileReloadName, "reload", writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "profile-reload-approved", out var approvedProfileReloadName))
                {
                    await HandleProfileTrustOperationAsync(
                        approvedProfileReloadName,
                        "reload-approved",
                        writer,
                        stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "profile-revoke", out var profileRevokeName))
                {
                    await HandleProfileTrustOperationAsync(profileRevokeName, "revoke", writer, stoppingToken);
                    continue;
                }

                if (TryGetArgument(message, "profile-capabilities", out var profileCapabilitiesName))
                {
                    await HandleProfileCapabilitiesAsync(profileCapabilitiesName, writer, stoppingToken);
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

    private async Task HandleSecretPutAsync(
        string requestJson,
        TextReader reader,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        SecretPutRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<SecretPutRequest>(requestJson);
        }
        catch (JsonException)
        {
            await writer.WriteLineAsync("invalid-request");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            await writer.WriteLineAsync("secret-name-required");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        await writer.WriteLineAsync("secret-required");
        await writer.FlushAsync(cancellationToken);

        var contentBase64 = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            await writer.WriteLineAsync("secret-empty");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            await writer.WriteLineAsync("secret-invalid-base64");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        try
        {
            var ttl = request.TtlSeconds is > 0
                ? TimeSpan.FromSeconds(request.TtlSeconds.Value)
                : TimeSpan.FromMinutes(10);
            var info = _secretStore.Put(request.Name, content, ttl);
            KpLog.Info($"Secret stored for MCP session. name={info.Name}, size={info.Size}, expiresAtUtc={info.ExpiresAtUtc:O}");
            await writer.WriteLineAsync(JsonSerializer.Serialize(info));
            await writer.FlushAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await writer.WriteLineAsync("secret-rejected:" + ex.Message);
            await writer.FlushAsync(cancellationToken);
        }
    }

    private async Task HandleSecretListAsync(
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(JsonSerializer.Serialize(_secretStore.List()));
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleSecretForgetAsync(
        string secretName,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        var removed = _secretStore.Forget(secretName);
        KpLog.Info($"Secret forget requested. name={secretName}, removed={removed}");
        await writer.WriteLineAsync(removed ? "secret-forgotten" : "secret-not-found");
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleEnvPutAsync(
        string requestJson,
        TextReader reader,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        EnvPutRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<EnvPutRequest>(requestJson);
        }
        catch (JsonException)
        {
            await writer.WriteLineAsync("invalid-request");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ProfileName) || string.IsNullOrWhiteSpace(request.Key))
        {
            await writer.WriteLineAsync("invalid-request");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (!TryValidateEnvironmentOverride(request.ProfileName, request.Key, out var profile, out var failure))
        {
            await writer.WriteLineAsync(failure);
            await writer.FlushAsync(cancellationToken);
            return;
        }

        await writer.WriteLineAsync("env-value-required");
        await writer.FlushAsync(cancellationToken);

        var valueBase64 = await reader.ReadLineAsync(cancellationToken);
        if (valueBase64 is null)
        {
            await writer.WriteLineAsync("env-value-missing");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        string value;
        try
        {
            value = Encoding.UTF8.GetString(Convert.FromBase64String(valueBase64));
        }
        catch (FormatException)
        {
            await writer.WriteLineAsync("env-value-invalid-base64");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        try
        {
            var info = _environmentOverrideStore.Put(profile.Name, request.Key.Trim(), value);
            KpLog.Info($"Environment override stored for MCP session. profile={info.ProfileName}, key={info.Key}, valueLength={info.ValueLength}");
            await writer.WriteLineAsync(JsonSerializer.Serialize(info));
            await writer.FlushAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            await writer.WriteLineAsync("env-rejected:" + ex.Message);
            await writer.FlushAsync(cancellationToken);
        }
    }

    private async Task HandleEnvListAsync(
        string requestJson,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        EnvListRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<EnvListRequest>(requestJson);
        }
        catch (JsonException)
        {
            await writer.WriteLineAsync("invalid-request");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        var profileName = request?.ProfileName?.Trim();
        if (!string.IsNullOrWhiteSpace(profileName) && !_profileCatalog.TryGet(profileName, out _))
        {
            await writer.WriteLineAsync("profile-not-found");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        await writer.WriteLineAsync(JsonSerializer.Serialize(_environmentOverrideStore.List(profileName)));
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleEnvForgetAsync(
        string requestJson,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        EnvForgetRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<EnvForgetRequest>(requestJson);
        }
        catch (JsonException)
        {
            await writer.WriteLineAsync("invalid-request");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ProfileName) || string.IsNullOrWhiteSpace(request.Key))
        {
            await writer.WriteLineAsync("invalid-request");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (!TryValidateEnvironmentOverride(request.ProfileName, request.Key, out var profile, out var failure))
        {
            await writer.WriteLineAsync(failure);
            await writer.FlushAsync(cancellationToken);
            return;
        }

        var removed = _environmentOverrideStore.Forget(profile.Name, request.Key.Trim());
        KpLog.Info($"Environment override forget requested. profile={profile.Name}, key={request.Key.Trim()}, removed={removed}");
        await writer.WriteLineAsync(removed ? "env-forgotten" : "env-not-found");
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleEnvClearAsync(
        string requestJson,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        EnvClearRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<EnvClearRequest>(requestJson);
        }
        catch (JsonException)
        {
            await writer.WriteLineAsync("invalid-request");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.ProfileName))
        {
            await writer.WriteLineAsync("invalid-request");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (!_profileCatalog.TryGet(request.ProfileName, out var profile))
        {
            await writer.WriteLineAsync("profile-not-found");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (!profile.Capabilities.Allows(KelpiePolicyNames.AllowSetEnvironmentValues))
        {
            await writer.WriteLineAsync("env-set-not-allowed");
            await writer.FlushAsync(cancellationToken);
            return;
        }

        var removed = _environmentOverrideStore.Clear(profile.Name);
        KpLog.Info($"Environment overrides cleared for MCP session. profile={profile.Name}, removed={removed}");
        await writer.WriteLineAsync(JsonSerializer.Serialize(new EnvClearResponse(profile.Name, removed)));
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

    private async Task HandleProfileTrustOperationAsync(
        string profileName,
        string operation,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        var policyOperation = string.Equals(operation, "reload-approved", StringComparison.Ordinal)
            ? "reload"
            : operation;
        if (!_profileOperations.IsAllowed(policyOperation, "CLI"))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new SshProfileTrustOperationResult(
                false,
                profileName,
                "disabled-by-config",
                "Profile operation is disabled by kelpiemcp.json.")));
            await writer.FlushAsync(cancellationToken);
            return;
        }

        if (_profileCatalog is not ReloadingSshConnectionProfileCatalog reloadingCatalog)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new SshProfileTrustOperationResult(
                false,
                profileName,
                "trust-disabled",
                "Profile trust store is not enabled.")));
            await writer.FlushAsync(cancellationToken);
            return;
        }

        var result = operation switch
        {
            "add" => reloadingCatalog.AddTrustedProfile(profileName),
            "reload" => reloadingCatalog.ReloadTrustedProfile(profileName),
            "reload-approved" => reloadingCatalog.ReloadTrustedProfile(profileName, approvePrivilegeExpansion: true),
            "revoke" => reloadingCatalog.RevokeTrustedProfile(profileName),
            _ => new SshProfileTrustOperationResult(false, profileName, "unknown-operation", "Unknown profile trust operation."),
        };

        if (result.AuthorizationChange == SshProfileAuthorizationChangeKind.PrivilegeExpansion)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            var fields = string.Join(",", result.ChangedFields ?? []);
            KpLog.Warn($"Profile authorization expansion evaluated. profile={profileName}, status={result.Status}, fields={fields}, source=cli_named_pipe, correlationId={correlationId}");
        }

        await writer.WriteLineAsync(JsonSerializer.Serialize(result));
        await writer.FlushAsync(cancellationToken);
    }

    private async Task HandleProfileCapabilitiesAsync(
        string profileName,
        TextWriter writer,
        CancellationToken cancellationToken)
    {
        if (_profileCatalog is not ReloadingSshConnectionProfileCatalog reloadingCatalog)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new SshProfileTrustCapabilities(
                profileName,
                false,
                false,
                false,
                "trust-disabled")));
            await writer.FlushAsync(cancellationToken);
            return;
        }

        var capabilities = reloadingCatalog.GetTrustCapabilities(profileName);
        await writer.WriteLineAsync(JsonSerializer.Serialize(ApplyCliProfileOperationPolicy(capabilities)));
        await writer.FlushAsync(cancellationToken);
    }

    private SshProfileTrustCapabilities ApplyCliProfileOperationPolicy(SshProfileTrustCapabilities capabilities)
    {
        var addAllowed = capabilities.AddAllowed && _profileOperations.IsAllowed("add", "CLI");
        var reloadAllowed = capabilities.ReloadAllowed && _profileOperations.IsAllowed("reload", "CLI");
        var revokeAllowed = capabilities.RevokeAllowed && _profileOperations.IsAllowed("revoke", "CLI");
        var reason = addAllowed || reloadAllowed || revokeAllowed
            ? capabilities.Reason
            : !_profileOperations.IsAllowed("add", "CLI")
                || !_profileOperations.IsAllowed("reload", "CLI")
                || !_profileOperations.IsAllowed("revoke", "CLI")
                    ? "disabled-by-config"
                    : capabilities.Reason;

        return capabilities with
        {
            AddAllowed = addAllowed,
            ReloadAllowed = reloadAllowed,
            RevokeAllowed = revokeAllowed,
            Reason = reason,
        };
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
                cancellationToken: cancellationToken);

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

    private bool TryValidateEnvironmentOverride(
        string profileName,
        string key,
        out SshConnectionProfile profile,
        out string failure)
    {
        profile = default!;
        failure = string.Empty;
        if (!_profileCatalog.TryGet(profileName, out profile))
        {
            failure = "profile-not-found";
            return false;
        }

        if (!profile.Capabilities.Allows(KelpiePolicyNames.AllowSetEnvironmentValues))
        {
            failure = "env-set-not-allowed";
            return false;
        }

        try
        {
            ValidateEnvironmentKey(key);
        }
        catch (InvalidOperationException)
        {
            failure = "env-key-invalid";
            return false;
        }

        var rule = profile.EnvironmentValues.FirstOrDefault(rule =>
            string.Equals(rule.Key, key.Trim(), StringComparison.Ordinal));
        if (rule is null || !rule.AllowsSetValue || rule.IsHidden)
        {
            failure = "env-key-not-allowed";
            return false;
        }

        return true;
    }

    private static void ValidateEnvironmentKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)
            || !key.Trim().All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_')
            || char.IsDigit(key.Trim()[0]))
        {
            throw new InvalidOperationException($"Environment variable key is invalid: {key}");
        }
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

    private string CreatePingResponse()
    {
        var windowsService = _isWindowsService() ? "true" : "false";
        return "pong;windowsService=" + windowsService;
    }

    private static bool IsClientDisconnected(IOException ex)
    {
        return ex.Message.Contains("Pipe is broken", StringComparison.OrdinalIgnoreCase);
    }

    private static NamedPipeServerStream CreateControlPipe(string pipeName, bool isWindowsService)
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
        using var identity = WindowsIdentity.GetCurrent();
        var currentUser = identity.User;
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        if (currentUser is not null)
        {
            security.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        }

        security.AddAccessRule(new PipeAccessRule(admins, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(localSystem, PipeAccessRights.FullControl, AccessControlType.Allow));
        if (isWindowsService)
        {
            var interactiveUsers = new SecurityIdentifier(WellKnownSidType.InteractiveSid, null);
            var remoteInteractiveUsers = new SecurityIdentifier("S-1-5-14");
            security.AddAccessRule(new PipeAccessRule(interactiveUsers, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(remoteInteractiveUsers, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        }

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

    private sealed record SecretPutRequest(
        string Name,
        int? TtlSeconds);

    private sealed record EnvPutRequest(
        string ProfileName,
        string Key);

    private sealed record EnvListRequest(
        string? ProfileName);

    private sealed record EnvForgetRequest(
        string ProfileName,
        string Key);

    private sealed record EnvClearRequest(
        string ProfileName);

    private sealed record EnvClearResponse(
        string ProfileName,
        int Removed);

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
