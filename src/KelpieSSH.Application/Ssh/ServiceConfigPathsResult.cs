namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents configuration file paths discovered for one application.
/// </summary>
public sealed record ServiceConfigPathsResult(
    string ServiceKey,
    string DisplayName,
    string? MainConfig,
    IReadOnlyList<string> ConfigFiles,
    IReadOnlyList<string> IncludePatterns,
    IReadOnlyList<string> Warnings,
    string? Error = null);
