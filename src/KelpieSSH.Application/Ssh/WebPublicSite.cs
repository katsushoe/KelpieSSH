namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one provider-approved web public root.
/// </summary>
public sealed class WebPublicSite
{
    /// <summary>
    /// Gets the site key.
    /// </summary>
    public required string SiteKey { get; init; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the Unix web public root path.
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Gets the allowed file extensions.
    /// </summary>
    public IReadOnlyCollection<string> AllowedExtensions { get; init; } = [];

    /// <summary>
    /// Gets executable extensions that may be written only when explicitly allowed by the profile.
    /// </summary>
    public IReadOnlyCollection<string> WritableExecutableExtensions { get; init; } = [];

    /// <summary>
    /// Gets content type access rules.
    /// </summary>
    public IReadOnlyCollection<WebPublicContentTypeRule> AllowedContentTypes { get; init; } = [];

    /// <summary>
    /// Gets file access rules. When configured, these rules control read and write permission by glob.
    /// </summary>
    public IReadOnlyCollection<WebPublicFileRule> AllowedFiles { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether missing parent directories may be created.
    /// </summary>
    public bool CreateDirectories { get; init; } = true;

    /// <summary>
    /// Gets the maximum readable file size.
    /// </summary>
    public int MaxReadBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>
    /// Gets the maximum writable file size.
    /// </summary>
    public int MaxWriteBytes { get; init; } = 5 * 1024 * 1024;
}
