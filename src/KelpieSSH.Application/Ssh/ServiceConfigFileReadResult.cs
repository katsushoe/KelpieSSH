namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents the content read from one application configuration file.
/// </summary>
public sealed record ServiceConfigFileReadResult(
    string ServiceKey,
    string DisplayName,
    string? Path,
    string Content,
    string Encoding,
    bool Truncated,
    IReadOnlyList<string> Warnings,
    string? Error = null);
