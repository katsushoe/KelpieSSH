namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one web public file read result.
/// </summary>
public sealed record WebPublicFileReadResult(
    string SiteKey,
    string DisplayName,
    string Path,
    string ResolvedPath,
    bool Exists,
    string? ContentBase64,
    string Encoding,
    string ContentType,
    long Size,
    string? LastModified,
    IReadOnlyList<string> Warnings,
    string? Error = null);
