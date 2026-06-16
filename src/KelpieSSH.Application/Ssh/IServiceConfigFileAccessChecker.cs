namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Checks configuration file access for one supported service without applying changes.
/// </summary>
public interface IServiceConfigFileAccessChecker
{
    /// <summary>
    /// Checks whether one application configuration file can be read.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="path">The optional full path. When empty, the provider checks its main configuration file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The access check result.</returns>
    Task<ServiceConfigFileAccessCheckResult> CanReadConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string? path = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether one limited edit could be written to an application configuration file.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="path">The full path to check.</param>
    /// <param name="targetKey">The provider-specific target key to edit.</param>
    /// <param name="method">The edit method.</param>
    /// <param name="targetValue">The value or line to write. Delete does not require a value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The access check result.</returns>
    Task<ServiceConfigFileAccessCheckResult> CanWriteConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string path,
        string targetKey,
        string method,
        string? targetValue = null,
        CancellationToken cancellationToken = default);
}
