namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Reads log files for one supported service.
/// </summary>
public interface IServiceLogfileReader
{
    /// <summary>
    /// Reads one application log file.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="logKey">The provider-defined log key.</param>
    /// <param name="sinceMinutes">The optional recent window in minutes.</param>
    /// <param name="lines">The maximum number of log lines to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The log file read result.</returns>
    Task<ServiceLogfileReadResult> ReadLogfileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string logKey,
        int? sinceMinutes = null,
        int lines = 500,
        CancellationToken cancellationToken = default);
}
