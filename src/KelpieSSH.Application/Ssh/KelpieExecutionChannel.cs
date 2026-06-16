namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines the channel that requested an SSH command execution.
/// </summary>
public enum KelpieExecutionChannel
{
    /// <summary>
    /// Command requested by the local Kelpie CLI.
    /// </summary>
    Cli,

    /// <summary>
    /// Command requested through MCP.
    /// </summary>
    Mcp,
}
