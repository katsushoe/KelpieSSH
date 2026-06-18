namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Applies fixed-template PHP-FPM routing to Nginx site configuration.
/// </summary>
public interface INginxPhpConfigurator
{
    /// <summary>
    /// Enables fixed-template PHP-FPM routing for one provider-approved Nginx site.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="siteKey">The provider-resolved site key.</param>
    /// <param name="socketPath">The PHP-FPM Unix socket path.</param>
    /// <param name="extension">The executable extension to route.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The PHP routing configuration result.</returns>
    Task<NginxPhpEnableResult> EnablePhpAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string socketPath,
        string extension,
        CancellationToken cancellationToken = default);
}
