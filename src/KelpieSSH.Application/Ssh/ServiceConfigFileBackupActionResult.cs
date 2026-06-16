namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents the result of an application configuration backup action.
/// </summary>
/// <param name="ServiceKey">The service key.</param>
/// <param name="DisplayName">The application display name.</param>
/// <param name="Path">The target configuration path.</param>
/// <param name="BackupPath">The backup path.</param>
/// <param name="Changed">A value indicating whether the backup state changed.</param>
/// <param name="Warnings">Warnings collected while processing the request.</param>
/// <param name="Error">The error message, if the action failed.</param>
public sealed record ServiceConfigFileBackupActionResult(
    string ServiceKey,
    string DisplayName,
    string? Path,
    string BackupPath,
    bool Changed,
    IReadOnlyList<string> Warnings,
    string? Error = null);
