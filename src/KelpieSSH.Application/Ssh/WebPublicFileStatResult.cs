namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents metadata for one web public path.
/// </summary>
public sealed record WebPublicFileStatResult(
    string SiteKey,
    string DisplayName,
    string Path,
    string ResolvedPath,
    bool Exists,
    string Type,
    long Size,
    string Mode,
    string Owner,
    string Group,
    string? LastModified,
    bool IsSymlink,
    IReadOnlyList<string> Warnings,
    string? Error = null);

