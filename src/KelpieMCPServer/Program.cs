using KelpieMCPServer;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (!KelpieRuntimePathOverrideParser.TryParse(args, out var commandArgs, out var runtimePathOverrides, out var runtimePathError))
{
    Console.Error.WriteLine(runtimePathError);
    Environment.ExitCode = 2;
    return;
}

KelpieRuntimePaths.SetOverrides(runtimePathOverrides);
args = commandArgs;

if (McpServerCliOptions.IsHelpRequested(args))
{
    Console.WriteLine(McpServerCliOptions.HelpText);
    return;
}

McpServerStartupOptions startupOptions;
try
{
    startupOptions = McpServerCliOptions.ParseStartupOptions(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Use --help for usage information.");
    Environment.ExitCode = 2;
    return;
}

var runtimeBaseDirectory = ResolveRuntimeBaseDirectory(args);
KpLogSetup.Configure(
    runtimeBaseDirectory,
    "kelpiemcp.log",
    KelpieRuntimePaths.KelpieMcpConfigFileName,
    "kelpiemcp");
KpLog.Info("KelpieMCPServer starting.");

try
{
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "KelpieMCPServer";
});

builder.Configuration.Sources.Clear();
var configFilePath = KelpieRuntimePaths.GetConfigFilePath(runtimeBaseDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName);
var trustStorePath = Path.Combine(KelpieRuntimePaths.GetDataDirectory(runtimeBaseDirectory), KelpieRuntimePaths.KelpieMcpTrustStoreFileName);
VerifyConfigurationTrust(configFilePath, trustStorePath, ResolveReloadConfig(args));
builder.Configuration.AddJsonFile(
    configFilePath,
    optional: true,
    reloadOnChange: false);

var controlPipeName = builder.Configuration["Server:ControlPipeName"];

if (string.IsNullOrWhiteSpace(controlPipeName))
{
    KpLog.Err("Server:ControlPipeName is not configured.");
    Console.Error.WriteLine("Server:ControlPipeName is not configured.");
    Environment.ExitCode = 2;
    return;
}

var serverUrl = startupOptions.ServerUrl;
var profilesDirectory = KelpieRuntimePaths.GetProfilesDirectory(runtimeBaseDirectory);
var reloadProfileNames = ResolveReloadProfileNames(args);

builder.WebHost.UseUrls(serverUrl);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(new KelpieServerControlOptions(controlPipeName));
builder.Services.AddSingleton(startupOptions);
builder.Services.AddSingleton(KelpieProfileOperationsOptions.FromConfiguration(builder.Configuration));
var profileCatalog = new ReloadingSshConnectionProfileCatalog(
    profilesDirectory,
    trustStorePath,
    reloadProfileNames);
foreach (var error in profileCatalog.ProfileLoadErrors)
{
    KpLog.Warn($"SSH profile load error. profile={error.ProfileName}, reason={error.Reason}, message={error.Message}");
}

builder.Services.AddSingleton(profileCatalog);
builder.Services.AddSingleton<ISshConnectionProfileCatalog>(serviceProvider =>
    serviceProvider.GetRequiredService<ReloadingSshConnectionProfileCatalog>());
builder.Services.AddSingleton(CommandProcessingProviderCatalog.CreateDefault());
builder.Services.AddSingleton(ServiceConfigPathsProviderCatalog.CreateDefault());
builder.Services.AddSingleton<IWebPublicFileProvider, WebPublicFileProvider>();
builder.Services.AddSingleton<IKelpieSecretStore, InMemoryKelpieSecretStore>();
builder.Services.AddSingleton<IKelpieEnvironmentOverrideStore, InMemoryKelpieEnvironmentOverrideStore>();
builder.Services.AddSingleton<ISshPasswordSessionStore, InMemorySshPasswordSessionStore>();
builder.Services.AddSingleton<ISshPasswordProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<ISshPasswordSessionStore>());
builder.Services.AddSingleton<SshTerminalSessionManager>();
builder.Services.AddSingleton<TargetInventoryCache>();
builder.Services.AddSingleton<ISshCommandRunner, SshNetCommandRunner>();
builder.Services.AddSingleton<ISshFileExporter>(serviceProvider => new SshNetFileExporter(
    serviceProvider.GetRequiredService<ISshPasswordProvider>(),
    Path.Combine(KelpieRuntimePaths.GetDataDirectory(runtimeBaseDirectory), "exports")));
builder.Services.AddSingleton(serviceProvider => new SshCommandService(
    serviceProvider.GetRequiredService<IReadOnlyCollection<ICommandProcessingProvider>>(),
    serviceProvider.GetRequiredService<ISshCommandRunner>(),
    serviceProvider.GetRequiredService<IKelpieEnvironmentOverrideStore>()));
builder.Services.AddHostedService<NamedPipeShutdownService>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "KelpieSSH",
            Version = "0.3.9.1",
        };
    })
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapGet("/", () => Results.Text(
    "KelpieMCPServer is running.\nMCP endpoint: /mcp\nHealth endpoint: /health\n",
    "text/plain"));
app.MapGet("/health", () => Results.Json(new
{
    Status = "ok",
    Name = "KelpieMCPServer",
    McpEndpoint = "/mcp",
}));
app.MapMcp("/mcp");

app.Lifetime.ApplicationStarted.Register(() =>
{
    KpLog.Info("KelpieMCPServer has started.");
    KpLog.Info($"KelpieMCPServer listening on {serverUrl}/mcp.");
});
app.Lifetime.ApplicationStopping.Register(() => KpLog.Info("KelpieMCPServer stopping."));
app.Lifetime.ApplicationStopped.Register(() =>
{
    KpLog.Info("KelpieMCPServer stopped.");
    KpLog.Flush();
});

await app.RunAsync();
}
catch (Exception ex)
{
    KpLog.Err("KelpieMCPServer startup failed.", ex);
    Console.Error.WriteLine("KelpieMCPServer startup failed: " + ex.Message);
    Environment.ExitCode = 1;
}
finally
{
    KpLog.Flush();
}

static string ResolveRuntimeBaseDirectory(string[] args)
{
    for (var index = 0; index < args.Length; index++)
    {
        var arg = args[index];
        if (string.Equals(arg, "--runtime-base", StringComparison.OrdinalIgnoreCase)
            && index + 1 < args.Length
            && !string.IsNullOrWhiteSpace(args[index + 1]))
        {
            return Path.GetFullPath(args[index + 1]);
        }

        const string prefix = "--runtime-base=";
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var value = arg[prefix.Length..];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return Path.GetFullPath(value);
            }
        }
    }

    return Environment.CurrentDirectory;
}

static bool ResolveReloadConfig(string[] args)
{
    return args.Any(arg => string.Equals(arg, "--reload-config", StringComparison.OrdinalIgnoreCase));
}

static IReadOnlyCollection<string> ResolveReloadProfileNames(string[] args)
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

static void VerifyConfigurationTrust(string configFilePath, string trustStorePath, bool reloadConfig)
{
    if (!File.Exists(configFilePath))
    {
        return;
    }

    var trustStore = SshProfileTrustStore.Load(trustStorePath);
    var trustStoreChanged = VerifyCreatorPathHash(trustStore);
    var currentHash = SshProfileTrustStore.ComputeFileHash(configFilePath);
    if (reloadConfig || !trustStore.TryGetConfigHash(out var trustedHash))
    {
        trustStore.SetConfigHash(currentHash);
        trustStore.Save(trustStorePath);
        return;
    }

    if (!string.Equals(currentHash, trustedHash, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "MCP server configuration hash does not match trusted baseline. Start with --reload-config to accept the current configuration.");
    }

    if (trustStoreChanged)
    {
        trustStore.Save(trustStorePath);
    }
}

static bool VerifyCreatorPathHash(SshProfileTrustStore trustStore)
{
    var executablePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
    var currentCreatorPathHash = SshProfileTrustStore.ComputePathHash(executablePath);
    if (!trustStore.TryGetCreatorPathHash(out var trustedCreatorPathHash))
    {
        trustStore.SetCreatorPathHashIfMissing(currentCreatorPathHash);
        return true;
    }

    if (!string.Equals(currentCreatorPathHash, trustedCreatorPathHash, StringComparison.OrdinalIgnoreCase))
    {
        KpLog.Warn("MCP trust store creator path hash differs from current executable path hash.");
    }

    return false;
}
