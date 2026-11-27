namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Identifies a sanitized SSH connection failure category.
/// </summary>
public enum SshConnectionFailureKind
{
    /// <summary>The presented SSH host key did not match the pinned fingerprint.</summary>
    HostKeyMismatch = 4,
    /// <summary>
    /// The connection failed for an unclassified SSH reason.
    /// </summary>
    ConnectionFailed = 0,

    /// <summary>
    /// The target host could not be reached.
    /// </summary>
    HostUnreachable,

    /// <summary>
    /// The connection attempt timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// SSH authentication was rejected.
    /// </summary>
    AuthenticationFailed,
}
