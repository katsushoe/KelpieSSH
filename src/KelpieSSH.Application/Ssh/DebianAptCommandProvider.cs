namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides Debian/Ubuntu apt package management SSH commands.
/// </summary>
public sealed class DebianAptCommandProvider : IAllowedCommandProvider
{
    private const string PackageNamePattern = "^[a-zA-Z0-9][a-zA-Z0-9.+:-]{0,127}$";
    private const string PackageQueryPattern = "^[a-zA-Z0-9][a-zA-Z0-9.+:_-]{0,63}$";
    private const string LimitPattern = "^[0-9]{1,3}$";

    private static readonly AllowedCommandParameterDefinition PackageParameter =
        new("package", Pattern: PackageNamePattern);

    private static readonly AllowedCommandParameterDefinition QueryParameter =
        new("query", MaxLength: 64, Pattern: PackageQueryPattern);

    private static readonly AllowedCommandParameterDefinition FilterParameter =
        new("filter", MaxLength: 64, Pattern: PackageQueryPattern);

    private static readonly AllowedCommandParameterDefinition LimitParameter =
        new("limit", MaxLength: 3, Pattern: LimitPattern);

    private static readonly AllowedCommandDefinition[] Commands =
    [
        new(
            "pkg_check_updates",
            "apt list --upgradable",
            TimeSpan.FromSeconds(30)),
        new(
            "pkg_info",
            "apt-cache policy {package}",
            TimeSpan.FromSeconds(30),
            [PackageParameter]),
        new(
            "pkg_search",
            "sh -c \"output=$(apt-cache search \\\"$1\\\"); code=$?; printf '%s\\n' \\\"$output\\\" | head -n \\\"$2\\\"; exit \\\"$code\\\"\" -- {query} {limit}",
            TimeSpan.FromSeconds(30),
            [QueryParameter, LimitParameter]),
        new(
            "pkg_list_installed",
            "sh -c \"output=$(apt list --installed); code=$?; printf '%s\\n' \\\"$output\\\" | grep -i -- \\\"$1\\\" | head -n \\\"$2\\\"; exit \\\"$code\\\"\" -- {filter} {limit}",
            TimeSpan.FromSeconds(30),
            [FilterParameter, LimitParameter]),
        new(
            "pkg_simulate_install",
            "apt-get -s install {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_install",
            "sudo -n env DEBIAN_FRONTEND=noninteractive apt-get install -y {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "pkg_simulate_remove",
            "apt-get -s remove {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_remove",
            "sudo -n env DEBIAN_FRONTEND=noninteractive apt-get remove -y {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["debian"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return string.Equals(OsFamilyAliasResolver.Resolve(profile.OsFamily), "debian", StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.PackageManager, "apt", StringComparison.OrdinalIgnoreCase);
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
