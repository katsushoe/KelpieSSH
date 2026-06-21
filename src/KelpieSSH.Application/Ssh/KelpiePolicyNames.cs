namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines supported Kelpie policy flag names.
/// </summary>
public static class KelpiePolicyNames
{
    /// <summary>
    /// Allows shell aliases.
    /// </summary>
    public const string AllowAlias = nameof(AllowAlias);

    /// <summary>
    /// Allows sudo execution.
    /// </summary>
    public const string AllowSudo = nameof(AllowSudo);

    /// <summary>
    /// Allows showing host password files or password-like secrets.
    /// </summary>
    public const string AllowShowPassword = nameof(AllowShowPassword);

    /// <summary>
    /// Allows showing host private key files.
    /// </summary>
    public const string AllowShowPrivateKey = nameof(AllowShowPrivateKey);

    /// <summary>
    /// Allows listing environment variable keys.
    /// </summary>
    public const string AllowPeekEnvironmentKeys = nameof(AllowPeekEnvironmentKeys);

    /// <summary>
    /// Allows reading environment variable values when profile rules permit it.
    /// </summary>
    public const string AllowPeekEnvironmentValues = nameof(AllowPeekEnvironmentValues);

    /// <summary>
    /// Allows setting environment variable values for one command execution when profile rules permit it.
    /// </summary>
    public const string AllowSetEnvironmentValues = nameof(AllowSetEnvironmentValues);

    /// <summary>
    /// Allows package list commands.
    /// </summary>
    public const string AllowListPackage = nameof(AllowListPackage);

    /// <summary>
    /// Allows package index update commands.
    /// </summary>
    public const string AllowUpdatePackageIndex = nameof(AllowUpdatePackageIndex);

    /// <summary>
    /// Allows package installation commands.
    /// </summary>
    public const string AllowInstallPackage = nameof(AllowInstallPackage);

    /// <summary>
    /// Allows package removal commands.
    /// </summary>
    public const string AllowRemovePackage = nameof(AllowRemovePackage);

    /// <summary>
    /// Allows physical file deletion.
    /// </summary>
    public const string AllowDeleteFiles = nameof(AllowDeleteFiles);

    /// <summary>
    /// Allows physical file movement.
    /// </summary>
    public const string AllowMoveFiles = nameof(AllowMoveFiles);

    /// <summary>
    /// Allows physical directory movement.
    /// </summary>
    public const string AllowMoveDirectory = nameof(AllowMoveDirectory);

    /// <summary>
    /// Gets all supported policy flag names.
    /// </summary>
    /// <returns>The supported policy flag names.</returns>
    public static IReadOnlyCollection<string> List()
    {
        return
        [
            AllowAlias,
            AllowSudo,
            AllowShowPassword,
            AllowShowPrivateKey,
            AllowPeekEnvironmentKeys,
            AllowPeekEnvironmentValues,
            AllowSetEnvironmentValues,
            AllowListPackage,
            AllowUpdatePackageIndex,
            AllowInstallPackage,
            AllowRemovePackage,
            AllowDeleteFiles,
            AllowMoveFiles,
            AllowMoveDirectory,
        ];
    }
}
