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

        var configuredPort = int.TryParse(configuration["Server:Port"], out var port)
            ? port
            : 45432;

        return new KelpieMcpServerOptions
        {
            ControlPipeName = controlPipeName,
            ServerPort = configuredPort,
            ServerExecutablePath = configuration["Commands:ExecutablePath"],
            ServerWorkingDirectory = configuration["Commands:WorkingDirectory"],
        };
    }
}
