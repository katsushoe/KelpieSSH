namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Discovers configuration file paths for one supported service.
/// </summary>
public interface IServiceConfigPathsProvider
{
    /// <summary>
    /// Gets the stable service key.
    /// </summary>
    string ServiceKey { get; }

    /// <summary>
    /// Gets the human-readable application name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets configuration file paths for the application.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The discovered configuration paths.</returns>
    Task<ServiceConfigPathsResult> GetConfigPathsAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        CancellationToken cancellationToken = default);
}
