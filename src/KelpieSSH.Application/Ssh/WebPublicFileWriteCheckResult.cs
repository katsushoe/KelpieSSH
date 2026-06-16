namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a non-mutating web public file write check result.
/// </summary>
public sealed record WebPublicFileWriteCheckResult(
    string SiteKey,
    string DisplayName,
    string Path,
    string ResolvedPath,
    bool Exists,
    bool CanWrite,
    bool RequiresConfirmation,
    string Confirmation,
    string ContentType,
    string? Reason,
    IReadOnlyList<string> Warnings,
    string? Error = null);

