namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents the result of testing one application's configuration files.
/// </summary>
public sealed record ServiceConfigFileTestResult(
    string ServiceKey,
    string DisplayName,
    string TestCommand,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    IReadOnlyList<string> Stdout,
    IReadOnlyList<string> Stderr,
    IReadOnlyList<string> Warnings,
    string? Error = null);
