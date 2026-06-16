namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Tests configuration files for one supported service.
/// </summary>
public interface IServiceConfigFileTester
{
    /// <summary>
    /// Tests one application's configuration files.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The file test result.</returns>
    Task<ServiceConfigFileTestResult> TestConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        CancellationToken cancellationToken = default);
}
