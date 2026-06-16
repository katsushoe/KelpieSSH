namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one web public directory listing entry.
/// </summary>
public sealed record WebPublicFileListEntry(
    string Name,
    string Path,
    string ResolvedPath,
    string Type,
    long Size,
    string Mode,
    string Owner,
    string Group,
    string? LastModified,
    int Depth,
    bool IsSymlink);

