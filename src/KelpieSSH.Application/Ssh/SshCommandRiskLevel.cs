namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines the execution risk level for an allowed SSH command.
/// </summary>
public enum SshCommandRiskLevel
{
    /// <summary>
    /// Read-only command that may run without extra confirmation.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Command that must request user confirmation before execution.
    /// </summary>
    ConfirmRequired,

    /// <summary>
    /// Command that must never be executed.
    /// </summary>
    Forbidden,
}
