namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a validated SSH command execution request.
/// </summary>
/// <param name="Profile">The SSH connection profile.</param>
/// <param name="CommandName">The allowed command name requested by the caller.</param>
/// <param name="CommandText">The exact command text sent over SSH.</param>
/// <param name="Timeout">The execution timeout.</param>
/// <param name="Arguments">The validated command arguments.</param>
/// <param name="StandardInput">The optional standard input sent to the command.</param>
/// <param name="EnvironmentOverrides">The optional environment overrides applied at execution time.</param>
/// <param name="BinaryStandardInput">The optional binary standard input encoded as Base64 while it is sent.</param>
public sealed record SshCommandRequest(
    SshConnectionProfile Profile,
    string CommandName,
    string CommandText,
    TimeSpan Timeout,
    IReadOnlyDictionary<string, string> Arguments,
    string? StandardInput = null,
    IReadOnlyDictionary<string, string>? EnvironmentOverrides = null,
    Stream? BinaryStandardInput = null);
