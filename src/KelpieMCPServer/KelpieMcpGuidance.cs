using System.ComponentModel;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

/// <summary>
/// Provides server-wide guidance and reusable prompts for AI clients.
/// </summary>
[McpServerPromptType]
public static class KelpieMcpGuidance
{
    /// <summary>
    /// Gets the instructions announced when an MCP client initializes the server.
    /// </summary>
    public const string ServerInstructions = """
        KelpieSSH helps an AI diagnose and maintain configured rental VPS hosts through constrained SSH operations.
        Use profile and capability discovery before selecting a target or operation. Prefer dedicated read-only diagnostic tools over terminal or generic command execution.
        Treat each profile's Mode, AllowedRoots, SpecialPaths, and tool result as authoritative. MCP clients cannot use CLI-only Policies. Never request, reveal, persist, or echo passwords, private keys, passphrases, secret values, or raw sensitive configuration.
        Start with ssh_get_capabilities for a known profile, or get_target_inventory when target discovery is needed. Use check, preview, or simulation tools before a modifying tool when available. Explain the intended target and impact before a state-changing operation, and do not bypass confirmation, approval, trust-store, timeout, audit, or rollback controls.
        Root login and unrestricted shell access are outside KelpieSSH's safety model. If a requested action is not exposed or permitted, report the limitation and suggest the minimum safe configuration or dedicated operation required.
        """;

    /// <summary>
    /// Creates a prompt that teaches an AI how to begin a safe KelpieSSH task.
    /// </summary>
    /// <param name="task">Optional operator goal to incorporate into the workflow.</param>
    /// <returns>The prompt text.</returns>
    [McpServerPrompt(Name = "kelpie_get_started")]
    [Description("Create a safe KelpieSSH workflow for diagnosing or maintaining a configured VPS.")]
    public static string GetStarted(
        [Description("Optional diagnosis or maintenance goal. Do not include credentials or secret values.")]
        string? task = null)
    {
        var goal = string.IsNullOrWhiteSpace(task)
            ? "Determine the operator's diagnosis or maintenance goal."
            : $"Operator goal: {task.Trim()}";

        return $$"""
            You are using KelpieSSH, a safety-focused MCP server for AI-assisted diagnosis and maintenance of configured rental VPS hosts.

            {{goal}}

            Follow this workflow:
            1. Discover the target with get_target_inventory when the profile is not already known.
            2. Call ssh_get_capabilities for the selected profile and user before planning operations.
            3. Prefer dedicated read-only diagnostic tools and gather only the information needed for the goal.
            4. Summarize evidence separately from assumptions. Do not expose credentials, private keys, passphrases, secret values, or sensitive raw configuration.
            5. Before any change, use the available check, preview, or simulation tool, explain the target and impact, and obtain any approval required by the client or tool.
            6. Respect Mode, AllowedRoots, SpecialPaths, trust checks, timeouts, audit records, and rollback contracts. Never attempt to bypass a denied or unavailable operation.
            7. Verify the resulting state with a read-only tool and report what changed, what was verified, and any remaining risk.

            If KelpieSSH does not expose or permit the requested action, stop and state the limitation. Recommend the minimum safe profile change or dedicated KelpieSSH operation instead of using unrestricted shell commands.
            """;
    }
}
