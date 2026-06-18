using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents web public site JSON options.
/// </summary>
public sealed class WebPublicSiteOptions
{
    /// <summary>
    /// Gets or sets the site key.
    /// </summary>
    public string SiteKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the site root.
    /// </summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the site root alias.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets allowed extensions.
    /// </summary>
    public JsonElement AllowedExtensions { get; set; }

    /// <summary>
    /// Gets or sets executable extensions that may be written only when explicitly allowed by the profile.
    /// </summary>
    public JsonElement WritableExecutableExtensions { get; set; }

    /// <summary>
    /// Gets or sets allowed content types.
    /// </summary>
    public JsonElement AllowedContentTypes { get; set; }

    /// <summary>
    /// Gets or sets allowed file glob rules.
    /// </summary>
    public JsonElement AllowedFiles { get; set; }

    /// <summary>
    /// Gets or sets whether missing parent directories may be created.
    /// </summary>
    public bool? CreateDirectories { get; set; }

    /// <summary>
    /// Gets or sets the maximum readable bytes.
    /// </summary>
    public int? MaxReadBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum writable bytes.
    /// </summary>
    public int? MaxWriteBytes { get; set; }
}
