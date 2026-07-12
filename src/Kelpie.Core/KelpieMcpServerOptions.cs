using Microsoft.Extensions.Configuration;

namespace Kelpie.Core;

/// <summary>
/// Holds settings used to control the Kelpie MCP server body.
/// </summary>
public sealed class KelpieMcpServerOptions
{
    /// <summary>
    /// Gets the NamedPipe name used to send local control commands to the server body.
    /// </summary>
    public required string ControlPipeName { get; init; }

    /// <summary>
    /// Gets the port used by the Streamable HTTP MCP endpoint.
    /// </summary>
    public int ServerPort { get; init; }

    /// <summary>
    /// Gets the optional explicit path to the KelpieMCPServer executable or DLL.
    /// </summary>
    public string? ServerExecutablePath { get; init; }

    /// <summary>
    /// Gets the optional working directory used when launching the server body.
    /// </summary>
    public string? ServerWorkingDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether the MCP server configuration file is explicitly accepted for this server start.
    /// </summary>
    public bool ReloadConfig { get; init; }

    /// <summary>
    /// Gets the profile names explicitly accepted for this server start.
    /// </summary>
    public IReadOnlyCollection<string> ReloadProfileNames { get; init; } = [];

    /// <summary>
    /// Gets profile management operation permissions.
    /// </summary>
    public KelpieProfileOperationsOptions ProfileOperations { get; init; } = KelpieProfileOperationsOptions.Default;

    /// <summary>
    /// Creates server control options from configuration.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The server control options.</returns>
    public static KelpieMcpServerOptions FromConfiguration(IConfiguration configuration)
    {
        var controlPipeName = configuration["Server:ControlPipeName"];
        if (string.IsNullOrWhiteSpace(controlPipeName))
        {
            throw new InvalidOperationException("Server:ControlPipeName is not configured.");
        }

        return new KelpieMcpServerOptions
        {
            ControlPipeName = controlPipeName,
            ServerPort = 45432,
            ServerExecutablePath = configuration["Commands:ExecutablePath"],
            ServerWorkingDirectory = configuration["Commands:WorkingDirectory"],
            ProfileOperations = KelpieProfileOperationsOptions.FromConfiguration(configuration),
        };
    }
}
