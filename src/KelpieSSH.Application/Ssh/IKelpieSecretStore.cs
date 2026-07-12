namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Stores short-lived secret payloads for MCP operations without exposing their values.
/// </summary>
public interface IKelpieSecretStore
{
    /// <summary>
    /// Stores or replaces one secret payload.
    /// </summary>
    KelpieSecretInfo Put(string name, byte[] content, TimeSpan ttl);

    /// <summary>
    /// Tries to resolve one secret payload as Base64.
    /// </summary>
    bool TryGetContentBase64(string name, out string contentBase64, out KelpieSecretInfo? info);

    /// <summary>
    /// Lists non-expired secret metadata.
    /// </summary>
    IReadOnlyCollection<KelpieSecretInfo> List();

    /// <summary>
    /// Removes one secret payload.
    /// </summary>
    bool Forget(string name);
}
