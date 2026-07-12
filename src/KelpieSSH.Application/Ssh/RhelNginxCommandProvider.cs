namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides RHEL-family nginx test-site SSH commands.
/// </summary>
public sealed class RhelNginxCommandProvider : IAllowedCommandProvider
{
    private const string ServiceNamePattern = "^[a-zA-Z0-9_.@-]{1,128}$";

    private static readonly AllowedCommandParameterDefinition ServiceParameter =
        new("service", Pattern: ServiceNamePattern);

    private static readonly AllowedCommandParameterDefinition PortParameter =
        new("port", Pattern: AllowedCommandPatterns.TcpPort);

    private static readonly AllowedCommandDefinition[] Commands =
    [
        new(
            "service_enable_now",
            "sudo -n systemctl enable --now {service}",
            TimeSpan.FromSeconds(60),
            [ServiceParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_reload",
            "sudo -n systemctl reload {service}",
            TimeSpan.FromSeconds(60),
            [ServiceParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_restart",
            "sudo -n systemctl restart {service}",
            TimeSpan.FromSeconds(90),
            [ServiceParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_stop",
            "sudo -n systemctl stop {service}",
            TimeSpan.FromSeconds(60),
            [ServiceParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_disable",
            "sudo -n systemctl disable {service}",
            TimeSpan.FromSeconds(60),
            [ServiceParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "http_get_local",
            "sh -c \"if command -v curl >/dev/null 2>&1; then curl -fsS --max-time 5 \\\"http://127.0.0.1:$1/\\\"; elif command -v wget >/dev/null 2>&1; then wget -qO- -T 5 \\\"http://127.0.0.1:$1/\\\"; else printf '%s\\n' 'ERROR: curl or wget is required' >&2; exit 127; fi\" -- {port}",
            TimeSpan.FromSeconds(10),
            [PortParameter]),
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["rhel"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return string.Equals(OsFamilyAliasResolver.Resolve(profile.OsFamily), "rhel", StringComparison.OrdinalIgnoreCase);
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
