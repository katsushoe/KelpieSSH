using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>Exports a remote file directly to a local protected destination.</summary>
    [McpServerTool(Name = "ssh_file_export")]
    [Description("Exports an @Read and @Export authorized remote regular file directly to a local file without returning its content.")]
    public static async Task<SshFileExportResult> ExportSshFileAsync(
        ISshConnectionProfileCatalog profileCatalog,
        ISshFileExporter fileExporter,
        string profileName,
        string remotePath,
        string localPath,
        bool confirmSpecialPath = false,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_file_export profile={profileName}, remotePath={remotePath}, localPath={localPath}, confirmSpecialPath={confirmSpecialPath}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await fileExporter.ExportAsync(profile, remotePath, localPath, confirmSpecialPath, cancellationToken);
    }
}
