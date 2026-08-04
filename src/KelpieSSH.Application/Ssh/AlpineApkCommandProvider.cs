namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides Alpine Linux apk package management SSH commands.
/// </summary>
public sealed class AlpineApkCommandProvider : IAllowedCommandProvider
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
        new("pkg_check_updates", "apk version -l '<'", TimeSpan.FromSeconds(30)),
        new("pkg_info", "apk info -a {package}", TimeSpan.FromSeconds(30), [PackageParameter]),
        new(
            "pkg_search",
            "sh -c \"output=$(apk search \\\"$1\\\"); code=$?; printf '%s\\n' \\\"$output\\\" | head -n \\\"$2\\\"; exit \\\"$code\\\"\" -- {query} {limit}",
            TimeSpan.FromSeconds(30),
            [QueryParameter, LimitParameter]),
        new(
            "pkg_list_installed",
            "sh -c \"output=$(apk info); code=$?; printf '%s\\n' \\\"$output\\\" | grep -i -- \\\"$1\\\" | head -n \\\"$2\\\"; exit \\\"$code\\\"\" -- {filter} {limit}",
            TimeSpan.FromSeconds(30),
            [FilterParameter, LimitParameter]),
        new("pkg_simulate_install", "apk add --simulate {package}", TimeSpan.FromSeconds(60), [PackageParameter]),
        new(
            "pkg_install",
            "sudo -n apk add {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
        new("pkg_simulate_remove", "apk del --simulate {package}", TimeSpan.FromSeconds(60), [PackageParameter]),
        new(
            "pkg_remove",
            "sudo -n apk del {package}",
            TimeSpan.FromMinutes(10),
            [PackageParameter],
            SshCommandRiskLevel.ConfirmRequired),
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["alpine"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return string.Equals(OsFamilyAliasResolver.Resolve(profile.OsFamily), "alpine", StringComparison.OrdinalIgnoreCase)
            && string.Equals(profile.PackageManager, "apk", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AllowedCommandDefinition> GetCommands(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Supports(profile) ? Commands : [];
    }
}
