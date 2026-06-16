namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a bounded text search result under a web public root.
/// </summary>
public sealed record WebPublicTextSearchResult(
    string SiteKey,
    string DisplayName,
    string Path,
    string ResolvedPath,
    string Query,
    bool Exists,
    IReadOnlyCollection<WebPublicTextSearchMatch> Matches,
    bool Truncated,
    IReadOnlyList<string> Warnings,
    string? Error = null);
