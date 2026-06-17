using Microsoft.Extensions.Configuration;

namespace Kelpie.Core;

/// <summary>
/// Holds profile management operation permissions by caller channel.
/// </summary>
public sealed class KelpieProfileOperationsOptions
{
    /// <summary>
    /// Gets the default profile operation permissions.
    /// </summary>
    public static KelpieProfileOperationsOptions Default { get; } = new(
        addCliAllowed: true,
        addMcpAllowed: false,
        reloadCliAllowed: true,
        reloadMcpAllowed: false,
        revokeCliAllowed: true,
        revokeMcpAllowed: false);

    /// <summary>
    /// Initializes a new instance of the <see cref="KelpieProfileOperationsOptions"/> class.
    /// </summary>
    public KelpieProfileOperationsOptions(
        bool addCliAllowed,
        bool addMcpAllowed,
        bool reloadCliAllowed,
        bool reloadMcpAllowed,
        bool revokeCliAllowed,
        bool revokeMcpAllowed)
    {
        AddCliAllowed = addCliAllowed;
        AddMcpAllowed = addMcpAllowed;
        ReloadCliAllowed = reloadCliAllowed;
        ReloadMcpAllowed = reloadMcpAllowed;
        RevokeCliAllowed = revokeCliAllowed;
        RevokeMcpAllowed = revokeMcpAllowed;
    }

    /// <summary>
    /// Gets a value indicating whether CLI profile add is allowed.
    /// </summary>
    public bool AddCliAllowed { get; }

    /// <summary>
    /// Gets a value indicating whether MCP profile add is allowed.
    /// </summary>
    public bool AddMcpAllowed { get; }

    /// <summary>
    /// Gets a value indicating whether CLI profile reload is allowed.
    /// </summary>
    public bool ReloadCliAllowed { get; }

    /// <summary>
    /// Gets a value indicating whether MCP profile reload is allowed.
    /// </summary>
    public bool ReloadMcpAllowed { get; }

    /// <summary>
    /// Gets a value indicating whether CLI profile revoke is allowed.
    /// </summary>
    public bool RevokeCliAllowed { get; }

    /// <summary>
    /// Gets a value indicating whether MCP profile revoke is allowed.
    /// </summary>
    public bool RevokeMcpAllowed { get; }

    /// <summary>
    /// Creates profile operation permissions from configuration.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The profile operation permissions.</returns>
    public static KelpieProfileOperationsOptions FromConfiguration(IConfiguration configuration)
    {
        return new KelpieProfileOperationsOptions(
            ReadPermission(configuration, "Add", "CLI", Default.AddCliAllowed),
            ReadPermission(configuration, "Add", "MCP", Default.AddMcpAllowed),
            ReadPermission(configuration, "Reload", "CLI", Default.ReloadCliAllowed),
            ReadPermission(configuration, "Reload", "MCP", Default.ReloadMcpAllowed),
            ReadPermission(configuration, "Revoke", "CLI", Default.RevokeCliAllowed),
            ReadPermission(configuration, "Revoke", "MCP", Default.RevokeMcpAllowed));
    }

    /// <summary>
    /// Returns whether the specified operation is allowed for the specified channel.
    /// </summary>
    /// <param name="operation">The profile operation name.</param>
    /// <param name="channel">The caller channel name.</param>
    /// <returns><c>true</c> when the operation is allowed.</returns>
    public bool IsAllowed(string operation, string channel)
    {
        return Normalize(operation) switch
        {
            "add" => IsCli(channel) ? AddCliAllowed : AddMcpAllowed,
            "reload" => IsCli(channel) ? ReloadCliAllowed : ReloadMcpAllowed,
            "revoke" => IsCli(channel) ? RevokeCliAllowed : RevokeMcpAllowed,
            _ => false,
        };
    }

    private static bool ReadPermission(
        IConfiguration configuration,
        string operation,
        string channel,
        bool defaultValue)
    {
        var value = configuration[$"ProfileOperations:{operation}:{channel}"];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        return Normalize(value) switch
        {
            "allowed" or "allow" => true,
            "deny" or "denied" => false,
            _ => false,
        };
    }

    private static bool IsCli(string channel)
    {
        return string.Equals(channel, "CLI", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
