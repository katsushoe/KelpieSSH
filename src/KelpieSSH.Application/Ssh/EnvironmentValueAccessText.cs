namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Parses and formats environment variable access rules.
/// </summary>
public static class EnvironmentValueAccessText
{
    /// <summary>
    /// Parses an environment variable access expression.
    /// </summary>
    /// <param name="value">The access expression.</param>
    /// <returns>The parsed access flags.</returns>
    public static EnvironmentValueAccess Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("SSH environment value access is required.");
        }

        var access = EnvironmentValueAccess.None;
        foreach (var part in value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            access |= ParsePart(part);
        }

        Validate(access);
        return access;
    }

    /// <summary>
    /// Formats environment variable access flags.
    /// </summary>
    /// <param name="access">The access flags.</param>
    /// <returns>The formatted access expression.</returns>
    public static string Format(EnvironmentValueAccess access)
    {
        if (access == EnvironmentValueAccess.None)
        {
            return "None";
        }

        return string.Join("|", Enum.GetValues<EnvironmentValueAccess>()
            .Where(flag => flag != EnvironmentValueAccess.None && access.HasFlag(flag))
            .Select(flag => flag.ToString()));
    }

    private static EnvironmentValueAccess ParsePart(string part)
    {
        return part switch
        {
            "Common" => EnvironmentValueAccess.PeekCommon | EnvironmentValueAccess.SetCommon,
            "Secret" => EnvironmentValueAccess.PeekSecret | EnvironmentValueAccess.SetSecret,
            "Log" => EnvironmentValueAccess.PeekLog | EnvironmentValueAccess.SetLog,
            _ when Enum.TryParse<EnvironmentValueAccess>(part, ignoreCase: true, out var parsed) => parsed,
            _ => throw new InvalidOperationException($"Unknown SSH environment value access: {part}"),
        };
    }

    private static void Validate(EnvironmentValueAccess access)
    {
        if (access == EnvironmentValueAccess.None)
        {
            throw new InvalidOperationException("SSH environment value access is required.");
        }

        var permissions = EnvironmentValueAccess.PeekCommon
            | EnvironmentValueAccess.SetCommon
            | EnvironmentValueAccess.PeekSecret
            | EnvironmentValueAccess.SetSecret;
        var auditOnly = EnvironmentValueAccess.PeekLog
            | EnvironmentValueAccess.SetLog
            | EnvironmentValueAccess.NoLog;

        if ((access & permissions) == EnvironmentValueAccess.None
            && (access & (EnvironmentValueAccess.Hidden | EnvironmentValueAccess.Masked | EnvironmentValueAccess.KeyOnly)) == EnvironmentValueAccess.None)
        {
            throw new InvalidOperationException("SSH environment value access cannot contain only audit flags.");
        }

        if (access.HasFlag(EnvironmentValueAccess.Hidden)
            && (access & ~EnvironmentValueAccess.Hidden) != EnvironmentValueAccess.None)
        {
            throw new InvalidOperationException("SSH environment value access cannot combine Hidden with other flags.");
        }

        if (access.HasFlag(EnvironmentValueAccess.KeyOnly)
            && (access & (permissions | EnvironmentValueAccess.Masked)) != EnvironmentValueAccess.None)
        {
            throw new InvalidOperationException("SSH environment value access cannot combine KeyOnly with peek, set, or masked flags.");
        }

        if (access.HasFlag(EnvironmentValueAccess.Masked)
            && (access & permissions) != EnvironmentValueAccess.None)
        {
            throw new InvalidOperationException("SSH environment value access cannot combine Masked with real-value peek or set flags.");
        }

        if ((access & (EnvironmentValueAccess.PeekCommon | EnvironmentValueAccess.SetCommon)) != EnvironmentValueAccess.None
            && (access & (EnvironmentValueAccess.PeekSecret | EnvironmentValueAccess.SetSecret)) != EnvironmentValueAccess.None)
        {
            throw new InvalidOperationException("SSH environment value access cannot combine Common and Secret flags.");
        }

        _ = auditOnly;
    }
}
