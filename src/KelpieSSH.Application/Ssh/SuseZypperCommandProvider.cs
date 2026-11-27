namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides SUSE-family zypper package management SSH commands.
/// </summary>
public sealed class SuseZypperCommandProvider : IAllowedCommandProvider
{
    private const string PackageNamePattern = "^[a-zA-Z0-9][a-zA-Z0-9.+:_-]{0,127}$";
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
        // Avoid zypper's implicit metadata refresh in this read-only listing operation.
        new("pkg_check_updates", "zypper --no-refresh list-updates", TimeSpan.FromSeconds(60)),
        new("pkg_info", "zypper --no-refresh info {package}", TimeSpan.FromSeconds(30), [PackageParameter]),
        new(
            "pkg_search",
            "sh -c 'output=$(zypper --no-refresh search \"$1\"); code=$?; printf \"%s\\n\" \"$output\" | head -n \"$2\"; exit \"$code\"' -- {query} {limit}",
            TimeSpan.FromSeconds(45),
            [QueryParameter, LimitParameter]),
        new(
            "pkg_list_installed",
            "sh -c 'output=$(zypper --no-refresh search -i \"$1\"); code=$?; printf \"%s\\n\" \"$output\" | head -n \"$2\"; exit \"$code\"' -- {filter} {limit}",
            TimeSpan.FromSeconds(45),
            [FilterParameter, LimitParameter]),
        new(
            "pkg_simulate_install",
            "sudo -n zypper --non-interactive install --dry-run {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_install",
            "sudo -n zypper --non-interactive install {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "pkg_simulate_remove",
            "sudo -n zypper --non-interactive remove --dry-run {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_remove",
            "sudo -n zypper --non-interactive remove {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["suse"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return string.Equals(OsFamilyAliasResolver.Resolve(profile.OsFamily), "suse", StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.PackageManager, "zypper", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AllowedCommandDefinition> GetCommands(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Supports(profile) ? Commands : [];
    }
}
