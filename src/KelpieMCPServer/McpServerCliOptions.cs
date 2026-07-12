using System.Globalization;

namespace KelpieMCPServer;

/// <summary>
/// Parses command-line options for the MCP server executable.
/// </summary>
public static class McpServerCliOptions
{
    /// <summary>
    /// Gets the command-line help text.
    /// </summary>
    public const string HelpText = """
        Usage: KelpieMCPServer [options]

        Options:
          --port <port-number>  Public HTTP port (1-65535). Default: 45432.
          --runtime-base <path> Runtime base directory.
          --help                Show this help.

        Server.Port in kelpiemcp.json is not used to select the public port.
        """;

    /// <summary>
    /// Parses and validates the public port from command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The validated startup options.</returns>
    public static McpServerStartupOptions ParseStartupOptions(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var port = McpServerStartupOptions.DefaultPort;
        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--port", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException("The --port option requires a value.");
                }

                port = ParsePort(args[++index]);
                continue;
            }

            const string prefix = "--port=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = arg[prefix.Length..];
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("The --port option requires a value.");
                }

                port = ParsePort(value);
            }
        }

        return new McpServerStartupOptions(port);
    }

    /// <summary>
    /// Returns whether command-line help was requested.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns><c>true</c> when help was requested.</returns>
    public static bool IsHelpRequested(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase));
    }

    private static int ParsePort(string value)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var port))
        {
            throw new ArgumentException("The --port value must be a number.");
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(null, value, "The --port value must be between 1 and 65535.");
        }

        return port;
    }
}
