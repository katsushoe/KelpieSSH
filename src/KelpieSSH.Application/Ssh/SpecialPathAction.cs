namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines how a special path pattern affects a file operation.
/// </summary>
public enum SpecialPathAction
{
    /// <summary>
    /// The operation is denied.
    /// </summary>
    Deny,

    /// <summary>
    /// The operation requires confirmation.
    /// </summary>
    Confirm,

    /// <summary>
    /// The operation falls back to normal allowed-root and policy checks.
    /// </summary>
    Allow,
}
