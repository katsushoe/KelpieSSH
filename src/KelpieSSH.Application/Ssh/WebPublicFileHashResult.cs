namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a metadata-only hash result for one web public file.
/// </summary>
public sealed record WebPublicFileHashResult(
    string ProfileName,
    string SiteKey,
    string Path,
    string ResolvedPath,
    string Algorithm,
    string? Hash,
    long Size,
    string Owner,
    string Group,
    string Mode,
    bool IsSymlink,
    IReadOnlyList<string> Warnings,
    WebPublicFileHashError? Error);

/// <summary>
/// Represents a safe web public file hash error.
/// </summary>
public sealed record WebPublicFileHashError(
    string Code,
    string Message,
    string CorrelationId);
