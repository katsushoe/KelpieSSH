namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Writes configuration files for one supported service.
/// </summary>
public interface IServiceConfigFileWriter
{
    /// <summary>
    /// Applies a limited edit to one application configuration file.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="path">The full path to write.</param>
    /// <param name="targetKey">The provider-specific target key to edit.</param>
    /// <param name="method">The edit method.</param>
    /// <param name="targetValue">The value or line to write. Delete does not require a value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The file write result.</returns>
    Task<ServiceConfigFileWriteResult> WriteConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string path,
        string targetKey,
        string method,
        string? targetValue = null,
        CancellationToken cancellationToken = default);
}
