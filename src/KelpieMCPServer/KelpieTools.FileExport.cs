using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;
using Renci.SshNet.Common;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>Exports a remote file directly to a local protected destination.</summary>
    [McpServerTool(Name = "ssh_file_export")]
    [Description("Exports an @Read and @Export authorized remote regular file directly to a local file without returning its content.")]
    public static async Task<SshFileExportToolResult> ExportSshFileAsync(
        ISshConnectionProfileCatalog profileCatalog,
        ISshFileExporter fileExporter,
        string profileName,
        string remotePath,
        string localPath,
        bool confirmSpecialPath = false,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_file_export profile={profileName}, remotePath={remotePath}, localPath={localPath}, confirmSpecialPath={confirmSpecialPath}");
        try
        {
            var profile = ResolveSshProfile(profileCatalog, profileName);
            var data = await fileExporter.ExportAsync(
                profile,
                remotePath,
                localPath,
                confirmSpecialPath,
                cancellationToken);
            return new SshFileExportToolResult(true, data, null, null);
        }
        catch (KelpiePolicyError ex)
        {
            return new SshFileExportToolResult(
                false,
                null,
                ex.Message,
                new SshToolErrorInfo(
                    "KELPIE_POLICY_FILE_EXPORT_DENIED",
                    "PolicyDenied",
                    "The requested SSH file export is denied by the current file policy.",
                    ex.Message,
                    Retryable: false));
        }
        catch (SshConnectionException ex)
        {
            return new SshFileExportToolResult(
                false,
                null,
                ex.Message,
                new SshToolErrorInfo(
                    "KELPIE_SSH_FILE_EXPORT_FAILED",
                    "Connection",
                    "The SSH file export failed before a local file was completed.",
                    "Check the remote file permissions, SSH authentication, and connection state.",
                    Retryable: true));
        }
    }

    /// <summary>Represents a metadata-only SSH file export result or a structured failure.</summary>
    public sealed record SshFileExportToolResult(
        bool Ok,
        SshFileExportResult? Data,
        string? Error,
        SshToolErrorInfo? ErrorInfo);
}
