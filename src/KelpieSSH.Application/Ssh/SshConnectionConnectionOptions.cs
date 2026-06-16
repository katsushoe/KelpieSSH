namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents SSH connection behavior configuration values for one profile.
/// </summary>
public sealed class SshConnectionConnectionOptions
{
    /// <summary>
    /// Gets or sets the SSH connection timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
