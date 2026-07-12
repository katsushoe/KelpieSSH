namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents public metadata for a short-lived secret payload.
/// </summary>
public sealed record KelpieSecretInfo(
    string Name,
    long Size,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);
