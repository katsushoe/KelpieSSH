using KelpieServerCommand;
using Kelpie.Core;
using Microsoft.Extensions.Configuration;

KpLogSetup.Configure(
    AppContext.BaseDirectory,
    "kelpiemcp.log",
    KelpieRuntimePaths.KelpieMcpConfigFileName,
    "kelpiemcp");
KpLog.Info("KelpieServerCommand starting.");

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile(
        KelpieRuntimePaths.GetConfigFilePath(AppContext.BaseDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName),
        optional: true,
        reloadOnChange: false)
    .Build();

var command = args.Length > 0 ? args[0] : string.Empty;

if (string.Equals(command, "start", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("KelpieServerCommand start requested.");
    var options = CreateStartOptions(
        configuration,
        ParseReloadConfig(args.Skip(1)),
        ParseReloadProfileNames(args.Skip(1)));
    await KelpieServerCommandRunner.StartAsync(options);
    return;
}

if (string.Equals(command, "stop", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("KelpieServerCommand stop requested.");
    var options = CreateOptions(configuration);
    await KelpieServerCommandRunner.StopAsync(options);
    return;
}

if (string.Equals(command, "status", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("KelpieServerCommand status requested.");
    var options = CreateOptions(configuration);
    await KelpieServerCommandRunner.StatusAsync(options);
    return;
}

if (string.Equals(command, "service", StringComparison.OrdinalIgnoreCase))
{
    var serviceCommand = args.Length > 1 ? args[1] : string.Empty;
    if (string.Equals(serviceCommand, "register", StringComparison.OrdinalIgnoreCase))
    {
        KpLog.Info("KelpieServerCommand service register requested.");
        var options = CreateOptions(configuration);
        await KelpieServerCommandRunner.RegisterServiceAsync(options);
        return;
    }

    if (string.Equals(serviceCommand, "unregister", StringComparison.OrdinalIgnoreCase))
    {
        KpLog.Info("KelpieServerCommand service unregister requested.");
        await KelpieServerCommandRunner.UnregisterServiceAsync();
        return;
    }

    if (string.Equals(serviceCommand, "status", StringComparison.OrdinalIgnoreCase))
    {
        KpLog.Info("KelpieServerCommand service status requested.");
        await KelpieServerCommandRunner.ServiceStatusAsync();
        return;
    }

    ShowServiceUsage(serviceCommand);
    Environment.ExitCode = string.IsNullOrWhiteSpace(serviceCommand) ? 0 : 1;
    return;
}

if (string.Equals(command, "password", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"KelpieServerCommand password requested. profile={profileName}");
    var options = CreateOptions(configuration);
    await KelpieServerCommandRunner.PasswordAsync(options, profileName);
    return;
}

if (string.Equals(command, "forget", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"KelpieServerCommand forget requested. profile={profileName}");
    var options = CreateOptions(configuration);
    await KelpieServerCommandRunner.ForgetAsync(options, profileName);
    return;
}

if (string.Equals(command, "login", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"KelpieServerCommand login requested. profile={profileName}");
    var options = CreateOptions(configuration);
    await KelpieServerCommandRunner.LoginAsync(options, profileName);
    return;
}

if (string.Equals(command, "logout", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"KelpieServerCommand logout requested. profile={profileName}");
    var options = CreateOptions(configuration);
    await KelpieServerCommandRunner.LogoutAsync(options, profileName);
    return;
}

ShowUsage(command);
Environment.ExitCode = string.IsNullOrWhiteSpace(command) ? 0 : 1;

static KelpieMcpServerOptions CreateOptions(IConfiguration configuration)
{
    return KelpieMcpServerOptions.FromConfiguration(configuration);
}

static KelpieMcpServerOptions CreateStartOptions(
    IConfiguration configuration,
    bool reloadConfig,
    IReadOnlyCollection<string> reloadProfileNames)
{
    var options = KelpieMcpServerOptions.FromConfiguration(configuration);
    return new KelpieMcpServerOptions
    {
        ControlPipeName = options.ControlPipeName,
        ServerPort = options.ServerPort,
        ServerExecutablePath = options.ServerExecutablePath,
        ServerWorkingDirectory = options.ServerWorkingDirectory,
        ReloadConfig = reloadConfig,
        ReloadProfileNames = reloadProfileNames,
    };
}

static bool ParseReloadConfig(IEnumerable<string> args)
{
    return args.Any(arg => string.Equals(arg, "--reload-config", StringComparison.OrdinalIgnoreCase));
}

static IReadOnlyCollection<string> ParseReloadProfileNames(IEnumerable<string> args)
{
    var reloadProfileNames = new List<string>();
    const string prefix = "--reload-profile:";
    foreach (var arg in args)
    {
        if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var profileName = arg[prefix.Length..].Trim();
        if (!string.IsNullOrWhiteSpace(profileName))
        {
            reloadProfileNames.Add(profileName);
        }
    }

    return reloadProfileNames
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

static void ShowUsage(string command)
{
    if (!string.IsNullOrWhiteSpace(command))
    {
        Console.Error.WriteLine($"Unknown command: {command}");
    }

    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  kelpiemcp start [--reload-config] [--reload-profile:<profile>]");
    Console.Error.WriteLine("  kelpiemcp stop");
    Console.Error.WriteLine("  kelpiemcp status");
    Console.Error.WriteLine("  kelpiemcp service register");
    Console.Error.WriteLine("  kelpiemcp service unregister");
    Console.Error.WriteLine("  kelpiemcp service status");
    Console.Error.WriteLine("  kelpiemcp password <profile>");
    Console.Error.WriteLine("  kelpiemcp forget <profile>");
}

static void ShowServiceUsage(string serviceCommand)
{
    if (!string.IsNullOrWhiteSpace(serviceCommand))
    {
        Console.Error.WriteLine($"Unknown service command: {serviceCommand}");
    }

    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  kelpiemcp service register");
    Console.Error.WriteLine("  kelpiemcp service unregister");
    Console.Error.WriteLine("  kelpiemcp service status");
}
