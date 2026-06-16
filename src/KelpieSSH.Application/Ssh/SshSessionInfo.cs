namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one temporary SSH-related session held by the Kelpie server process.
/// </summary>
/// <param name="Handle">The random handle assigned when the session was opened.</param>
/// <param name="ProfileName">The SSH profile name.</param>
/// <param name="SecretName">The secret reference name used by the session.</param>
/// <param name="StartedAtUtc">The UTC time when the session was opened.</param>
/// <param name="Kind">The session kind.</param>
public sealed record SshSessionInfo(
    string Handle,
    string ProfileName,
    string SecretName,
    DateTimeOffset StartedAtUtc,
    string Kind);
