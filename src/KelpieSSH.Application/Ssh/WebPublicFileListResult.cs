namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a web public directory listing result.
/// </summary>
public sealed record WebPublicFileListResult(
    string SiteKey,
    string DisplayName,
    string Path,
    string ResolvedPath,
    bool Exists,
    IReadOnlyList<WebPublicFileListEntry> Entries,
    bool Truncated,
    IReadOnlyList<string> Warnings,
    string? Error = null);

