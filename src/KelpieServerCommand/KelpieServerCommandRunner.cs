using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Kelpie.Core;

namespace KelpieServerCommand;

/// <summary>
/// Implements the kelpiemcp frontend commands.
/// </summary>
public static class KelpieServerCommandRunner
{
    private static readonly TimeSpan PipeConnectionTimeout = TimeSpan.FromMilliseconds(300);
    private const string WindowsServiceName = "KelpieMCPServer";
    private const string WindowsServiceDisplayName = "KelpieSSH MCP Server";
    private const string WindowsServiceDescription = "Provides the local KelpieSSH MCP server endpoint for AI clients.";

    /// <summary>
    /// Starts the Kelpie MCP server body if it is not already running.
    /// </summary>
    /// <param name="options">The command options.</param>
    public static async Task StartAsync(KelpieMcpServerOptions options)
    {
        if (await SendControlCommandAsync(options.ControlPipeName, "ping", PipeConnectionTimeout))
        {
            KpLog.Info("KelpieMCPServer is already running.");
            Console.WriteLine("KelpieMCPServer is already running.");
            return;
        }

        var serverCommand = ResolveServerCommand(options);
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
    public static async Task StatusAsync(KelpieMcpServerOptions options)
    {
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
            Console.WriteLine($"Working as Windows service: {FormatYesNo(status.WorkingAsWindowsService)}");
            return;
        }

        KpLog.Info("KelpieMCPServer status: stopped.");
        Console.WriteLine("KelpieMCPServer: stopped");
        Console.WriteLine("Working as Windows service: no");
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
        var binPath = CreateWindowsServiceBinPath(serverCommand);
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

            var writer = new StreamWriter(pipe)
            {
                AutoFlush = true,
            };
            await writer.WriteLineAsync(command);
            await writer.FlushAsync(cancellationTokenSource.Token);

            using var reader = new StreamReader(pipe);
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

            var writer = new StreamWriter(pipe)
            {
                AutoFlush = true,
            };
            var reader = new StreamReader(pipe);

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
        var baseDirectory = AppContext.BaseDirectory;
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
            : AppContext.BaseDirectory;

        if (string.Equals(Path.GetExtension(serverPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new ServerCommand("dotnet", Quote(serverPath), workingDirectory);
        }

        return new ServerCommand(serverPath, string.Empty, workingDirectory);
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

    private static string CreateWindowsServiceBinPath(ServerCommand serverCommand)
    {
        var runtimeBase = serverCommand.WorkingDirectory;
        var runtimeBaseArgument = "--runtime-base " + QuoteWindowsCommandLineArgument(runtimeBase);

        if (string.Equals(serverCommand.FileName, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return QuoteWindowsCommandLineArgument("dotnet") + " "
                + serverCommand.Arguments + " "
                + runtimeBaseArgument;
        }

        return QuoteWindowsCommandLineArgument(serverCommand.FileName) + " " + runtimeBaseArgument;
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
        Console.Error.WriteLine("Run this command from an elevated terminal.");
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

    private sealed record ServerCommand(string FileName, string Arguments, string WorkingDirectory);

    private sealed record ScCommandResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed record PingStatus(bool IsRunning, bool? WorkingAsWindowsService);

    private sealed record KelpieSessionEntry(
        string Handle,
        string ProfileName,
        string SecretName,
        DateTimeOffset StartedAtUtc,
        string Kind);
}
