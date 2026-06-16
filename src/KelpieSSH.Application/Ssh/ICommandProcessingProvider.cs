namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides allowed SSH command definitions for a command-processing OS family.
/// </summary>
public interface ICommandProcessingProvider
{
    /// <summary>
    /// Gets the OS families handled by this provider.
    /// </summary>
    IReadOnlyCollection<string> OsFamilies { get; }

    /// <summary>
    /// Determines whether this provider can supply commands for the profile.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <returns><c>true</c> when the provider supports the profile.</returns>
    bool Supports(SshConnectionProfile profile);

    /// <summary>
    /// Gets the allowed command definitions for the profile.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <returns>The allowed command definitions.</returns>
    IReadOnlyCollection<AllowedCommandDefinition> GetCommands(SshConnectionProfile profile);
}
