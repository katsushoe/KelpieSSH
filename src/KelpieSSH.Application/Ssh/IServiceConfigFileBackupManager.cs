namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Manages backup state for one supported service's configuration files.
/// </summary>
public interface IServiceConfigFileBackupManager
{
    /// <summary>
    /// Restores a configuration file from its Kelpie backup and removes the backup.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="path">The full path to restore.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The rollback result.</returns>
    Task<ServiceConfigFileBackupActionResult> RollbackConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits a configuration file edit by removing its Kelpie backup.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="path">The full path to commit.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The commit result.</returns>
    Task<ServiceConfigFileBackupActionResult> CommitConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string path,
        CancellationToken cancellationToken = default);
}
