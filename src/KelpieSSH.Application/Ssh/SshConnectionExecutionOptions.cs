namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents SSH execution policy configuration values for one profile.
/// </summary>
public sealed class SshConnectionExecutionOptions
{
    /// <summary>
    /// Gets or sets the execution policy name.
    /// </summary>
    public string Policy { get; set; } = string.Empty;
}
