namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one web public permission change result.
/// </summary>
public sealed record WebPublicPermissionChangeResult(
    string SiteKey,
    string DisplayName,
    string Path,
    string ResolvedPath,
    bool Changed,
    string Owner,
    string Group,
    string Mode,
    IReadOnlyList<string> Warnings,
    string? Error = null);
