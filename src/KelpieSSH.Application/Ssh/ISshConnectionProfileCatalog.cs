namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines access to configured SSH connection profiles.
/// </summary>
public interface ISshConnectionProfileCatalog
{
    /// <summary>
    /// Attempts to get an SSH connection profile by name.
    /// </summary>
    /// <param name="name">The profile name.</param>
    /// <param name="profile">The configured SSH connection profile.</param>
    /// <returns><c>true</c> when the profile exists.</returns>
    bool TryGet(string name, out SshConnectionProfile profile);

    /// <summary>
    /// Lists all configured SSH connection profiles.
    /// </summary>
    /// <returns>The configured SSH connection profiles.</returns>
    IReadOnlyCollection<SshConnectionProfile> List();
}
