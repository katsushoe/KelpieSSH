namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines per-environment-variable access flags.
/// </summary>
[Flags]
public enum EnvironmentValueAccess
{
    /// <summary>
    /// No explicit access.
    /// </summary>
    None = 0,

    /// <summary>
    /// Allows reading a common environment variable value.
    /// </summary>
    PeekCommon = 1 << 0,

    /// <summary>
    /// Allows setting a common environment variable value for one command execution.
    /// </summary>
    SetCommon = 1 << 1,

    /// <summary>
    /// Allows reading a secret environment variable value.
    /// </summary>
    PeekSecret = 1 << 2,

    /// <summary>
    /// Allows setting a secret environment variable value for one command execution.
    /// </summary>
    SetSecret = 1 << 3,

    /// <summary>
    /// Hides the key and value.
    /// </summary>
    Hidden = 1 << 4,

    /// <summary>
    /// Allows only masked value metadata.
    /// </summary>
    Masked = 1 << 5,

    /// <summary>
    /// Allows only the key name.
    /// </summary>
    KeyOnly = 1 << 6,

    /// <summary>
    /// Writes a warning audit log when the value is read or masked.
    /// </summary>
    PeekLog = 1 << 7,

    /// <summary>
    /// Writes a warning audit log when the value is set.
    /// </summary>
    SetLog = 1 << 8,

    /// <summary>
    /// Suppresses normal access logs.
    /// </summary>
    NoLog = 1 << 9,
}

/// <summary>
/// Represents one environment variable access rule.
/// </summary>
/// <param name="Key">The environment variable key.</param>
/// <param name="Access">The access flags.</param>
public sealed record EnvironmentValueRule(string Key, EnvironmentValueAccess Access)
{
    /// <summary>
    /// Gets a value indicating whether the key should be hidden from callers.
    /// </summary>
    public bool IsHidden => Access.HasFlag(EnvironmentValueAccess.Hidden);

    /// <summary>
    /// Gets a value indicating whether a real value peek is allowed.
    /// </summary>
    public bool AllowsPeekValue => Access.HasFlag(EnvironmentValueAccess.PeekCommon)
        || Access.HasFlag(EnvironmentValueAccess.PeekSecret);

    /// <summary>
    /// Gets a value indicating whether setting a value is allowed.
    /// </summary>
    public bool AllowsSetValue => Access.HasFlag(EnvironmentValueAccess.SetCommon)
        || Access.HasFlag(EnvironmentValueAccess.SetSecret);
}
