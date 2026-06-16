namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents SSH target connection configuration values for one profile.
/// </summary>
public sealed class SshConnectionTargetOptions
{
    /// <summary>
    /// Gets or sets the SSH host name or address.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SSH port.
    /// </summary>
    public int Port { get; set; } = 22;

    /// <summary>
    /// Gets or sets the SSH user name.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SSH connection timeout in seconds.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 10;
}
