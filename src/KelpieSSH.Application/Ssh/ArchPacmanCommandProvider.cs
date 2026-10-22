namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides Arch Linux pacman package management SSH commands.
/// </summary>
public sealed class ArchPacmanCommandProvider : IAllowedCommandProvider
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
        // Uses the existing sync database. Refreshing it separately is intentionally not part of this read-only operation.
        new("pkg_check_updates", "pacman --query --upgrades", TimeSpan.FromSeconds(30)),
        new("pkg_info", "pacman --sync --info {package}", TimeSpan.FromSeconds(30), [PackageParameter]),
        new(
            "pkg_search",
            "sh -c 'output=$(pacman --sync --search \"$1\"); code=$?; printf \"%s\\n\" \"$output\" | head -n \"$2\"; exit \"$code\"' -- {query} {limit}",
            TimeSpan.FromSeconds(30),
            [QueryParameter, LimitParameter]),
        new(
            "pkg_list_installed",
            "sh -c 'output=$(pacman --query --quiet); code=$?; printf \"%s\\n\" \"$output\" | grep -i -- \"$1\" | head -n \"$2\"; exit \"$code\"' -- {filter} {limit}",
            TimeSpan.FromSeconds(30),
            [FilterParameter, LimitParameter]),
        new("pkg_simulate_install", "pacman --sync --print --print-format '%n %v' {package}", TimeSpan.FromSeconds(60), [PackageParameter]),
        new(
            "pkg_install",
            "sudo -n pacman --sync --noconfirm {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new("pkg_simulate_remove", "pacman --remove --print --print-format '%n %v' {package}", TimeSpan.FromSeconds(60), [PackageParameter]),
        new(
            "pkg_remove",
            "sudo -n pacman --remove --noconfirm {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["arch"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return string.Equals(OsFamilyAliasResolver.Resolve(profile.OsFamily), "arch", StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.PackageManager, "pacman", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AllowedCommandDefinition> GetCommands(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Supports(profile) ? Commands : [];
    }
}
