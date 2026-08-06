namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a sanitized SSH connection failure shared by command and interactive paths.
/// </summary>
public class SshConnectionFailureException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SshConnectionFailureException"/> class.
    /// </summary>
    /// <param name="failureKind">The sanitized failure category.</param>
    /// <param name="innerException">The infrastructure exception.</param>
    public SshConnectionFailureException(
        SshConnectionFailureKind failureKind,
        Exception? innerException = null)
        : this(CreateMessage(failureKind), failureKind, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshConnectionFailureException"/> class.
    /// </summary>
    /// <param name="message">The sanitized failure message.</param>
    /// <param name="failureKind">The sanitized failure category.</param>
    /// <param name="innerException">The infrastructure exception.</param>
    protected SshConnectionFailureException(
        string message,
        SshConnectionFailureKind failureKind,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }

    /// <summary>
    /// Gets a value indicating whether the connection attempt timed out.
    /// </summary>
    public bool TimedOut => FailureKind == SshConnectionFailureKind.Timeout;

    /// <summary>
    /// Gets the sanitized connection failure category.
    /// </summary>
    public SshConnectionFailureKind FailureKind { get; }

    private static string CreateMessage(SshConnectionFailureKind failureKind)
    {
        return failureKind switch
        {
            SshConnectionFailureKind.AuthenticationFailed => "SSH authentication failed. Verify the configured user and credentials.",
            SshConnectionFailureKind.Timeout => "SSH connection timed out. Verify the host, port, and connection timeout.",
            SshConnectionFailureKind.HostUnreachable => "SSH host is unreachable. Verify the host, port, and network path.",
            _ => "SSH connection failed. Verify the SSH profile and host key settings.",
        };
    }
}
