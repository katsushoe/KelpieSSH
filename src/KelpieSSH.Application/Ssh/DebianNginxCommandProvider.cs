namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides Debian-family systemd service control SSH commands.
/// </summary>
public sealed class DebianNginxCommandProvider : IAllowedCommandProvider
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
            "python3 -c \"import urllib.request; print(urllib.request.urlopen('http://127.0.0.1:' + {port} + '/', timeout=5).read().decode('utf-8'))\"",
            TimeSpan.FromSeconds(10),
            [PortParameter]),
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["debian"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return string.Equals(OsFamilyAliasResolver.Resolve(profile.OsFamily), "debian", StringComparison.OrdinalIgnoreCase);
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
