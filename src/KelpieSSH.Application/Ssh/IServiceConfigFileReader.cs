namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Reads configuration files for one supported service.
/// </summary>
public interface IServiceConfigFileReader
{
    /// <summary>
    /// Reads one application configuration file.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="path">The optional full path. When empty, the provider reads its main configuration file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The file read result.</returns>
    Task<ServiceConfigFileReadResult> ReadConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string? path = null,
        CancellationToken cancellationToken = default);
}
