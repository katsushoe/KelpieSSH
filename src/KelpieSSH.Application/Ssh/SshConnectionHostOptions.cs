namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents SSH host endpoint configuration values for one profile.
/// </summary>
public sealed class SshConnectionHostOptions
{
    /// <summary>
    /// Gets or sets the SSH host name or IP address.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SSH port.
    /// </summary>
    public int Port { get; set; } = 22;

    /// <summary>
    /// Gets or sets the expected SSH host key SHA256 fingerprint.
    /// </summary>
    public string HostKeyFingerprintSha256 { get; set; } = string.Empty;
}
