using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;

namespace KelpieServerCommand;

/// <summary>
/// Implements the kelpiemcp frontend commands.
/// </summary>
public static class KelpieServerCommandRunner
{
    private static readonly TimeSpan PipeConnectionTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly UTF8Encoding ControlPipeEncoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private const string WindowsServiceName = "KelpieMCPServer";
    private const string WindowsServiceDisplayName = "KelpieSSH MCP Server";
    private const string WindowsServiceDescription = "Provides the local KelpieSSH MCP server endpoint for AI clients.";

    /// <summary>
    /// Starts the Kelpie MCP server body if it is not already running.
    /// </summary>
    /// <param name="options">The command options.</param>
    public static async Task StartAsync(
        KelpieMcpServerOptions options,
        Func<Task<bool>>? windowsServiceExistsAsync = null,
        Func<Task<bool>>? startWindowsServiceAsync = null)
    {
        if (await SendControlCommandAsync(options.ControlPipeName, "ping", PipeConnectionTimeout))
        {
            KpLog.Info("KelpieMCPServer is already running.");
            Console.WriteLine("KelpieMCPServer is already running.");
            return;
        }

        var registeredAsWindowsService = OperatingSystem.IsWindows()
            && await (windowsServiceExistsAsync ?? WindowsServiceExistsAsync)();
        if (registeredAsWindowsService)
        {
            var started = startWindowsServiceAsync is not null
                ? await startWindowsServiceAsync()
                : await StartWindowsServiceAndWriteFailureAsync(options.ReloadConfig, options.ReloadProfileNames);
            if (!started)
            {
                return;
            }

            KpLog.Info($"KelpieMCPServer Windows Service start requested. service={WindowsServiceName}");
            Console.WriteLine($"Windows Service start requested: {WindowsServiceName}");
            return;
        }

        var serverCommand = AddServerArguments(
            ResolveServerCommand(options),
            CreateServerStartArguments(options));
        StartServerProcess(serverCommand);
        KpLog.Info("KelpieMCPServer start requested.");
        Console.WriteLine("KelpieMCPServer start requested.");
    }

    /// <summary>
    /// Requests graceful shutdown of the running Kelpie MCP server body.
    /// </summary>
    /// <param name="options">The command options.</param>
    public static async Task StopAsync(KelpieMcpServerOptions options)
    {
        if (await SendControlCommandAsync(options.ControlPipeName, "stop", PipeConnectionTimeout))
        {
            KpLog.Info("KelpieMCPServer stop requested.");
            Console.WriteLine("KelpieMCPServer stop requested.");
            return;
        }

        KpLog.Warn("KelpieMCPServer is not running.");
        Console.Error.WriteLine("KelpieMCPServer is not running.");
        Environment.ExitCode = 1;
    }

    /// <summary>
    /// Prints the current Kelpie MCP server body status.
    /// </summary>
    /// <param name="options">The command options.</param>
    public static async Task StatusAsync(
        KelpieMcpServerOptions options,
        Func<Task<bool>>? windowsServiceExistsAsync = null)
    {
        var registeredAsWindowsService = OperatingSystem.IsWindows()
            && await (windowsServiceExistsAsync ?? WindowsServiceExistsAsync)();
        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "ping",
            PipeConnectionTimeout);

        var status = ParsePingResponse(response);
        if (status.IsRunning)
        {
            var mcpUrl = $"http://127.0.0.1:{options.ServerPort}/mcp";
            var healthUrl = $"http://127.0.0.1:{options.ServerPort}/health";
            KpLog.Info($"KelpieMCPServer status: running ({mcpUrl}).");
            Console.WriteLine("KelpieMCPServer: running");
            Console.WriteLine($"MCP URL: {mcpUrl}");
            Console.WriteLine($"Health URL: {healthUrl}");
            Console.WriteLine($"Control pipe: {options.ControlPipeName}");
            Console.WriteLine($"Registered as Windows service: {FormatYesNo(registeredAsWindowsService)}");
            return;
        }

        KpLog.Info("KelpieMCPServer status: stopped.");
        Console.WriteLine("KelpieMCPServer: stopped");
        Console.WriteLine($"Registered as Windows service: {FormatYesNo(registeredAsWindowsService)}");
    }

    /// <summary>
    /// Registers KelpieMCPServer as a Windows Service.
    /// </summary>
    /// <param name="options">The command options.</param>
    public static async Task RegisterServiceAsync(KelpieMcpServerOptions options)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Windows Service registration is supported on Windows only.");
            Environment.ExitCode = 1;
            return;
        }

        var serverCommand = ResolveServerCommand(options);
        var binPath = CreateWindowsServiceBinPath(serverCommand, options.ServerPort);
        var serviceExists = await WindowsServiceExistsAsync();
        var result = serviceExists
            ? await ConfigureWindowsServiceAsync(binPath)
            : await CreateWindowsServiceAsync(binPath);

        if (result.ExitCode != 0)
        {
            WriteScFailure("Failed to register Windows Service.", result);
            return;
        }

        var descriptionResult = await RunScAsync("description", WindowsServiceName, WindowsServiceDescription);
        if (descriptionResult.ExitCode != 0)
        {
            WriteScFailure("Failed to set Windows Service description.", descriptionResult);
            return;
        }

        Console.WriteLine(serviceExists
            ? $"Windows Service updated: {WindowsServiceName}"
            : $"Windows Service registered: {WindowsServiceName}");
        Console.WriteLine("Startup type: automatic");
        Console.WriteLine($"Binary path: {binPath}");
    }

    /// <summary>
    /// Unregisters the KelpieMCPServer Windows Service.
    /// </summary>
    public static async Task UnregisterServiceAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Windows Service registration is supported on Windows only.");
            Environment.ExitCode = 1;
            return;
        }

        if (!await WindowsServiceExistsAsync())
        {
            Console.WriteLine($"Windows Service is not registered: {WindowsServiceName}");
            return;
        }

        var result = await RunScAsync("delete", WindowsServiceName);
        if (result.ExitCode != 0)
        {
            WriteScFailure("Failed to unregister Windows Service.", result);
            return;
        }

        Console.WriteLine($"Windows Service unregistered: {WindowsServiceName}");
    }

    /// <summary>
    /// Prints the KelpieMCPServer Windows Service registration status.
    /// </summary>
    public static async Task ServiceStatusAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Windows Service status is supported on Windows only.");
            Environment.ExitCode = 1;
            return;
        }

        var result = await RunScAsync("query", WindowsServiceName);
        if (result.ExitCode != 0)
        {
            Console.WriteLine($"Windows Service: not registered ({WindowsServiceName})");
            return;
        }

        Console.WriteLine($"Windows Service: registered ({WindowsServiceName})");
        var stateLine = result.StandardOutput
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.Contains("STATE", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(stateLine))
        {
            Console.WriteLine(stateLine.Trim());
        }
    }

    /// <summary>
    /// Prints the temporary SSH sessions held by the running server.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="setExitCodeWhenUnavailable">Whether to set a non-zero exit code when the server is unavailable.</param>
    public static async Task SessionsAsync(KelpieMcpServerOptions options, bool setExitCodeWhenUnavailable = true)
    {
        Console.WriteLine("Sessions:");

        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "sessions",
            TimeSpan.FromSeconds(3));

        if (response is null)
        {
            Console.WriteLine("(KelpieMCPServer is not running.)");
            if (setExitCodeWhenUnavailable)
            {
                Environment.ExitCode = 1;
            }

            return;
        }

        var sessions = DeserializeSessions(response);
        if (sessions.Count == 0)
        {
            Console.WriteLine("(none)");
            return;
        }

        foreach (var session in sessions)
        {
            Console.WriteLine(
                $"{session.Handle}  {session.ProfileName}  {session.Kind}  {session.StartedAtUtc:yyyy-MM-dd HH:mm:ss}Z");
        }
    }

    /// <summary>
    /// Kills a temporary SSH session held by the running server.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="handle">The session handle.</param>
    public static async Task KillAsync(KelpieMcpServerOptions options, string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            Console.Error.WriteLine("Session handle is required.");
            Environment.ExitCode = 1;
            return;
        }

        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "kill " + handle,
            TimeSpan.FromSeconds(3));

        if (string.Equals(response, "killed", StringComparison.OrdinalIgnoreCase))
        {
            KpLog.Info($"SSH session killed. handle={handle}");
            Console.WriteLine($"SSH session killed: {handle}");
            return;
        }

        var message = response switch
        {
            "session-not-found" => $"SSH session was not found: {handle}",
            null => "KelpieMCPServer is not running.",
            _ => $"KelpieMCPServer returned unexpected response: {response}",
        };

        KpLog.Warn(message);
        Console.Error.WriteLine(message);
        Environment.ExitCode = 1;
    }

    /// <summary>
    /// Adds a trusted SSH profile.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The profile name.</param>
    public static Task ProfileAddAsync(KelpieMcpServerOptions options, string profileName)
    {
        return RunProfileTrustOperationAsync(options, "add", "profile-add", profileName, profile => CreateOfflineCatalog().AddTrustedProfile(profile));
    }

    /// <summary>
    /// Reloads a trusted SSH profile.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The profile name.</param>
    public static Task ProfileReloadAsync(
        KelpieMcpServerOptions options,
        string profileName,
        bool approvePrivilegeExpansion = false)
    {
        var pipeCommand = approvePrivilegeExpansion ? "profile-reload-approved" : "profile-reload";
        return RunProfileTrustOperationAsync(
            options,
            "reload",
            pipeCommand,
            profileName,
            profile => CreateOfflineCatalog().ReloadTrustedProfile(profile, approvePrivilegeExpansion));
    }

    /// <summary>
    /// Revokes a trusted SSH profile.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The profile name.</param>
    public static Task ProfileRevokeAsync(KelpieMcpServerOptions options, string profileName)
    {
        return RunProfileTrustOperationAsync(options, "revoke", "profile-revoke", profileName, profile => CreateOfflineCatalog().RevokeTrustedProfile(profile));
    }

    /// <summary>
    /// Prints profile trust operation capabilities.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The optional profile name.</param>
    public static async Task ProfileCapabilitiesAsync(KelpieMcpServerOptions options, string profileName)
    {
        var resolvedProfileName = string.IsNullOrWhiteSpace(profileName)
            ? LoadOpenProfileName()
            : profileName.Trim();
        if (string.IsNullOrWhiteSpace(resolvedProfileName))
        {
            Console.Error.WriteLine("Profile name is required.");
            Environment.ExitCode = 1;
            return;
        }

        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "profile-capabilities " + resolvedProfileName,
            TimeSpan.FromSeconds(3));
        var capabilities = !string.IsNullOrWhiteSpace(response)
            ? JsonSerializer.Deserialize<SshProfileTrustCapabilities>(response)
                ?? new SshProfileTrustCapabilities(resolvedProfileName, false, false, false, "invalid-response")
            : CreateOfflineCatalog().GetTrustCapabilities(resolvedProfileName);
        Console.WriteLine(JsonSerializer.Serialize(ApplyCliProfileOperationPolicy(options, capabilities)));
    }

    /// <summary>
    /// Stores a short-lived secret payload in the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="args">The secret command arguments.</param>
    public static async Task SecretPutAsync(KelpieMcpServerOptions options, IReadOnlyList<string> args)
    {
        var request = ParseSecretPutArguments(args);
        if (request.Error is not null)
        {
            Console.Error.WriteLine(request.Error);
            Environment.ExitCode = 1;
            return;
        }

        byte[] content;
        try
        {
            content = File.ReadAllBytes(request.FromFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine("Failed to read secret file: " + ex.Message);
            Environment.ExitCode = 1;
            return;
        }

        var response = await SendSecretPutCommandAsync(
            options.ControlPipeName,
            request.Name,
            request.TtlSeconds,
            content,
            TimeSpan.FromSeconds(10));

        if (response is null)
        {
            Console.Error.WriteLine("KelpieMCPServer is not running.");
            Environment.ExitCode = 1;
            return;
        }

        if (response.StartsWith("secret-rejected:", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(response["secret-rejected:".Length..]);
            Environment.ExitCode = 1;
            return;
        }

        if (!TryDeserializeSecretInfo(response, out var info))
        {
            WriteSecretFailure(response);
            Environment.ExitCode = 1;
            return;
        }

        KpLog.Info($"Secret stored for this KelpieMCPServer session. name={info.Name}, size={info.Size}, expiresAtUtc={info.ExpiresAtUtc:O}");
        Console.WriteLine("Secret stored for this KelpieMCPServer session.");
        Console.WriteLine($"Name: {info.Name}");
        Console.WriteLine($"Size: {info.Size} bytes");
        Console.WriteLine($"ExpiresAtUtc: {info.ExpiresAtUtc:O}");
    }

    /// <summary>
    /// Lists short-lived secret references in the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    public static async Task SecretListAsync(KelpieMcpServerOptions options)
    {
        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "secret-list",
            TimeSpan.FromSeconds(3));
        if (response is null)
        {
            Console.Error.WriteLine("KelpieMCPServer is not running.");
            Environment.ExitCode = 1;
            return;
        }

        IReadOnlyCollection<KelpieSecretInfo> secrets;
        try
        {
            secrets = JsonSerializer.Deserialize<KelpieSecretInfo[]>(response) ?? [];
        }
        catch (JsonException)
        {
            Console.Error.WriteLine("KelpieMCPServer returned an invalid secret list.");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("Secrets:");
        if (secrets.Count == 0)
        {
            Console.WriteLine("(none)");
            return;
        }

        foreach (var secret in secrets)
        {
            Console.WriteLine($"{secret.Name}  {secret.Size} bytes  expires {secret.ExpiresAtUtc:yyyy-MM-dd HH:mm:ss}Z");
        }
    }

    /// <summary>
    /// Removes one short-lived secret reference from the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="args">The secret command arguments.</param>
    public static async Task SecretForgetAsync(KelpieMcpServerOptions options, IReadOnlyList<string> args)
    {
        var secretName = args.Count > 0 ? args[0].Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(secretName))
        {
            Console.Error.WriteLine("Secret name is required.");
            Environment.ExitCode = 1;
            return;
        }

        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "secret-forget " + secretName,
            TimeSpan.FromSeconds(3));
        if (string.Equals(response, "secret-forgotten", StringComparison.OrdinalIgnoreCase))
        {
            KpLog.Info($"Secret cleared for this KelpieMCPServer session. name={secretName}");
            Console.WriteLine("Secret cleared for this KelpieMCPServer session.");
            return;
        }

        WriteSecretFailure(response);
        Environment.ExitCode = 1;
    }

    /// <summary>
    /// Stores one environment override in the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="args">The env command arguments.</param>
    public static async Task EnvPutAsync(KelpieMcpServerOptions options, IReadOnlyList<string> args)
    {
        if (args.Count < 3)
        {
            Console.Error.WriteLine("Usage: kelpiemcp env put <profile> <key> <value>");
            Environment.ExitCode = 1;
            return;
        }

        var profileName = args[0].Trim();
        var key = args[1].Trim();
        var value = args[2];
        var response = await SendEnvPutCommandAsync(
            options.ControlPipeName,
            profileName,
            key,
            value,
            TimeSpan.FromSeconds(10));
        if (response is null)
        {
            Console.Error.WriteLine("KelpieMCPServer is not running.");
            Environment.ExitCode = 1;
            return;
        }

        if (response.StartsWith("env-rejected:", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(response["env-rejected:".Length..]);
            Environment.ExitCode = 1;
            return;
        }

        if (!TryDeserializeEnvironmentOverrideInfo(response, out var info))
        {
            WriteEnvFailure(response);
            Environment.ExitCode = 1;
            return;
        }

        KpLog.Info($"Environment override stored for this KelpieMCPServer session. profile={info.ProfileName}, key={info.Key}, valueLength={info.ValueLength}");
        Console.WriteLine("Environment override stored for this KelpieMCPServer session.");
        Console.WriteLine($"Profile: {info.ProfileName}");
        Console.WriteLine($"Key: {info.Key}");
        Console.WriteLine($"ValueLength: {info.ValueLength}");
        Console.WriteLine($"UpdatedAtUtc: {info.UpdatedAtUtc:O}");
    }

    /// <summary>
    /// Lists environment overrides in the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The optional SSH profile name.</param>
    public static async Task EnvListAsync(KelpieMcpServerOptions options, string profileName)
    {
        var request = JsonSerializer.Serialize(new EnvListRequest(
            string.IsNullOrWhiteSpace(profileName) ? null : profileName.Trim()));
        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "env-list " + request,
            TimeSpan.FromSeconds(3));
        if (response is null)
        {
            Console.Error.WriteLine("KelpieMCPServer is not running.");
            Environment.ExitCode = 1;
            return;
        }

        IReadOnlyCollection<KelpieEnvironmentOverrideInfo> overrides;
        try
        {
            overrides = JsonSerializer.Deserialize<KelpieEnvironmentOverrideInfo[]>(response) ?? [];
        }
        catch (JsonException)
        {
            WriteEnvFailure(response);
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("Environment overrides:");
        if (overrides.Count == 0)
        {
            Console.WriteLine("(none)");
            return;
        }

        foreach (var item in overrides)
        {
            Console.WriteLine($"{item.ProfileName}  {item.Key}  length {item.ValueLength}  updated {item.UpdatedAtUtc:yyyy-MM-dd HH:mm:ss}Z");
        }
    }

    /// <summary>
    /// Removes one environment override from the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="args">The env command arguments.</param>
    public static async Task EnvForgetAsync(KelpieMcpServerOptions options, IReadOnlyList<string> args)
    {
        if (args.Count < 2)
        {
            Console.Error.WriteLine("Usage: kelpiemcp env forget <profile> <key>");
            Environment.ExitCode = 1;
            return;
        }

        var request = JsonSerializer.Serialize(new EnvForgetRequest(args[0].Trim(), args[1].Trim()));
        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "env-forget " + request,
            TimeSpan.FromSeconds(3));
        if (string.Equals(response, "env-forgotten", StringComparison.OrdinalIgnoreCase))
        {
            KpLog.Info($"Environment override cleared for this KelpieMCPServer session. profile={args[0].Trim()}, key={args[1].Trim()}");
            Console.WriteLine("Environment override cleared for this KelpieMCPServer session.");
            return;
        }

        WriteEnvFailure(response);
        Environment.ExitCode = 1;
    }

    /// <summary>
    /// Removes every environment override for one profile from the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The SSH profile name.</param>
    public static async Task EnvClearAsync(KelpieMcpServerOptions options, string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            Console.Error.WriteLine("Profile name is required.");
            Environment.ExitCode = 1;
            return;
        }

        var request = JsonSerializer.Serialize(new EnvClearRequest(profileName.Trim()));
        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "env-clear " + request,
            TimeSpan.FromSeconds(3));
        if (!TryDeserializeEnvClearResponse(response, out var clearResponse))
        {
            WriteEnvFailure(response);
            Environment.ExitCode = 1;
            return;
        }

        KpLog.Info($"Environment overrides cleared for this KelpieMCPServer session. profile={clearResponse.ProfileName}, removed={clearResponse.Removed}");
        Console.WriteLine("Environment overrides cleared for this KelpieMCPServer session.");
        Console.WriteLine($"Profile: {clearResponse.ProfileName}");
        Console.WriteLine($"Removed: {clearResponse.Removed}");
    }

    /// <summary>
    /// Stores a password for one SSH profile in the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="passwordReader">The optional password reader used by tests.</param>
    public static async Task PasswordAsync(
        KelpieMcpServerOptions options,
        string profileName,
        Func<string>? passwordReader = null)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            Console.Error.WriteLine("Profile name is required.");
            Environment.ExitCode = 1;
            return;
        }

        var response = await SendLoginCommandAsync(
            options.ControlPipeName,
            profileName,
            passwordReader ?? (() => ReadPasswordFromConsole(profileName)),
            TimeSpan.FromSeconds(10));

        if (string.Equals(response, "logged-in", StringComparison.OrdinalIgnoreCase))
        {
            KpLog.Info($"SSH password session stored. profile={profileName}");
            Console.WriteLine("SSH password stored for this KelpieMCPServer session.");
            return;
        }

        WriteLoginFailure(response);
        Environment.ExitCode = 1;
    }

    /// <summary>
    /// Compatibility alias for storing a password for one SSH profile in the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="passwordReader">The optional password reader used by tests.</param>
    public static Task LoginAsync(
        KelpieMcpServerOptions options,
        string profileName,
        Func<string>? passwordReader = null)
    {
        return PasswordAsync(options, profileName, passwordReader);
    }

    /// <summary>
    /// Clears a stored password for one SSH profile in the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The SSH profile name.</param>
    public static async Task ForgetAsync(KelpieMcpServerOptions options, string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            Console.Error.WriteLine("Profile name is required.");
            Environment.ExitCode = 1;
            return;
        }

        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            "logout " + profileName,
            TimeSpan.FromSeconds(3));

        if (string.Equals(response, "logged-out", StringComparison.OrdinalIgnoreCase))
        {
            KpLog.Info($"SSH password session cleared. profile={profileName}");
            Console.WriteLine("SSH password cleared for this KelpieMCPServer session.");
            return;
        }

        WriteLoginFailure(response);
        Environment.ExitCode = 1;
    }

    /// <summary>
    /// Compatibility alias for clearing a stored password for one SSH profile in the running server session.
    /// </summary>
    /// <param name="options">The command options.</param>
    /// <param name="profileName">The SSH profile name.</param>
    public static Task LogoutAsync(KelpieMcpServerOptions options, string profileName)
    {
        return ForgetAsync(options, profileName);
    }

    private static async Task<bool> SendControlCommandAsync(
        string pipeName,
        string command,
        TimeSpan timeout)
    {
        var response = await SendControlCommandWithResponseAsync(pipeName, command, timeout);
        return response is not null;
    }

    private static async Task<string?> SendControlCommandWithResponseAsync(
        string pipeName,
        string command,
        TimeSpan timeout,
        Action<ControlPipeFailure>? reportFailure = null)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var cancellationTokenSource = new CancellationTokenSource(timeout);
            await pipe.ConnectAsync(cancellationTokenSource.Token);

            var writer = new StreamWriter(pipe, ControlPipeEncoding)
            {
                AutoFlush = true,
            };
            await writer.WriteLineAsync(command);
            await writer.FlushAsync(cancellationTokenSource.Token);

            using var reader = new StreamReader(pipe, ControlPipeEncoding);
            return await reader.ReadLineAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            reportFailure?.Invoke(ControlPipeFailure.Timeout);
            return null;
        }
        catch (IOException)
        {
            reportFailure?.Invoke(ControlPipeFailure.Unavailable);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            reportFailure?.Invoke(ControlPipeFailure.AccessDenied);
            return null;
        }
        catch (TimeoutException)
        {
            reportFailure?.Invoke(ControlPipeFailure.Timeout);
            return null;
        }
    }

    private static async Task<string?> SendLoginCommandAsync(
        string pipeName,
        string profileName,
        Func<string> passwordReader,
        TimeSpan timeout)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var cancellationTokenSource = new CancellationTokenSource(timeout);
            await pipe.ConnectAsync(cancellationTokenSource.Token);

            var writer = new StreamWriter(pipe, ControlPipeEncoding)
            {
                AutoFlush = true,
            };
            var reader = new StreamReader(pipe, ControlPipeEncoding);

            await writer.WriteLineAsync("login " + profileName);
            await writer.FlushAsync(cancellationTokenSource.Token);

            var response = await reader.ReadLineAsync(cancellationTokenSource.Token);
            if (!string.Equals(response, "password-required", StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }

            var password = passwordReader();
            await writer.WriteLineAsync(password);
            await writer.FlushAsync(cancellationTokenSource.Token);
            return await reader.ReadLineAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private static async Task<string?> SendSecretPutCommandAsync(
        string pipeName,
        string secretName,
        int ttlSeconds,
        byte[] content,
        TimeSpan timeout)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var cancellationTokenSource = new CancellationTokenSource(timeout);
            await pipe.ConnectAsync(cancellationTokenSource.Token);

            var writer = new StreamWriter(pipe, ControlPipeEncoding)
            {
                AutoFlush = true,
            };
            var reader = new StreamReader(pipe, ControlPipeEncoding);

            var request = JsonSerializer.Serialize(new SecretPutRequest(secretName, ttlSeconds));
            await writer.WriteLineAsync("secret-put " + request);
            await writer.FlushAsync(cancellationTokenSource.Token);

            var response = await reader.ReadLineAsync(cancellationTokenSource.Token);
            if (!string.Equals(response, "secret-required", StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }

            await writer.WriteLineAsync(Convert.ToBase64String(content));
            await writer.FlushAsync(cancellationTokenSource.Token);
            return await reader.ReadLineAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private static async Task<string?> SendEnvPutCommandAsync(
        string pipeName,
        string profileName,
        string key,
        string value,
        TimeSpan timeout)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var cancellationTokenSource = new CancellationTokenSource(timeout);
            await pipe.ConnectAsync(cancellationTokenSource.Token);

            var writer = new StreamWriter(pipe, ControlPipeEncoding)
            {
                AutoFlush = true,
            };
            var reader = new StreamReader(pipe, ControlPipeEncoding);

            var request = JsonSerializer.Serialize(new EnvPutRequest(profileName, key));
            await writer.WriteLineAsync("env-put " + request);
            await writer.FlushAsync(cancellationTokenSource.Token);

            var response = await reader.ReadLineAsync(cancellationTokenSource.Token);
            if (!string.Equals(response, "env-value-required", StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }

            await writer.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
            await writer.FlushAsync(cancellationTokenSource.Token);
            return await reader.ReadLineAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private static string ReadPasswordFromConsole(string profileName)
    {
        Console.Error.Write($"Password for {profileName}: ");
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return builder.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }
    }

    private static void WriteLoginFailure(string? response)
    {
        var message = response switch
        {
            "profile-not-found" => "SSH profile was not found.",
            "password-authentication-not-configured" => "SSH profile does not use password authentication.",
            "password-secret-not-configured" => "SSH password secret name is not configured.",
            "password-empty" => "SSH password is required.",
            null => "KelpieMCPServer is not running.",
            _ => $"KelpieMCPServer returned unexpected response: {response}",
        };

        KpLog.Warn(message);
        Console.Error.WriteLine(message);
    }

    private static void WriteSecretFailure(string? response)
    {
        var message = response switch
        {
            "secret-name-required" => "Secret name is required.",
            "secret-empty" => "Secret content is required.",
            "secret-invalid-base64" => "Secret content could not be transferred.",
            "secret-not-found" => "Secret reference was not found.",
            "invalid-request" => "KelpieMCPServer returned invalid request response.",
            null => "KelpieMCPServer is not running.",
            _ => $"KelpieMCPServer returned unexpected response: {response}",
        };

        KpLog.Warn(message);
        Console.Error.WriteLine(message);
    }

    private static void WriteEnvFailure(string? response)
    {
        var message = response switch
        {
            "profile-not-found" => "SSH profile was not found.",
            "env-set-not-allowed" => "SSH profile does not allow setting environment values.",
            "env-key-invalid" => "Environment variable key is invalid.",
            "env-key-not-allowed" => "Environment variable key is not allowed by the SSH profile.",
            "env-value-missing" => "Environment value was not transferred.",
            "env-value-invalid-base64" => "Environment value could not be transferred.",
            "env-not-found" => "Environment override was not found.",
            "invalid-request" => "KelpieMCPServer returned invalid request response.",
            null => "KelpieMCPServer is not running.",
            _ => $"KelpieMCPServer returned unexpected response: {response}",
        };

        KpLog.Warn(message);
        Console.Error.WriteLine(message);
    }

    private static bool TryDeserializeSecretInfo(string response, out KelpieSecretInfo info)
    {
        try
        {
            info = JsonSerializer.Deserialize<KelpieSecretInfo>(response) ?? new KelpieSecretInfo(string.Empty, 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
            return !string.IsNullOrWhiteSpace(info.Name);
        }
        catch (JsonException)
        {
            info = new KelpieSecretInfo(string.Empty, 0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
            return false;
        }
    }

    private static bool TryDeserializeEnvironmentOverrideInfo(string response, out KelpieEnvironmentOverrideInfo info)
    {
        try
        {
            info = JsonSerializer.Deserialize<KelpieEnvironmentOverrideInfo>(response) ?? new KelpieEnvironmentOverrideInfo(string.Empty, string.Empty, 0, DateTimeOffset.MinValue);
            return !string.IsNullOrWhiteSpace(info.ProfileName) && !string.IsNullOrWhiteSpace(info.Key);
        }
        catch (JsonException)
        {
            info = new KelpieEnvironmentOverrideInfo(string.Empty, string.Empty, 0, DateTimeOffset.MinValue);
            return false;
        }
    }

    private static bool TryDeserializeEnvClearResponse(string? response, out EnvClearResponse clearResponse)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            clearResponse = new EnvClearResponse(string.Empty, 0);
            return false;
        }

        try
        {
            clearResponse = JsonSerializer.Deserialize<EnvClearResponse>(response) ?? new EnvClearResponse(string.Empty, 0);
            return !string.IsNullOrWhiteSpace(clearResponse.ProfileName);
        }
        catch (JsonException)
        {
            clearResponse = new EnvClearResponse(string.Empty, 0);
            return false;
        }
    }

    private static SecretPutCliRequest ParseSecretPutArguments(IReadOnlyList<string> args)
    {
        var name = string.Empty;
        var fromFile = string.Empty;
        var ttl = "10m";
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--name", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                name = args[++index];
                continue;
            }

            if (string.Equals(arg, "--from-file", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                fromFile = args[++index];
                continue;
            }

            if (string.Equals(arg, "--ttl", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
            {
                ttl = args[++index];
                continue;
            }

            if (string.IsNullOrWhiteSpace(name) && !arg.StartsWith("-", StringComparison.Ordinal))
            {
                name = arg;
                continue;
            }

            return SecretPutCliRequest.Invalid("Unknown or incomplete secret put option: " + arg);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return SecretPutCliRequest.Invalid("Secret name is required.");
        }

        if (string.IsNullOrWhiteSpace(fromFile))
        {
            return SecretPutCliRequest.Invalid("--from-file is required.");
        }

        if (!TryParseTtlSeconds(ttl, out var ttlSeconds))
        {
            return SecretPutCliRequest.Invalid("TTL must be a positive duration such as 600, 600s, 10m, or 1h.");
        }

        return new SecretPutCliRequest(name.Trim(), fromFile, ttlSeconds, null);
    }

    private static bool TryParseTtlSeconds(string value, out int ttlSeconds)
    {
        ttlSeconds = 0;
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        var suffix = trimmed[^1];
        var numberText = char.IsLetter(suffix) ? trimmed[..^1] : trimmed;
        if (!long.TryParse(numberText, out var number) || number <= 0)
        {
            return false;
        }

        var seconds = char.ToLowerInvariant(suffix) switch
        {
            's' => number,
            'm' => number * 60,
            'h' => number * 60 * 60,
            _ when char.IsDigit(suffix) => number,
            _ => 0,
        };
        if (seconds <= 0 || seconds > int.MaxValue)
        {
            return false;
        }

        ttlSeconds = (int)seconds;
        return ttlSeconds > 0;
    }

    private static async Task RunProfileTrustOperationAsync(
        KelpieMcpServerOptions options,
        string operation,
        string pipeCommand,
        string profileName,
        Func<string, SshProfileTrustOperationResult> offlineOperation)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            Console.Error.WriteLine("Profile name is required.");
            Environment.ExitCode = 1;
            return;
        }

        var normalizedProfileName = profileName.Trim();
        if (!options.ProfileOperations.IsAllowed(operation, "CLI"))
        {
            Console.WriteLine(JsonSerializer.Serialize(new SshProfileTrustOperationResult(
                false,
                normalizedProfileName,
                "disabled-by-config",
                "Profile operation is disabled by kelpiemcp.json.")));
            Environment.ExitCode = 1;
            return;
        }

        var pipeFailure = ControlPipeFailure.None;
        var response = await SendControlCommandWithResponseAsync(
            options.ControlPipeName,
            pipeCommand + " " + normalizedProfileName,
            TimeSpan.FromSeconds(3),
            failure => pipeFailure = failure);
        var result = pipeFailure switch
        {
            ControlPipeFailure.AccessDenied => new SshProfileTrustOperationResult(
                false,
                normalizedProfileName,
                "control-pipe-access-denied",
                "KelpieMCPServer control pipe access was denied. The offline trust store was not changed."),
            ControlPipeFailure.Timeout => new SshProfileTrustOperationResult(
                false,
                normalizedProfileName,
                "control-pipe-timeout",
                "KelpieMCPServer control pipe timed out. The offline trust store was not changed."),
            _ when !string.IsNullOrWhiteSpace(response) => JsonSerializer.Deserialize<SshProfileTrustOperationResult>(response)
                ?? new SshProfileTrustOperationResult(false, normalizedProfileName, "invalid-response", "KelpieMCPServer returned an invalid response."),
            _ => offlineOperation(normalizedProfileName),
        };

        Console.WriteLine(JsonSerializer.Serialize(result));
        if (!result.Success)
        {
            Environment.ExitCode = 1;
        }
    }

    private static SshProfileTrustCapabilities ApplyCliProfileOperationPolicy(
        KelpieMcpServerOptions options,
        SshProfileTrustCapabilities capabilities)
    {
        var addAllowed = capabilities.AddAllowed && options.ProfileOperations.IsAllowed("add", "CLI");
        var reloadAllowed = capabilities.ReloadAllowed && options.ProfileOperations.IsAllowed("reload", "CLI");
        var revokeAllowed = capabilities.RevokeAllowed && options.ProfileOperations.IsAllowed("revoke", "CLI");
        var reason = addAllowed || reloadAllowed || revokeAllowed
            ? capabilities.Reason
            : !options.ProfileOperations.IsAllowed("add", "CLI")
                || !options.ProfileOperations.IsAllowed("reload", "CLI")
                || !options.ProfileOperations.IsAllowed("revoke", "CLI")
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

    private static ReloadingSshConnectionProfileCatalog CreateOfflineCatalog()
    {
        var profilesDirectory = KelpieRuntimePaths.GetProfilesDirectory(AppContext.BaseDirectory);
        var trustStorePath = Path.Combine(
            KelpieRuntimePaths.GetDataDirectory(AppContext.BaseDirectory),
            KelpieRuntimePaths.KelpieMcpTrustStoreFileName);
        return new ReloadingSshConnectionProfileCatalog(profilesDirectory, trustStorePath, []);
    }

    private static string? LoadOpenProfileName()
    {
        try
        {
            _ = KelpieRuntimePaths.MigrateLegacyClientStateFile(AppContext.BaseDirectory);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var statePath = KelpieRuntimePaths.GetClientStatePath(AppContext.BaseDirectory);
        if (!File.Exists(statePath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(statePath));
            return document.RootElement.TryGetProperty("OpenProfile", out var openProfile)
                && openProfile.ValueKind == JsonValueKind.String
                    ? openProfile.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static PingStatus ParsePingResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return new PingStatus(false, null);
        }

        var parts = response.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !string.Equals(parts[0], "pong", StringComparison.OrdinalIgnoreCase))
        {
            return new PingStatus(false, null);
        }

        bool? workingAsWindowsService = null;
        foreach (var part in parts.Skip(1))
        {
            const string prefix = "windowsService=";
            if (part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && bool.TryParse(part[prefix.Length..], out var value))
            {
                workingAsWindowsService = value;
            }
        }

        return new PingStatus(true, workingAsWindowsService);
    }

    private static string FormatYesNo(bool? value)
    {
        return value switch
        {
            true => "yes",
            false => "no",
            null => "unknown",
        };
    }

    private static IReadOnlyCollection<KelpieSessionEntry> DeserializeSessions(string response)
    {
        try
        {
            return JsonSerializer.Deserialize<KelpieSessionEntry[]>(response) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ServerCommand ResolveServerCommand(KelpieMcpServerOptions options)
    {
        var configuredPath = options.ServerExecutablePath;
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullConfiguredPath = Path.GetFullPath(configuredPath);
            KpLog.Debug($"Using configured KelpieMCPServer path: {fullConfiguredPath}");
            return CreateServerCommand(fullConfiguredPath, options.ServerWorkingDirectory);
        }

        foreach (var candidatePath in GetServerPathCandidates())
        {
            if (File.Exists(candidatePath))
            {
                KpLog.Debug($"Resolved KelpieMCPServer path: {candidatePath}");
                return CreateServerCommand(candidatePath, options.ServerWorkingDirectory);
            }
        }

        throw new FileNotFoundException("KelpieMCPServer executable was not found.");
    }

    private static IEnumerable<string> GetServerPathCandidates()
    {
        var baseDirectory = KelpieRuntimePaths.GetBinDirectory(AppContext.BaseDirectory);
        var mcpDirectory = Path.Combine(baseDirectory, "mcp");

        yield return Path.Combine(mcpDirectory, "KelpieMCPServer.exe");
        yield return Path.Combine(mcpDirectory, "KelpieMCPServer.dll");
        yield return Path.Combine(baseDirectory, "KelpieMCPServer.exe");
        yield return Path.Combine(baseDirectory, "KelpieMCPServer.dll");

        var sourceRoot = GetSourceRoot(baseDirectory);
        if (sourceRoot is null)
        {
            yield break;
        }

        yield return Path.Combine(sourceRoot, "KelpieMCPServer", "bin", "Debug", "net8.0", "KelpieMCPServer.exe");
        yield return Path.Combine(sourceRoot, "KelpieMCPServer", "bin", "Debug", "net8.0", "KelpieMCPServer.dll");
    }

    private static string? GetSourceRoot(string baseDirectory)
    {
        var directory = new DirectoryInfo(baseDirectory);
        while (directory is not null)
        {
            if (string.Equals(directory.Name, "src", StringComparison.OrdinalIgnoreCase))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static ServerCommand CreateServerCommand(string serverPath, string? configuredWorkingDirectory)
    {
        var workingDirectory = !string.IsNullOrWhiteSpace(configuredWorkingDirectory)
            ? Path.GetFullPath(configuredWorkingDirectory)
            : KelpieRuntimePaths.GetBinDirectory(AppContext.BaseDirectory);

        if (string.Equals(Path.GetExtension(serverPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ServerCommand("dotnet", Quote(serverPath), workingDirectory);
        }

        return new ServerCommand(serverPath, string.Empty, workingDirectory);
    }

    private static ServerCommand AddServerArguments(ServerCommand serverCommand, IEnumerable<string> arguments)
    {
        var additionalArguments = arguments
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .Select(QuoteWindowsCommandLineArgument)
            .ToArray();
        if (additionalArguments.Length == 0)
        {
            return serverCommand;
        }

        var argumentsText = string.IsNullOrWhiteSpace(serverCommand.Arguments)
            ? string.Join(" ", additionalArguments)
            : serverCommand.Arguments + " " + string.Join(" ", additionalArguments);

        return serverCommand with { Arguments = argumentsText };
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static void StartServerProcess(ServerCommand serverCommand)
    {
        if (OperatingSystem.IsWindows())
        {
            KpLog.Debug("Starting KelpieMCPServer through Windows Start-Process.");
            StartServerProcessOnWindows(serverCommand);
            return;
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = serverCommand.FileName,
            Arguments = serverCommand.Arguments,
            WorkingDirectory = serverCommand.WorkingDirectory,
            UseShellExecute = true,
        };

        Process.Start(processStartInfo);
        KpLog.Debug("KelpieMCPServer process start issued.");
    }

    private static void StartServerProcessOnWindows(ServerCommand serverCommand)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = serverCommand.FileName,
            Arguments = serverCommand.Arguments,
            WorkingDirectory = serverCommand.WorkingDirectory,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start KelpieMCPServer.");
        }

        KpLog.Debug($"KelpieMCPServer process start issued. pid={process.Id}");
    }

    private static string CreateWindowsServiceBinPath(ServerCommand serverCommand, int port)
    {
        var runtimeBase = serverCommand.WorkingDirectory;
        var runtimeBaseArgument = "--runtime-base " + QuoteWindowsCommandLineArgument(runtimeBase);
        var portArgument = " --port " + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var pathOverrideArguments = KelpieRuntimePathOverrideParser
            .ToArguments(KelpieRuntimePaths.Overrides)
            .Select(QuoteWindowsCommandLineArgument)
            .ToArray();
        var pathOverrideText = pathOverrideArguments.Length > 0
            ? " " + string.Join(" ", pathOverrideArguments)
            : string.Empty;

        if (string.Equals(serverCommand.FileName, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return QuoteWindowsCommandLineArgument("dotnet") + " "
                + serverCommand.Arguments + " "
                + runtimeBaseArgument
                + pathOverrideText
                + portArgument;
        }

        return QuoteWindowsCommandLineArgument(serverCommand.FileName) + " " + runtimeBaseArgument + pathOverrideText + portArgument;
    }

    private static string QuoteWindowsCommandLineArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static async Task<bool> WindowsServiceExistsAsync()
    {
        var result = await RunScAsync("query", WindowsServiceName);
        return result.ExitCode == 0;
    }

    private static Task<bool> StartWindowsServiceAndWriteFailureAsync()
    {
        return StartWindowsServiceAndWriteFailureAsync(reloadConfig: false, []);
    }

    private static async Task<bool> StartWindowsServiceAndWriteFailureAsync(
        bool reloadConfig,
        IReadOnlyCollection<string> reloadProfileNames)
    {
        var arguments = new List<string>
        {
            "start",
            WindowsServiceName,
        };
        arguments.AddRange(CreateServerStartArguments(reloadConfig, reloadProfileNames));

        var result = await RunScAsync(arguments.ToArray());
        if (result.ExitCode == 0)
        {
            return true;
        }

        WriteScFailure("Failed to start Windows Service.", result);
        return false;
    }

    private static Task<ScCommandResult> CreateWindowsServiceAsync(string binPath)
    {
        return RunScAsync(
            "create",
            WindowsServiceName,
            "binPath=",
            binPath,
            "start=",
            "auto",
            "DisplayName=",
            WindowsServiceDisplayName);
    }

    private static Task<ScCommandResult> ConfigureWindowsServiceAsync(string binPath)
    {
        return RunScAsync(
            "config",
            WindowsServiceName,
            "binPath=",
            binPath,
            "start=",
            "auto",
            "DisplayName=",
            WindowsServiceDisplayName);
    }

    private static async Task<ScCommandResult> RunScAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sc.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start sc.exe.");
        }

        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ScCommandResult(process.ExitCode, standardOutput, standardError);
    }

    private static void WriteScFailure(string message, ScCommandResult result)
    {
        KpLog.Warn($"{message} scExitCode={result.ExitCode}");
        Console.Error.WriteLine(message);
        if (result.ExitCode == 5)
        {
            Console.Error.WriteLine("Reason: Access denied.");
            Console.Error.WriteLine("Run this command from a terminal running as administrator.");
            Environment.ExitCode = result.ExitCode;
            return;
        }

        Console.Error.WriteLine("Run this command from a terminal running as administrator.");
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            Console.Error.Write(result.StandardOutput);
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            Console.Error.Write(result.StandardError);
        }

        Environment.ExitCode = result.ExitCode == 0 ? 1 : result.ExitCode;
    }

    private static IReadOnlyCollection<string> CreateServerStartArguments(KelpieMcpServerOptions options)
    {
        var arguments = CreateServerStartArguments(options.ReloadConfig, options.ReloadProfileNames).ToList();
        arguments.Add("--port");
        arguments.Add(options.ServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return arguments;
    }

    private static IReadOnlyCollection<string> CreateServerStartArguments(
        bool reloadConfig,
        IReadOnlyCollection<string> reloadProfileNames)
    {
        var arguments = new List<string>();
        if (reloadConfig)
        {
            arguments.Add("--reload-config");
        }

        arguments.AddRange(reloadProfileNames.Select(profileName => "--reload-profile:" + profileName));
        arguments.AddRange(KelpieRuntimePathOverrideParser.ToArguments(KelpieRuntimePaths.Overrides));
        return arguments;
    }

    private enum ControlPipeFailure
    {
        None,
        Unavailable,
        AccessDenied,
        Timeout,
    }

    private sealed record ServerCommand(string FileName, string Arguments, string WorkingDirectory);

    private sealed record ScCommandResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed record PingStatus(bool IsRunning, bool? WorkingAsWindowsService);

    private sealed record KelpieSessionEntry(
        string Handle,
        string ProfileName,
        string SecretName,
        DateTimeOffset StartedAtUtc,
        string Kind);

    private sealed record SecretPutRequest(
        string Name,
        int TtlSeconds);

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

    private sealed record SecretPutCliRequest(
        string Name,
        string FromFile,
        int TtlSeconds,
        string? Error)
    {
        public static SecretPutCliRequest Invalid(string error)
        {
            return new SecretPutCliRequest(string.Empty, string.Empty, 0, error);
        }
    }
}
