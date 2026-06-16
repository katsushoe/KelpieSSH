namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents service-specific profile JSON options.
/// </summary>
public sealed class SshConnectionServicesOptions
{
    /// <summary>
    /// Gets or sets Nginx-specific settings.
    /// </summary>
    public NginxServiceOptions? Nginx { get; set; } = new();
}
