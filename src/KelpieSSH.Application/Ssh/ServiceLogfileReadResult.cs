namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents the content read from one application log file.
/// </summary>
public sealed record ServiceLogfileReadResult(
    string ServiceKey,
    string DisplayName,
    string LogKey,
    string? Path,
    string Content,
    string Encoding,
    bool Truncated,
    IReadOnlyList<string> Warnings,
    string? Error = null);
