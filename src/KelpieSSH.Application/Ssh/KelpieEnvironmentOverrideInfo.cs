namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Describes one in-memory environment override held by the running MCP server.
/// </summary>
/// <param name="ProfileName">The SSH profile name.</param>
/// <param name="Key">The environment variable key.</param>
/// <param name="ValueLength">The environment variable value length in characters.</param>
/// <param name="UpdatedAtUtc">The last update timestamp in UTC.</param>
public sealed record KelpieEnvironmentOverrideInfo(
    string ProfileName,
    string Key,
    int ValueLength,
    DateTimeOffset UpdatedAtUtc);
