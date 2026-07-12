using System.Globalization;

namespace KelpieMCPServer;

/// <summary>
/// Defines validated startup options for the MCP server host.
/// </summary>
public sealed record McpServerStartupOptions
{
    /// <summary>
    /// Gets the default public HTTP port.
    /// </summary>
    public const int DefaultPort = 45432;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServerStartupOptions"/> class.
    /// </summary>
    /// <param name="port">The public HTTP port.</param>
    public McpServerStartupOptions(int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535.");
        }

        Port = port;
    }

    /// <summary>
    /// Gets the public HTTP port.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Gets the loopback URL used by the MCP server host.
    /// </summary>
    public string ServerUrl => $"http://127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}";
}
