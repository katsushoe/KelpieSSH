namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides Void Linux XBPS package management SSH commands.
/// </summary>
public sealed class VoidXbpsCommandProvider : IAllowedCommandProvider
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
        // Use cached repository metadata; never add -S or -M to read-only operations.
        new("pkg_check_updates", "xbps-install -un", TimeSpan.FromSeconds(60)),
        new("pkg_info", "xbps-query -R {package}", TimeSpan.FromSeconds(30), [PackageParameter]),
        new(
            "pkg_search",
            "sh -c 'output=$(xbps-query -Rs \"$1\"); code=$?; printf \"%s\\n\" \"$output\" | head -n \"$2\"; exit \"$code\"' -- {query} {limit}",
            TimeSpan.FromSeconds(45),
            [QueryParameter, LimitParameter]),
        new(
            "pkg_list_installed",
            "sh -c 'output=$(xbps-query -l); code=$?; printf \"%s\\n\" \"$output\" | grep -i -F -- \"$1\" | head -n \"$2\"; exit \"$code\"' -- {filter} {limit}",
            TimeSpan.FromSeconds(45),
            [FilterParameter, LimitParameter]),
        new(
            "pkg_simulate_install",
            "xbps-install -n {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_install",
            "sudo -n xbps-install -y {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new(
            "pkg_simulate_remove",
            "xbps-remove -n {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_remove",
            "sudo -n xbps-remove -y {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["void"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return string.Equals(OsFamilyAliasResolver.Resolve(profile.OsFamily), "void", StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.PackageManager, "xbps", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AllowedCommandDefinition> GetCommands(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Supports(profile) ? Commands : [];
    }
}
