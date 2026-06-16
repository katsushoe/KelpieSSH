namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Resolves user-facing OS family names to command-processing OS families.
/// </summary>
public static class OsFamilyAliasResolver
{
    /// <summary>
    /// Resolves an OS family to the command-processing family.
    /// </summary>
    /// <param name="osFamily">The configured OS family.</param>
    /// <returns>The command-processing OS family.</returns>
    public static string Resolve(string osFamily)
    {
        if (string.IsNullOrWhiteSpace(osFamily))
        {
            return string.Empty;
        }

        return osFamily.Trim().ToLowerInvariant() switch
        {
            "ubuntu" => "debian",
            "alma" => "rhel",
            "almalinux" => "rhel",
            "rocky" => "rhel",
            "rockylinux" => "rhel",
            "centos" => "rhel",
            "oraclelinux" => "rhel",
            "ol" => "rhel",
            var value => value,
        };
    }
}
