namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents Nginx-specific profile settings.
/// </summary>
public sealed class NginxServiceSettings
{
    /// <summary>
    /// Gets the Nginx worker user.
    /// </summary>
    public string User { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Nginx worker group.
    /// </summary>
    public string Group { get; init; } = string.Empty;

    /// <summary>
    /// Gets the listen port.
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// Gets the web root path.
    /// </summary>
    public string Root { get; init; } = string.Empty;
}
