namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides RHEL-family dnf package management SSH commands.
/// </summary>
public sealed class RhelDnfCommandProvider : IAllowedCommandProvider
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
            "python3 -c \"import subprocess,sys; query={query}; limit=int({limit}); result=subprocess.run(['dnf','search',query], text=True, capture_output=True); print('\\n'.join(result.stdout.splitlines()[:limit])); print(result.stderr, end='', file=sys.stderr); raise SystemExit(result.returncode)\"",
            TimeSpan.FromSeconds(60),
            [QueryParameter, LimitParameter]),
        new(
            "pkg_list_installed",
            "python3 -c \"import subprocess,sys; filter_text={filter}.lower(); limit=int({limit}); result=subprocess.run(['dnf','list','installed'], text=True, capture_output=True); lines=[line for line in result.stdout.splitlines() if filter_text in line.lower()]; print('\\n'.join(lines[:limit])); print(result.stderr, end='', file=sys.stderr); raise SystemExit(result.returncode)\"",
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
