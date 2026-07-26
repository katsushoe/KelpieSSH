using KelpieSSH.Application.Ssh;

namespace KelpieMCPServer;

internal static class ControlPipeAccessPolicy
{
    private static readonly HashSet<string> CrossUserReferenceCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "ping",
        "sessions",
        "secret-list",
        "env-list",
        "profile-capabilities",
    };

    public static bool IsAllowed(string? message, ControlPipeCaller caller)
    {
        return caller.CanUpdate || CrossUserReferenceCommands.Contains(GetCommandName(message));
    }

    public static IReadOnlyCollection<SshSessionInfo> RedactSessions(
        IReadOnlyCollection<SshSessionInfo> sessions)
    {
        return sessions.Select(session => session with
        {
            Handle = "(redacted)",
            ProfileName = "(redacted)",
            SecretName = string.Empty,
        }).ToArray();
    }

    public static IReadOnlyCollection<KelpieSecretInfo> RedactSecrets(
        IReadOnlyCollection<KelpieSecretInfo> secrets)
    {
        return secrets.Select(secret => secret with
        {
            Name = "(redacted)",
            Size = 0,
        }).ToArray();
    }

    public static IReadOnlyCollection<KelpieEnvironmentOverrideInfo> RedactEnvironmentOverrides(
        IReadOnlyCollection<KelpieEnvironmentOverrideInfo> overrides)
    {
        return overrides.Select(item => item with
        {
            ProfileName = "(redacted)",
            Key = "(redacted)",
            ValueLength = 0,
        }).ToArray();
    }

    public static SshProfileTrustCapabilities RedactProfileCapabilities()
    {
        return new SshProfileTrustCapabilities(
            "(redacted)",
            AddAllowed: false,
            ReloadAllowed: false,
            RevokeAllowed: false,
            Reason: "redacted-for-cross-user");
    }

    private static string GetCommandName(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        var separatorIndex = message.IndexOf(' ');
        return separatorIndex < 0 ? message : message[..separatorIndex];
    }
}

internal readonly record struct ControlPipeCaller(
    string? UserSid,
    bool IsServerOwner,
    bool IsAdministrator,
    bool IsSystem)
{
    public bool CanUpdate => IsServerOwner || IsAdministrator || IsSystem;

    public bool RequiresRedaction => !CanUpdate;

    public static ControlPipeCaller Trusted { get; } = new(
        UserSid: null,
        IsServerOwner: true,
        IsAdministrator: false,
        IsSystem: false);
}
