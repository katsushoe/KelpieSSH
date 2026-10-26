namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents the result of writing one application configuration file.
/// </summary>
public sealed record ServiceConfigFileWriteResult(
    string ServiceKey,
    string DisplayName,
    string? Path,
    string Encoding,
    int BytesWritten,
    IReadOnlyList<string> Warnings,
    string? Error = null)
{
    /// <summary>Whether the remote write outcome is known.</summary>
    public bool OutcomeKnown { get; init; } = true;
}
