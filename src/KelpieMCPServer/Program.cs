using KelpieMCPServer;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
builder.Configuration.AddJsonFile(
    KelpieRuntimePaths.GetConfigFilePath(runtimeBaseDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName),
    optional: true,
    reloadOnChange: false);

var configuredPort = builder.Configuration.GetValue<int?>("Server:Port");
var controlPipeName = builder.Configuration["Server:ControlPipeName"];

if (configuredPort is null or <= 0 or > 65535)
{
    KpLog.Err("Server:Port is not configured or invalid.");
    Console.Error.WriteLine("Server:Port is not configured or invalid.");
    Environment.ExitCode = 2;
    return;
}

if (string.IsNullOrWhiteSpace(controlPipeName))
{
    KpLog.Err("Server:ControlPipeName is not configured.");
    Console.Error.WriteLine("Server:ControlPipeName is not configured.");
    Environment.ExitCode = 2;
    return;
}

var serverUrl = $"http://127.0.0.1:{configuredPort.Value}";
var profilesDirectory = KelpieRuntimePaths.GetProfilesDirectory(runtimeBaseDirectory);

builder.WebHost.UseUrls(serverUrl);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton(new KelpieServerControlOptions(controlPipeName));
builder.Services.AddSingleton<ISshConnectionProfileCatalog>(
    new SshConnectionProfileCatalog(
        SshConnectionProfileFileLoader.LoadDirectory(profilesDirectory)));
builder.Services.AddSingleton(CommandProcessingProviderCatalog.CreateDefault());
builder.Services.AddSingleton(ServiceConfigPathsProviderCatalog.CreateDefault());
builder.Services.AddSingleton<IWebPublicFileProvider, WebPublicFileProvider>();
builder.Services.AddSingleton<ISshPasswordSessionStore, InMemorySshPasswordSessionStore>();
builder.Services.AddSingleton<ISshPasswordProvider>(serviceProvider =>
    serviceProvider.GetRequiredService<ISshPasswordSessionStore>());
builder.Services.AddSingleton<SshTerminalSessionManager>();
builder.Services.AddSingleton<ISshCommandRunner, SshNetCommandRunner>();
builder.Services.AddSingleton(serviceProvider => new SshCommandService(
    serviceProvider.GetRequiredService<IReadOnlyCollection<ICommandProcessingProvider>>(),
    serviceProvider.GetRequiredService<ISshCommandRunner>()));
builder.Services.AddHostedService<NamedPipeShutdownService>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "KelpieSSH",
            Version = "0.1.30.0",
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
