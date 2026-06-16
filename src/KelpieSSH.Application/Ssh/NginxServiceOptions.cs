namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents Nginx-specific profile JSON options.
/// </summary>
public sealed class NginxServiceOptions
{
    /// <summary>
    /// Gets or sets the Nginx worker user.
    /// </summary>
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Nginx worker group.
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the listen port.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Gets or sets the web root path.
    /// </summary>
    public string Root { get; set; } = string.Empty;
}
