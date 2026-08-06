using System.Text.Json;
using Microsoft.AspNetCore.Connections;

namespace KelpieMCPServer;

internal static class McpServerStartupFailureFormatter
{
    private const string TrustMismatchPrefix =
        "MCP server configuration hash does not match trusted baseline.";

    public static string Format(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is InvalidOperationException
            && exception.Message.StartsWith(TrustMismatchPrefix, StringComparison.Ordinal))
        {
            return "KelpieMCPServer configuration is not trusted. Restart with --reload-config to accept the current configuration.";
        }

        if (exception is JsonException or InvalidDataException)
        {
            return "KelpieMCPServer configuration is invalid. Run 'kelpie config check' and correct kelpiemcp.json.";
        }

        if (Contains<AddressInUseException>(exception))
        {
            return "KelpieMCPServer endpoint is already in use. Stop the existing server or select another --port.";
        }

        if (Contains<UnauthorizedAccessException>(exception))
        {
            return "KelpieMCPServer access was denied. Verify Kelpie home permissions and that another server instance is not using the control pipe.";
        }

        return "KelpieMCPServer failed to start. Check the Kelpie logs for details.";
    }

    private static bool Contains<TException>(Exception exception)
        where TException : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TException)
            {
                return true;
            }
        }

        return false;
    }
}
