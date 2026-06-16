namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a content type access rule for web public files.
/// </summary>
public sealed record WebPublicContentTypeRule(
    string ContentType,
    AllowedRootAccess Access);
