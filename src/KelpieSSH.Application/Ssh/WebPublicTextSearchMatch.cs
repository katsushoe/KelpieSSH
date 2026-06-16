namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one bounded web public text search match.
/// </summary>
public sealed record WebPublicTextSearchMatch(
    string Path,
    string ResolvedPath,
    long LineNumber,
    string Line,
    long Size);
