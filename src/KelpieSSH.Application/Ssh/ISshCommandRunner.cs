namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Executes a validated SSH command request.
/// </summary>
public interface ISshCommandRunner
{
    /// <summary>
    /// Executes the SSH command.
    /// </summary>
    /// <param name="request">The validated command request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    Task<SshCommandResult> ExecuteAsync(SshCommandRequest request, CancellationToken cancellationToken = default);
}
