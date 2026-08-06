namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a sanitized SSH command connection failure crossing the infrastructure boundary.
/// </summary>
public sealed class SshCommandConnectionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SshCommandConnectionException"/> class.
    /// </summary>
    /// <param name="message">The sanitized failure message.</param>
    /// <param name="timedOut">Whether the connection attempt timed out.</param>
    /// <param name="innerException">The infrastructure exception.</param>
    public SshCommandConnectionException(string message, bool timedOut, Exception? innerException = null)
        : this(
            message,
            timedOut ? SshConnectionFailureKind.Timeout : SshConnectionFailureKind.ConnectionFailed,
            innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshCommandConnectionException"/> class.
    /// </summary>
    /// <param name="message">The sanitized failure message.</param>
    /// <param name="failureKind">The sanitized failure category.</param>
    /// <param name="innerException">The infrastructure exception.</param>
    public SshCommandConnectionException(
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
}
