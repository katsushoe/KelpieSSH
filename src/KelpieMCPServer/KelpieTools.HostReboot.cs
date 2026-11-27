using System.ComponentModel;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>Previews a normal reboot without changing the remote host.</summary>
    [McpServerTool(Name = "ssh_host_reboot_preview")]
    [Description("Previews a normal host reboot and its configured checks. Show the exact target and impact to the human administrator and obtain explicit approval before requesting reboot.")]
    public static Task<HostRebootOperation> PreviewHostRebootAsync(HostRebootService service,
        ISshConnectionProfileCatalog profileCatalog, string profileName, CancellationToken cancellationToken = default)
        => service.PreviewAsync(ResolveSshProfile(profileCatalog, profileName), cancellationToken);

    /// <summary>Submits an approved operation once, never automatically retrying reboot.</summary>
    [McpServerTool(Name = "ssh_host_reboot_request")]
    [Description("Requests exactly one normal reboot for an approved preview. Requires explicit human administrator approval of the target and downtime. Pass the preview operationId and exact confirmation token. A disconnect is Unknown; inspect the same ID, never create a replacement request automatically.")]
    public static Task<HostRebootOperation> RequestHostRebootAsync(HostRebootService service,
        ISshConnectionProfileCatalog profileCatalog, string profileName, string operationId,
        string confirmation, CancellationToken cancellationToken = default)
        => service.RequestAsync(ResolveSshProfile(profileCatalog, profileName), operationId, confirmation, cancellationToken);

    /// <summary>Inspects one reboot operation without dispatching commands that change the host.</summary>
    [McpServerTool(Name = "ssh_host_reboot_inspect")]
    [Description("Observes the same pinned host after reboot without resending reboot. wait=true polls within the original configured deadline. Verifies changed boot ID, expected kernel, configured systemd services and HTTP endpoints.")]
    public static Task<HostRebootOperation> InspectHostRebootAsync(HostRebootService service,
        ISshConnectionProfileCatalog profileCatalog, string profileName, string operationId,
        bool wait = false, CancellationToken cancellationToken = default)
        => service.InspectAsync(ResolveSshProfile(profileCatalog, profileName), operationId, wait, cancellationToken);
}
