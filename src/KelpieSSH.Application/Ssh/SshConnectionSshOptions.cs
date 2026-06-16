namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents SSH endpoint configuration values for one profile.
/// </summary>
public sealed class SshConnectionSshOptions
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
    /// Gets or sets the SSH authentication settings.
    /// </summary>
    public SshConnectionAuthenticationOptions Authentication { get; set; } = new();
}
