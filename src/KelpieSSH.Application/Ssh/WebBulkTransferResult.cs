namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one file deployed by a bulk web transfer.
/// </summary>
public sealed record WebBulkTransferFileResult(
    string Path,
    string ResolvedPath,
    bool Created,
    bool Overwritten,
    long Size,
    string Sha256,
    string BackupPath);

/// <summary>
/// Represents the result of one bulk web transfer operation.
/// </summary>
public sealed record WebBulkTransferResult(
    string SiteKey,
    string TransferId,
    bool Applied,
    IReadOnlyList<WebBulkTransferFileResult> Files,
    IReadOnlyList<string> Warnings,
    string? Error = null);

/// <summary>
/// Represents one file supplied to a bulk web transfer.
/// </summary>
public sealed record WebBulkTransferFile(
    string ArchiveEntry,
    string Path,
    long Size,
    string Sha256,
    string ContentType,
    string? Owner,
    string? Mode);
