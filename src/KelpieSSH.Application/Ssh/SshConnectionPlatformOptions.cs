namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents target platform configuration values for one SSH profile.
/// </summary>
public sealed class SshConnectionPlatformOptions
{
    /// <summary>
    /// Gets or sets the target OS family.
    /// </summary>
    public string OsFamily { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target package manager.
    /// </summary>
    public string PackageManager { get; set; } = string.Empty;
}
