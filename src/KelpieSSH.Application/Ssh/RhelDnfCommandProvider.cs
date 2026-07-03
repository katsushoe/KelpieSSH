namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides RHEL-family dnf package management SSH commands.
/// </summary>
public sealed class RhelDnfCommandProvider : IAllowedCommandProvider
{
    private const string PackageNamePattern = "^[a-zA-Z0-9][a-zA-Z0-9.+:_-]{0,127}$";
    private const string PackageQueryPattern = "^[a-zA-Z0-9][a-zA-Z0-9.+:_-]{0,63}$";
    private const string CertbotPluginPattern = "^(none|nginx|apache)$";
    private const string LimitPattern = "^[0-9]{1,3}$";

    private static readonly AllowedCommandParameterDefinition PackageParameter =
        new("package", Pattern: PackageNamePattern);

    private static readonly AllowedCommandParameterDefinition QueryParameter =
        new("query", MaxLength: 64, Pattern: PackageQueryPattern);

    private static readonly AllowedCommandParameterDefinition FilterParameter =
        new("filter", MaxLength: 64, Pattern: PackageQueryPattern);

    private static readonly AllowedCommandParameterDefinition CertbotPluginParameter =
        new("plugin", Pattern: CertbotPluginPattern);

    private static readonly AllowedCommandParameterDefinition LimitParameter =
        new("limit", MaxLength: 3, Pattern: LimitPattern);

    private static readonly AllowedCommandDefinition[] Commands =
    [
        new(
            "pkg_check_updates",
            "sh -c 'dnf check-update; code=$?; if [ \"$code\" -eq 100 ]; then exit 0; fi; exit \"$code\"'",
            TimeSpan.FromSeconds(60)),
        new(
            "pkg_info",
            "dnf info {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_search",
            "sh -c \"output=$(dnf search \\\"$1\\\"); code=$?; printf '%s\\n' \\\"$output\\\" | head -n \\\"$2\\\"; exit \\\"$code\\\"\" -- {query} {limit}",
            TimeSpan.FromSeconds(60),
            [QueryParameter, LimitParameter]),
        new(
            "pkg_list_installed",
            "sh -c \"output=$(dnf list installed); code=$?; printf '%s\\n' \\\"$output\\\" | grep -i -- \\\"$1\\\" | head -n \\\"$2\\\"; exit \\\"$code\\\"\" -- {filter} {limit}",
            TimeSpan.FromSeconds(60),
            [FilterParameter, LimitParameter]),
        new(
            "pkg_simulate_install",
            "sudo -n dnf install -y --setopt=tsflags=test {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_install",
            "sudo -n dnf install -y {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "pkg_simulate_remove",
            "sudo -n dnf remove -y --setopt=tsflags=test {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_remove",
            "sudo -n dnf remove -y {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "certbot_check_install",
            "sh -c \"plugin=$1; printf 'packageManager=dnf\\n'; printf 'plugin=%s\\n' \\\"$plugin\\\"; if command -v certbot >/dev/null 2>&1; then printf 'certbotInstalled=true\\n'; certbot --version 2>/dev/null | head -n 1; else printf 'certbotInstalled=false\\n'; fi; if command -v nginx >/dev/null 2>&1; then printf 'nginxInstalled=true\\n'; else printf 'nginxInstalled=false\\n'; fi; if command -v apache2 >/dev/null 2>&1 || command -v httpd >/dev/null 2>&1; then printf 'apacheInstalled=true\\n'; else printf 'apacheInstalled=false\\n'; fi; printf 'candidatePackages='; case \\\"$plugin\\\" in nginx) printf 'certbot python3-certbot-nginx\\n'; dnf list certbot python3-certbot-nginx 2>/dev/null || true ;; apache) printf 'certbot python3-certbot-apache\\n'; dnf list certbot python3-certbot-apache 2>/dev/null || true ;; none) printf 'certbot\\n'; dnf list certbot 2>/dev/null || true ;; *) printf 'unsupportedPlugin=%s\\n' \\\"$plugin\\\"; exit 2 ;; esac; printf 'confirmation=certbot_install:%s\\n' \\\"$plugin\\\"\" -- {plugin}",
            TimeSpan.FromSeconds(60),
            [CertbotPluginParameter]),
        new(
            "certbot_install",
            "sudo -n sh -c \"plugin=$1; case \\\"$plugin\\\" in nginx) dnf install -y certbot python3-certbot-nginx ;; apache) dnf install -y certbot python3-certbot-apache ;; none) dnf install -y certbot ;; *) exit 2 ;; esac\" -- {plugin}",
            TimeSpan.FromMinutes(10),
            [CertbotPluginParameter],
            SshCommandRiskLevel.ConfirmRequired),
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["rhel"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return string.Equals(OsFamilyAliasResolver.Resolve(profile.OsFamily), "rhel", StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.PackageManager, "dnf", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AllowedCommandDefinition> GetCommands(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!Supports(profile))
        {
            return [];
        }

        return Commands;
    }
}
