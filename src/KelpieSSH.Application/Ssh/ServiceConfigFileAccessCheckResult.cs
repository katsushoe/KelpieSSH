namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a non-destructive access check result for one application configuration file.
/// </summary>
public sealed record ServiceConfigFileAccessCheckResult(
    string ServiceKey,
    string DisplayName,
    string? Path,
    bool CanRead,
    bool CanWrite,
    bool RequiresConfirmation,
    string? Confirmation,
    string? Method,
    string? TargetKey,
    string Encoding,
    IReadOnlyList<string> Warnings,
    string? Reason = null,
    string? Error = null);
