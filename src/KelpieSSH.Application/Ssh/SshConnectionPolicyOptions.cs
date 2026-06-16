namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents execution policy configuration values for one SSH profile.
/// </summary>
public sealed class SshConnectionPolicyOptions
{
    /// <summary>
    /// Gets or sets the execution policy level.
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the allowed root path or glob patterns.
    /// </summary>
    public IReadOnlyCollection<string> AllowedRoots { get; set; } = [];
}
