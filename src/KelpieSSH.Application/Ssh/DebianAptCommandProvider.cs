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
            "python3 -c \"import subprocess,sys; query={query}; limit=int({limit}); result=subprocess.run(['apt-cache','search',query], text=True, capture_output=True); print('\\n'.join(result.stdout.splitlines()[:limit])); print(result.stderr, end='', file=sys.stderr); raise SystemExit(result.returncode)\"",
            TimeSpan.FromSeconds(30),
            [QueryParameter, LimitParameter]),
        new(
            "pkg_list_installed",
            "python3 -c \"import subprocess,sys; filter_text={filter}.lower(); limit=int({limit}); result=subprocess.run(['apt','list','--installed'], text=True, capture_output=True); lines=[line for line in result.stdout.splitlines() if filter_text in line.lower()]; print('\\n'.join(lines[:limit])); print(result.stderr, end='', file=sys.stderr); raise SystemExit(result.returncode)\"",
            TimeSpan.FromSeconds(30),
            [FilterParameter, LimitParameter]),
        new(
            "pkg_simulate_install",
            "apt-get -s install {package}",
            TimeSpan.FromSeconds(60),
            [PackageParameter]),
        new(
            "pkg_install",
            "apt-get install {package}",
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
            "apt-get remove {package}",
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
