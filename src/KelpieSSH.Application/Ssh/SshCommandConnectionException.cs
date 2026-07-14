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
        : base(message, innerException)
    {
        TimedOut = timedOut;
    }

    /// <summary>
    /// Gets a value indicating whether the connection attempt timed out.
    /// </summary>
    public bool TimedOut { get; }
}
