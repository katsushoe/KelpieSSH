namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one web public file write result.
/// </summary>
public sealed record WebPublicFileWriteResult(
    string SiteKey,
    string DisplayName,
    string Path,
    string ResolvedPath,
    bool Written,
    bool Created,
    bool Overwritten,
    string ContentType,
    long Size,
    IReadOnlyList<string> Warnings,
    string? Error = null,
    string Owner = "",
    string Group = "",
    string Mode = "",
    string? ReasonCode = null,
    string? Guidance = null,
    string PreviousSha256 = "",
    string Sha256 = "",
    string BackupPath = "",
    bool PermissionsPreserved = false);
