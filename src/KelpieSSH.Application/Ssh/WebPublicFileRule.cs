namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one web public file access rule.
/// </summary>
public sealed record WebPublicFileRule(
    string Pattern,
    AllowedRootAccess Access);
