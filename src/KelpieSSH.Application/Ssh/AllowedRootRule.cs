namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents an allowed path root and its access level.
/// </summary>
/// <param name="Path">The root path or glob pattern.</param>
/// <param name="Access">The allowed access level.</param>
public sealed record AllowedRootRule(string Path, AllowedRootAccess Access);
