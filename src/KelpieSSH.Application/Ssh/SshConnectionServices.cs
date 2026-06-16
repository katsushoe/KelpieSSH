namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents service-specific profile settings.
/// </summary>
public sealed class SshConnectionServices
{
    /// <summary>
    /// Gets Nginx-specific settings.
    /// </summary>
    public NginxServiceSettings? Nginx { get; init; }
}
