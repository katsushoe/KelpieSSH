using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>Returns fresh isolated PHP/Node information without loading application configuration.</summary>
    [McpServerTool(Name = "ssh_runtime_info", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Reads isolated PHP 8 or Node 22/24 version and built-in JSON/SHA-256 features. Capability registration does not prove runtime installation. Does not run application code.")]
    public static async Task<RuntimeDiagnosticResult> GetRuntimeInfoAsync(
        SshCommandService sshCommandService, ISshConnectionProfileCatalog profileCatalog,
        string profileName, string runtime, bool refresh = false, CancellationToken cancellationToken = default)
    {
        KpLog.Info("MCP runtime.info requested.");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await new RuntimeDiagnosticService(sshCommandService).GetInfoAsync(profile, runtime, refresh,
            KelpieExecutionChannel.Mcp, cancellationToken);
    }
}
