namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Stores temporary SSH passwords for the current Kelpie server session.
/// </summary>
public interface ISshPasswordSessionStore : ISshPasswordProvider
{
    /// <summary>
    /// Stores or replaces a password for a secret name.
    /// </summary>
    /// <param name="secretName">The configured password secret name.</param>
    /// <param name="password">The password value.</param>
    void SetPassword(string secretName, string password);

    /// <summary>
    /// Stores or replaces a password for a profile session.
    /// </summary>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="secretName">The configured password secret name.</param>
    /// <param name="password">The password value.</param>
    void SetPasswordSession(string profileName, string secretName, string password);

    /// <summary>
    /// Opens an interactive SSH session placeholder for a profile.
    /// </summary>
    /// <param name="profileName">The SSH profile name.</param>
    /// <returns>The opened session information.</returns>
    SshSessionInfo OpenInteractiveSession(string profileName);

    /// <summary>
    /// Clears a password for a secret name.
    /// </summary>
    /// <param name="secretName">The configured password secret name.</param>
    /// <returns><c>true</c> when a password was removed.</returns>
    bool ClearPassword(string secretName);

    /// <summary>
    /// Clears a temporary SSH session by its handle.
    /// </summary>
    /// <param name="handle">The session handle.</param>
    /// <returns><c>true</c> when a session was removed.</returns>
    bool ClearSession(string handle);

    /// <summary>
    /// Lists temporary SSH sessions held by the current server process.
    /// </summary>
    /// <returns>The active temporary SSH sessions.</returns>
    IReadOnlyCollection<SshSessionInfo> ListSessions();
}
