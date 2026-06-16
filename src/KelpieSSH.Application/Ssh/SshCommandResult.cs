namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents the result of an SSH command execution.
/// </summary>
/// <param name="CommandName">The allowed command name.</param>
/// <param name="CommandText">The exact command text sent over SSH.</param>
/// <param name="ExitCode">The remote command exit code.</param>
/// <param name="StandardOutput">The remote command standard output.</param>
/// <param name="StandardError">The remote command standard error.</param>
/// <param name="StartedAt">The UTC time when execution started.</param>
/// <param name="CompletedAt">The UTC time when execution completed.</param>
/// <param name="TimedOut">A value indicating whether the command timed out.</param>
public sealed record SshCommandResult(
    string CommandName,
    string CommandText,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool TimedOut);
