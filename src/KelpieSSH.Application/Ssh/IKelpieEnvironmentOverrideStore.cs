namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Stores short-lived environment overrides for a running MCP server session.
/// </summary>
public interface IKelpieEnvironmentOverrideStore
{
    /// <summary>
    /// Stores one environment value override.
    /// </summary>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="key">The environment variable key.</param>
    /// <param name="value">The environment variable value.</param>
    /// <returns>The stored override metadata.</returns>
    KelpieEnvironmentOverrideInfo Put(string profileName, string key, string value);

    /// <summary>
    /// Lists stored environment override metadata.
    /// </summary>
    /// <param name="profileName">The optional SSH profile name filter.</param>
    /// <returns>The stored override metadata.</returns>
    IReadOnlyCollection<KelpieEnvironmentOverrideInfo> List(string? profileName = null);

    /// <summary>
    /// Removes one environment override.
    /// </summary>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="key">The environment variable key.</param>
    /// <returns><see langword="true"/> when an override was removed.</returns>
    bool Forget(string profileName, string key);

    /// <summary>
    /// Removes every environment override for one profile.
    /// </summary>
    /// <param name="profileName">The SSH profile name.</param>
    /// <returns>The number of removed overrides.</returns>
    int Clear(string profileName);

    /// <summary>
    /// Gets environment override values for command execution.
    /// </summary>
    /// <param name="profileName">The SSH profile name.</param>
    /// <returns>The environment override values.</returns>
    IReadOnlyDictionary<string, string> GetValues(string profileName);
}
