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

var options = KelpieMcpServerOptions.FromConfiguration(configuration);
var command = args.Length > 0 ? args[0] : string.Empty;

if (string.Equals(command, "start", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("KelpieServerCommand start requested.");
    await KelpieServerCommandRunner.StartAsync(options);
    return;
}

if (string.Equals(command, "stop", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("KelpieServerCommand stop requested.");
    await KelpieServerCommandRunner.StopAsync(options);
    return;
}

if (string.Equals(command, "status", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("KelpieServerCommand status requested.");
    await KelpieServerCommandRunner.StatusAsync(options);
    return;
}

if (string.Equals(command, "password", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"KelpieServerCommand password requested. profile={profileName}");
    await KelpieServerCommandRunner.PasswordAsync(options, profileName);
    return;
}

if (string.Equals(command, "forget", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"KelpieServerCommand forget requested. profile={profileName}");
    await KelpieServerCommandRunner.ForgetAsync(options, profileName);
    return;
}

if (string.Equals(command, "login", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"KelpieServerCommand login requested. profile={profileName}");
    await KelpieServerCommandRunner.LoginAsync(options, profileName);
    return;
}

if (string.Equals(command, "logout", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"KelpieServerCommand logout requested. profile={profileName}");
    await KelpieServerCommandRunner.LogoutAsync(options, profileName);
    return;
}

ShowUsage(command);
Environment.ExitCode = string.IsNullOrWhiteSpace(command) ? 0 : 1;

static void ShowUsage(string command)
{
    if (!string.IsNullOrWhiteSpace(command))
    {
        Console.Error.WriteLine($"Unknown command: {command}");
    }

    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  kelpiemcp start");
    Console.Error.WriteLine("  kelpiemcp stop");
    Console.Error.WriteLine("  kelpiemcp status");
    Console.Error.WriteLine("  kelpiemcp password <profile>");
    Console.Error.WriteLine("  kelpiemcp forget <profile>");
}
