namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Resolves the package manager used by a command-processing OS family.
/// </summary>
public static class PackageManagerResolver
{
    /// <summary>
    /// Resolves the configured or default package manager.
    /// </summary>
    /// <param name="osFamily">The configured OS family.</param>
    /// <param name="packageManager">The configured package manager.</param>
    /// <returns>The configured package manager, or the default for the OS family.</returns>
    public static string Resolve(string osFamily, string? packageManager)
    {
        if (!string.IsNullOrWhiteSpace(packageManager))
        {
            return packageManager.Trim();
        }

        if (string.IsNullOrWhiteSpace(osFamily))
        {
            throw new InvalidOperationException("SSH OS family is required.");
        }

        var commandProcessingOsFamily = OsFamilyAliasResolver.Resolve(osFamily);
        return commandProcessingOsFamily switch
        {
            "debian" => "apt",
            "rhel" => "dnf",
            _ => throw new InvalidOperationException($"SSH package manager is not configured and cannot be inferred from OS family: {osFamily}"),
        };
    }
}
