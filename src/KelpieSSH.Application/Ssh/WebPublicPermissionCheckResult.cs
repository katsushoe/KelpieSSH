namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a non-mutating web public permission change check.
/// </summary>
public sealed record WebPublicPermissionCheckResult(
    string SiteKey,
    string DisplayName,
    string Path,
    string ResolvedPath,
    bool Exists,
    string Type,
    string CurrentOwner,
    string CurrentGroup,
    string CurrentMode,
    bool CanChangeOwner,
    bool CanChangeMode,
    string OwnerConfirmation,
    string ModeConfirmation,
    string? Reason,
    IReadOnlyList<string> Warnings,
    string? Error = null);
