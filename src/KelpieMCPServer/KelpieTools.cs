using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

/// <summary>
/// Provides basic MCP tools exposed by KelpieSSH.
/// </summary>
[McpServerToolType]
public sealed partial class KelpieTools
{
    /// <summary>
    /// Returns a small response that can be used to verify the MCP server is reachable.
    /// </summary>
    /// <returns>A short status message.</returns>
    [McpServerTool(Name = "kelpie_ping")]
    [Description("Verifies that the KelpieSSH MCP server is running.")]
    public static string Ping()
    {
        KpLog.Debug("MCP tool called: kelpie_ping");
        return "KelpieSSH MCP server is running.";
    }

    /// <summary>
    /// Returns basic information about the local system running KelpieMCPServer.
    /// </summary>
    /// <returns>The local system information.</returns>
    [McpServerTool(Name = "get_system_info")]
    [Description("Returns basic OS, runtime, machine, and process information for the local KelpieMCPServer host.")]
    public static SystemInfoResult GetSystemInfo()
    {
        KpLog.Debug("MCP tool called: get_system_info");

        return new SystemInfoResult(
            Environment.MachineName,
            Environment.UserName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            Environment.ProcessorCount,
            Environment.Is64BitOperatingSystem,
            Environment.Is64BitProcess,
            Environment.ProcessId,
            AppContext.BaseDirectory);
    }

    /// <summary>
    /// Returns local disk usage for ready drives.
    /// </summary>
    /// <returns>The local disk usage information.</returns>
    [McpServerTool(Name = "get_disk_usage")]
    [Description("Returns disk usage for ready local drives on the KelpieMCPServer host.")]
    public static DiskUsageResult GetDiskUsage()
    {
        KpLog.Debug("MCP tool called: get_disk_usage");

        var drives = DriveInfo.GetDrives()
            .Where(drive => drive.IsReady)
            .Select(drive => new DiskUsageItem(
                drive.Name,
                drive.DriveType.ToString(),
                drive.DriveFormat,
                drive.TotalSize,
                drive.AvailableFreeSpace,
                drive.TotalFreeSpace,
                drive.TotalSize - drive.TotalFreeSpace))
            .ToArray();

        return new DiskUsageResult(drives);
    }

    /// <summary>
    /// Returns memory usage for the local KelpieMCPServer process and managed runtime.
    /// </summary>
    /// <returns>The local memory usage information.</returns>
    [McpServerTool(Name = "get_memory_usage")]
    [Description("Returns process and managed runtime memory usage for KelpieMCPServer.")]
    public static MemoryUsageResult GetMemoryUsage()
    {
        KpLog.Debug("MCP tool called: get_memory_usage");

        using var process = Process.GetCurrentProcess();
        var gcMemoryInfo = GC.GetGCMemoryInfo();

        return new MemoryUsageResult(
            process.WorkingSet64,
            process.PrivateMemorySize64,
            process.VirtualMemorySize64,
            GC.GetTotalMemory(forceFullCollection: false),
            gcMemoryInfo.HeapSizeBytes,
            gcMemoryInfo.HighMemoryLoadThresholdBytes,
            gcMemoryInfo.MemoryLoadBytes,
            gcMemoryInfo.TotalAvailableMemoryBytes);
    }

    /// <summary>
    /// Returns local listening TCP/UDP ports.
    /// </summary>
    /// <returns>The local listening port information.</returns>
    [McpServerTool(Name = "get_listening_ports")]
    [Description("Returns local listening TCP/UDP ports from the KelpieMCPServer host.")]
    public static async Task<ListeningPortsResult> GetListeningPortsAsync()
    {
        KpLog.Debug("MCP tool called: get_listening_ports");

        var command = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new CommandSpec("netstat.exe", "-ano")
            : new CommandSpec("ss", "-lntu");
        var output = await RunCommandAsync(command);

        var ports = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ParseWindowsNetstat(output.StandardOutput)
            : ParseUnixSs(output.StandardOutput);

        return new ListeningPortsResult(command.FileName, command.Arguments, output.ExitCode, output.StandardError, ports);
    }

    /// <summary>
    /// Runs an allowed SSH command against a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="commandName">The allowed SSH command name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="arguments">The optional command arguments.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_run_allowed_command")]
    [Description("Runs one allowed read-only diagnostic command against a configured SSH profile.")]
    public static async Task<SshToolResult> RunAllowedSshCommandAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string commandName,
        string profileName,
        IReadOnlyDictionary<string, string>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_run_allowed_command command={commandName}, profile={profileName}");
        if (commandName.StartsWith("service_config_", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRejectedSshToolResult(
                profileName,
                commandName,
                "Service config commands must be called through service_config_* tools.");
        }

        if (commandName.StartsWith("service_logfile_", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRejectedSshToolResult(
                profileName,
                commandName,
                "Service logfile commands must be called through service_logfile_* tools.");
        }

        if (commandName.StartsWith("web_public_file_", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRejectedSshToolResult(
                profileName,
                commandName,
                "Web public file commands must be called through web_file_* tools.");
        }

        if (commandName.StartsWith("web_change_", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRejectedSshToolResult(
                profileName,
                commandName,
                "Web permission commands must be called through web_change_* tools.");
        }

        if (string.Equals(commandName, "support_report_collect", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRejectedSshToolResult(
                profileName,
                commandName,
                "Support report commands must be called through ssh_support_report_collect.");
        }

        if (commandName.StartsWith("audit_", StringComparison.OrdinalIgnoreCase))
        {
            return CreateRejectedSshToolResult(
                profileName,
                commandName,
                "Audit commands must be called through ssh_audit_* tools.");
        }

        if (IsDedicatedConfirmationCommand(commandName))
        {
            return CreateRejectedSshToolResult(
                profileName,
                commandName,
                "Confirmation-required maintenance commands must be called through their dedicated MCP tools.");
        }

        try
        {
            return await ExecuteSshCommandAsync(
                sshCommandService,
                profileCatalog,
                commandName,
                arguments,
                profileName,
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsUnknownAllowedCommandError(ex))
        {
            return CreateRejectedSshToolResult(profileName, commandName, ex.Message);
        }
    }

    /// <summary>
    /// Runs one SSH remote operation without requiring a saved profile name.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="operation">The remote operation request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH remote operation result.</returns>
    [McpServerTool(Name = "ssh_run_remote_operation")]
    [Description("Runs one SSH remote operation from endpoint, credential, policy, operation, and options inputs.")]
    public static async Task<SshRemoteOperationToolResult> RunRemoteOperationAsync(
        SshCommandService sshCommandService,
        SshRemoteOperation operation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_run_remote_operation operation={operation.Operation.Name}, correlationId={operation.Options?.CorrelationId ?? string.Empty}");
        var result = await sshCommandService.ExecuteAsync(
            operation,
            KelpieExecutionChannel.Mcp,
            cancellationToken);

        return CreateRemoteOperationToolResult(operation, result);
    }

    /// <summary>
    /// Opens an interactive SSH terminal session and returns the initial screen snapshot.
    /// </summary>
    /// <param name="terminalSessionManager">The terminal session manager.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="columns">The terminal columns.</param>
    /// <param name="rows">The terminal rows.</param>
    /// <param name="pixelWidth">The terminal pixel width.</param>
    /// <param name="pixelHeight">The terminal pixel height.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened terminal screen snapshot.</returns>
    [McpServerTool(Name = "ssh_terminal_open")]
    [Description("Opens an interactive SSH terminal session and returns the initial rendered screen snapshot.")]
    public static async Task<SshTerminalSnapshotResult> OpenSshTerminalAsync(
        SshTerminalSessionManager terminalSessionManager,
        string profileName,
        int columns = 120,
        int rows = 40,
        int pixelWidth = 1200,
        int pixelHeight = 800,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_terminal_open profile={profileName}");
        return await terminalSessionManager.OpenAsync(
            profileName,
            columns,
            rows,
            pixelWidth,
            pixelHeight,
            cancellationToken);
    }

    /// <summary>
    /// Sends raw input to an interactive SSH terminal session and returns the updated screen snapshot.
    /// </summary>
    /// <param name="terminalSessionManager">The terminal session manager.</param>
    /// <param name="handle">The terminal session handle.</param>
    /// <param name="input">The raw terminal input.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated terminal screen snapshot.</returns>
    [McpServerTool(Name = "ssh_terminal_send")]
    [Description("Sends raw input to an interactive SSH terminal session and returns the updated rendered screen snapshot.")]
    public static async Task<SshTerminalSnapshotResult> SendSshTerminalInputAsync(
        SshTerminalSessionManager terminalSessionManager,
        string handle,
        string input,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_terminal_send handle={handle}");
        return await terminalSessionManager.SendAsync(handle, input, cancellationToken);
    }

    /// <summary>
    /// Returns the current screen snapshot for an interactive SSH terminal session.
    /// </summary>
    /// <param name="terminalSessionManager">The terminal session manager.</param>
    /// <param name="handle">The terminal session handle.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The current terminal screen snapshot.</returns>
    [McpServerTool(Name = "ssh_terminal_snapshot")]
    [Description("Returns the current rendered screen snapshot for an interactive SSH terminal session.")]
    public static async Task<SshTerminalSnapshotResult> GetSshTerminalSnapshotAsync(
        SshTerminalSessionManager terminalSessionManager,
        string handle,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_terminal_snapshot handle={handle}");
        return await terminalSessionManager.SnapshotAsync(handle, cancellationToken);
    }

    /// <summary>
    /// Closes an interactive SSH terminal session.
    /// </summary>
    /// <param name="terminalSessionManager">The terminal session manager.</param>
    /// <param name="handle">The terminal session handle.</param>
    /// <returns>The close result.</returns>
    [McpServerTool(Name = "ssh_terminal_close")]
    [Description("Closes an interactive SSH terminal session.")]
    public static async Task<SshTerminalCloseResult> CloseSshTerminalAsync(
        SshTerminalSessionManager terminalSessionManager,
        string handle)
    {
        KpLog.Info($"MCP SSH tool called: ssh_terminal_close handle={handle}");
        return await terminalSessionManager.CloseAsync(handle);
    }

    /// <summary>
    /// Returns system information from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_system_info")]
    [Description("Runs the allowed get_system_info command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshSystemInfoAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_system_info profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_system_info",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns OS release information from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_os_release")]
    [Description("Runs the allowed get_os_release command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshOsReleaseAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_os_release profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_os_release",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns uptime information from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_uptime")]
    [Description("Runs the allowed get_uptime command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshUptimeAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_uptime profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_uptime",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns disk usage from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_disk_usage")]
    [Description("Runs the allowed get_disk_usage command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshDiskUsageAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_disk_usage profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_disk_usage",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns memory usage from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_memory_usage")]
    [Description("Runs the allowed get_memory_usage command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshMemoryUsageAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_memory_usage profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_memory_usage",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns a process summary from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="sortBy">The process sort key: cpu or memory.</param>
    /// <param name="limit">The maximum number of process rows to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_process_summary")]
    [Description("Runs the allowed get_process_summary command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshProcessSummaryAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string sortBy = "cpu",
        string limit = "10",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_process_summary sortBy={sortBy}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_process_summary",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sortBy"] = sortBy,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns inode usage from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_inode_usage")]
    [Description("Runs the allowed get_inode_usage command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshInodeUsageAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_inode_usage profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_inode_usage",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns mount information from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_mounts")]
    [Description("Runs the allowed get_mounts command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshMountsAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_mounts profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_mounts",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns network addresses from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_network_addresses")]
    [Description("Runs the allowed get_network_addresses command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshNetworkAddressesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_network_addresses profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_network_addresses",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns route information from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_routes")]
    [Description("Runs the allowed get_routes command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshRoutesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_routes profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_routes",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns DNS resolver configuration from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_dns_config")]
    [Description("Runs the allowed get_dns_config command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshDnsConfigAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_dns_config profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_dns_config",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks local HTTP response on a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="port">The local TCP port.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_check_http_local")]
    [Description("Checks an HTTP response from 127.0.0.1 on a configured SSH profile with a validated port.")]
    public static async Task<SshToolResult> CheckSshHttpLocalAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string port,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_check_http_local port={port}, profile={profileName}");
        return await ExecuteLocalPortCommandAsync(
            sshCommandService,
            profileCatalog,
            "check_http_local",
            port,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks local TCP connectivity on a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="port">The local TCP port.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_check_tcp_connect_local")]
    [Description("Checks TCP connectivity to 127.0.0.1 on a configured SSH profile with a validated port.")]
    public static async Task<SshToolResult> CheckSshTcpConnectLocalAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string port,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_check_tcp_connect_local port={port}, profile={profileName}");
        return await ExecuteLocalPortCommandAsync(
            sshCommandService,
            profileCatalog,
            "check_tcp_connect_local",
            port,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns system and current-user cron entries from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="limit">The maximum number of cron entries to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_cron_list")]
    [Description("Lists system and current-user cron entries with a bounded result limit.")]
    public static async Task<SshToolResult> ListSshCronAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string limit = "100",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_cron_list limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "cron_list",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Validates cron settings without writing them.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cronExpression">The five-field cron expression.</param>
    /// <param name="runUser">The intended run user.</param>
    /// <param name="command">The command text to validate.</param>
    /// <param name="logPath">The intended log path under /var/log.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_cron_validate")]
    [Description("Validates a cron expression, run user, command text, and /var/log path without changing cron files.")]
    public static async Task<SshToolResult> ValidateSshCronAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string cronExpression,
        string runUser,
        string command,
        string logPath,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_cron_validate runUser={runUser}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "cron_validate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cronExpression"] = cronExpression,
                ["runUser"] = runUser,
                ["command"] = command,
                ["logPath"] = logPath,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Inspects a certificate file from a constrained certificate path.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="path">The certificate file path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_cert_inspect")]
    [Description("Inspects issuer, subject, dates, and SAN for a certificate under approved certificate directories.")]
    public static async Task<SshToolResult> InspectSshCertificateAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string path,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_cert_inspect path={path}, profile={profileName}");
        return await ExecuteCertificateCommandAsync(
            sshCommandService,
            profileCatalog,
            "cert_inspect",
            path,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks whether a certificate remains valid for a number of days.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="path">The certificate file path.</param>
    /// <param name="days">The required remaining validity in days.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_cert_expiry_check")]
    [Description("Checks whether a certificate under approved certificate directories is valid for the requested number of days.")]
    public static async Task<SshToolResult> CheckSshCertificateExpiryAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string path,
        string days = "30",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_cert_expiry_check days={days}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "cert_expiry_check",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = path,
                ["days"] = days,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Lists local users from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="limit">The maximum number of users to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_list")]
    [Description("Lists local users with UID, GID, home directory, and shell using a bounded result limit.")]
    public static async Task<SshToolResult> ListSshUsersAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string limit = "100",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_list limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_list",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns local user details from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="user">The local user name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_info")]
    [Description("Returns UID, GID, primary group, supplementary groups, home, and shell for one local user.")]
    public static async Task<SshToolResult> GetSshUserInfoAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string user,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_info user={user}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_info",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Lists local groups from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="limit">The maximum number of groups to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_group_list")]
    [Description("Lists local groups with GID and member names using a bounded result limit.")]
    public static async Task<SshToolResult> ListSshGroupsAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string limit = "100",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_group_list limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "group_list",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns local group details from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="group">The local group name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_group_info")]
    [Description("Returns GID and member names for one local group.")]
    public static async Task<SshToolResult> GetSshGroupInfoAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string group,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_group_info group={group}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "group_info",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["group"] = group,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks summarized sudoers evidence for one user or group without returning sudoers content.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="targetType">The principal type: user or group.</param>
    /// <param name="name">The user or group name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_sudoers_check")]
    [Description("Summarizes whether one user or group has sudoers evidence without returning sudoers file content.")]
    public static async Task<SshToolResult> CheckSshSudoersAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string targetType,
        string name,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_sudoers_check targetType={targetType}, name={name}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "sudoers_check",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetType"] = targetType,
                ["name"] = name,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Summarizes whether a user or group is used by services, cron, or common owned paths.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="targetType">The target principal type: user or group.</param>
    /// <param name="name">The user or group name.</param>
    /// <param name="limit">The bounded service, file, and source limit.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_usage_check")]
    [Description("Checks whether one user or group is referenced by services, cron owners, or common owned paths with bounded output.")]
    public static async Task<SshToolResult> CheckSshUserUsageAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string targetType,
        string name,
        string limit = "50",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_usage_check targetType={targetType}, name={name}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_usage_check",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetType"] = targetType,
                ["name"] = name,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks bounded file ownership under an approved scan root.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="targetType">The target principal type: user or group.</param>
    /// <param name="name">The user or group name.</param>
    /// <param name="scanRoot">The approved absolute scan root.</param>
    /// <param name="depth">The maximum scan depth from the root.</param>
    /// <param name="limit">The maximum matching entries to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_file_ownership_check")]
    [Description("Checks whether one user or group owns files under an approved root using a bounded non-following scan.")]
    public static async Task<SshToolResult> CheckSshUserFileOwnershipAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string targetType,
        string name,
        string scanRoot,
        string depth = "2",
        string limit = "50",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_file_ownership_check targetType={targetType}, name={name}, scanRoot={scanRoot}, depth={depth}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_file_ownership_check",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetType"] = targetType,
                ["name"] = name,
                ["scanRoot"] = scanRoot,
                ["depth"] = depth,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks systemd service user and group references.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="targetType">The target principal type: user or group.</param>
    /// <param name="name">The user or group name.</param>
    /// <param name="limit">The maximum service units to inspect.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_service_usage_check")]
    [Description("Checks systemd User, Group, and SupplementaryGroups references for one user or group.")]
    public static async Task<SshToolResult> CheckSshUserServiceUsageAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string targetType,
        string name,
        string limit = "50",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_service_usage_check targetType={targetType}, name={name}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_service_usage_check",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetType"] = targetType,
                ["name"] = name,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks common residual files and directories for one service.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="limit">The maximum paths to report.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_service_residual_config_check")]
    [Description("Checks common service unit, config, log, data, and runtime residual paths without reading file contents.")]
    public static async Task<SshToolResult> CheckSshServiceResidualConfigAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string service,
        string limit = "50",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_service_residual_config_check service={service}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "service_residual_config_check",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["service"] = service,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns listening ports from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_listening_ports")]
    [Description("Runs the allowed get_listening_ports command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshListeningPortsAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_listening_ports profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_listening_ports",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns failed services from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_failed_services")]
    [Description("Runs the allowed get_failed_services command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshFailedServicesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_failed_services profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_failed_services",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns recent journal lines from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="lines">The number of journal lines to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_get_journal_recent")]
    [Description("Runs the allowed get_journal_recent command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshJournalRecentAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string lines = "50",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_journal_recent lines={lines}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "get_journal_recent",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["lines"] = lines,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Collects a sanitized support report from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="limit">The maximum number of bounded report rows to return per section.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_support_report_collect")]
    [Description("Collects a sanitized read-only support report without host names, IP addresses, usernames, or file contents.")]
    public static async Task<SshToolResult> CollectSshSupportReportAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string limit = "20",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_support_report_collect limit={limit}, profile={profileName}");
        var result = await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "support_report_collect",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);

        return result with
        {
            Host = string.Empty,
            Port = 0,
            UserName = string.Empty,
        };
    }

    /// <summary>
    /// Checks a cron write request without changing cron files.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="targetType">The cron target type: user or system.</param>
    /// <param name="runUser">The user that will run the cron entry.</param>
    /// <param name="cronExpression">The five-field cron expression.</param>
    /// <param name="command">The command text to validate.</param>
    /// <param name="logPath">The /var/log path for command output.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_cron_check_write")]
    [Description("Checks cron write inputs, target, confirmation token, and rollback support without changing cron files.")]
    public static async Task<SshToolResult> CheckSshCronWriteAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string targetType,
        string runUser,
        string cronExpression,
        string command,
        string logPath,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_cron_check_write targetType={targetType}, runUser={runUser}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "cron_check_write",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetType"] = targetType,
                ["runUser"] = runUser,
                ["cronExpression"] = cronExpression,
                ["command"] = command,
                ["logPath"] = logPath,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Writes one managed cron entry after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="targetType">The cron target type: user or system.</param>
    /// <param name="runUser">The user that will run the cron entry.</param>
    /// <param name="cronExpression">The five-field cron expression.</param>
    /// <param name="command">The command text to write.</param>
    /// <param name="logPath">The /var/log path for command output.</param>
    /// <param name="confirmation">The required confirmation token: cron_write:&lt;targetType&gt;:&lt;runUser&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_cron_write")]
    [Description("Writes one managed cron entry after explicit confirmation and creates a rollback backup.")]
    public static async Task<SshToolResult> WriteSshCronAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string targetType,
        string runUser,
        string cronExpression,
        string command,
        string logPath,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_cron_write targetType={targetType}, runUser={runUser}, profile={profileName}");
        if (!TryGetConfirmationError("cron_write", targetType + ":" + runUser, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "cron_write", confirmationError);
        }

        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "cron_write",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetType"] = targetType,
                ["runUser"] = runUser,
                ["cronExpression"] = cronExpression,
                ["command"] = command,
                ["logPath"] = logPath,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Restores the latest managed cron backup after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="targetType">The cron target type: user or system.</param>
    /// <param name="runUser">The user associated with the managed cron entry.</param>
    /// <param name="confirmation">The required confirmation token: cron_rollback:&lt;targetType&gt;:&lt;runUser&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_cron_rollback")]
    [Description("Restores the latest Kelpie-managed cron backup after explicit confirmation.")]
    public static async Task<SshToolResult> RollbackSshCronAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string targetType,
        string runUser,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_cron_rollback targetType={targetType}, runUser={runUser}, profile={profileName}");
        if (!TryGetConfirmationError("cron_rollback", targetType + ":" + runUser, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "cron_rollback", confirmationError);
        }

        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "cron_rollback",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["targetType"] = targetType,
                ["runUser"] = runUser,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks a user supplementary group change without changing the user.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="user">The user name.</param>
    /// <param name="groups">The comma-separated supplementary group list.</param>
    /// <param name="mode">The change mode: append or replace.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_check_group_change")]
    [Description("Checks a user supplementary group change and returns the diff and confirmation token without applying it.")]
    public static async Task<SshToolResult> CheckSshUserGroupChangeAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string user,
        string groups,
        string mode = "append",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_check_group_change user={user}, mode={mode}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_check_group_change",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user,
                ["groups"] = groups,
                ["mode"] = mode,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Applies a user supplementary group change after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="user">The user name.</param>
    /// <param name="groups">The comma-separated supplementary group list.</param>
    /// <param name="mode">The change mode: append or replace.</param>
    /// <param name="confirmation">The required confirmation token: user_apply_group_change:&lt;user&gt;:&lt;mode&gt;:&lt;groups&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_apply_group_change")]
    [Description("Applies a user supplementary group change after explicit confirmation.")]
    public static async Task<SshToolResult> ApplySshUserGroupChangeAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string user,
        string groups,
        string mode,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_apply_group_change user={user}, mode={mode}, profile={profileName}");
        if (!TryGetConfirmationError("user_apply_group_change", user + ":" + mode + ":" + groups, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "user_apply_group_change", confirmationError);
        }

        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_apply_group_change",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user,
                ["groups"] = groups,
                ["mode"] = mode,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Restores the latest user supplementary group backup after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="user">The user name.</param>
    /// <param name="confirmation">The required confirmation token: user_rollback_group_change:&lt;user&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_rollback_group_change")]
    [Description("Restores the latest user supplementary group backup after explicit confirmation.")]
    public static async Task<SshToolResult> RollbackSshUserGroupChangeAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string user,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_rollback_group_change user={user}, profile={profileName}");
        if (!TryGetConfirmationError("user_rollback_group_change", user, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "user_rollback_group_change", confirmationError);
        }

        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_rollback_group_change",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks a user permission change without changing the user.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="user">The user name.</param>
    /// <param name="shell">The requested login shell.</param>
    /// <param name="login">The requested login state: enabled, disabled, or unchanged.</param>
    /// <param name="sudo">The requested sudo evidence state: present, absent, or unchanged.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_check_permission_change")]
    [Description("Checks a user shell, login, or sudo permission change without applying it.")]
    public static async Task<SshToolResult> CheckSshUserPermissionChangeAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string user,
        string shell,
        string login = "unchanged",
        string sudo = "unchanged",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_check_permission_change user={user}, login={login}, sudo={sudo}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_check_permission_change",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user,
                ["shell"] = shell,
                ["login"] = login,
                ["sudo"] = sudo,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Applies a user shell, login, or sudo permission change after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="user">The user name.</param>
    /// <param name="shell">The requested login shell.</param>
    /// <param name="login">The requested login state: enabled, disabled, or unchanged.</param>
    /// <param name="sudo">The requested sudo evidence state: present, absent, or unchanged.</param>
    /// <param name="confirmation">The required confirmation token: user_apply_permission_change:&lt;user&gt;:&lt;shell&gt;:&lt;login&gt;:&lt;sudo&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_apply_permission_change")]
    [Description("Applies a user shell, login, or sudo permission change after explicit confirmation.")]
    public static async Task<SshToolResult> ApplySshUserPermissionChangeAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string user,
        string shell,
        string login,
        string sudo,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_apply_permission_change user={user}, login={login}, sudo={sudo}, profile={profileName}");
        if (!TryGetConfirmationError("user_apply_permission_change", user + ":" + shell + ":" + login + ":" + sudo, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "user_apply_permission_change", confirmationError);
        }

        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_apply_permission_change",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user,
                ["shell"] = shell,
                ["login"] = login,
                ["sudo"] = sudo,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Restores the latest user permission backup after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="user">The user name.</param>
    /// <param name="confirmation">The required confirmation token: user_rollback_permission_change:&lt;user&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_user_rollback_permission_change")]
    [Description("Restores the latest user shell, login, and managed sudo permission backup after explicit confirmation.")]
    public static async Task<SshToolResult> RollbackSshUserPermissionChangeAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string user,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_user_rollback_permission_change user={user}, profile={profileName}");
        if (!TryGetConfirmationError("user_rollback_permission_change", user, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "user_rollback_permission_change", confirmationError);
        }

        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "user_rollback_permission_change",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["user"] = user,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns a sanitized firewall status summary.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_firewall_status")]
    [Description("Checks firewalld and ufw availability and status without returning rule bodies.")]
    public static async Task<SshToolResult> GetSshFirewallStatusAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_firewall_status profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "firewall_status",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks one firewall rule change without applying it.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="action">The firewall action: add or remove.</param>
    /// <param name="target">The firewall target: service or port.</param>
    /// <param name="value">The service name or port/protocol value.</param>
    /// <param name="zone">The firewalld zone.</param>
    /// <param name="permanent">Whether to check the permanent firewalld configuration.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_firewall_check_rule")]
    [Description("Checks one firewalld rule change and returns state plus confirmation token without applying it.")]
    public static async Task<SshToolResult> CheckSshFirewallRuleAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string action,
        string target,
        string value,
        string zone = "public",
        string permanent = "false",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_firewall_check_rule action={action}, target={target}, zone={zone}, permanent={permanent}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "firewall_check_rule",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["action"] = action,
                ["target"] = target,
                ["value"] = value,
                ["zone"] = zone,
                ["permanent"] = permanent,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Applies one firewall rule change after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="action">The firewall action: add or remove.</param>
    /// <param name="target">The firewall target: service or port.</param>
    /// <param name="value">The service name or port/protocol value.</param>
    /// <param name="zone">The firewalld zone.</param>
    /// <param name="permanent">Whether to apply the permanent firewalld configuration.</param>
    /// <param name="confirmation">The required confirmation token: firewall_apply_rule:&lt;action&gt;:&lt;target&gt;:&lt;value&gt;:&lt;zone&gt;:&lt;permanent&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_firewall_apply_rule")]
    [Description("Applies one firewalld rule change after explicit confirmation.")]
    public static async Task<SshToolResult> ApplySshFirewallRuleAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string action,
        string target,
        string value,
        string zone,
        string permanent,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_firewall_apply_rule action={action}, target={target}, zone={zone}, permanent={permanent}, profile={profileName}");
        if (!TryGetConfirmationError("firewall_apply_rule", action + ":" + target + ":" + value + ":" + zone + ":" + permanent, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "firewall_apply_rule", confirmationError);
        }

        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "firewall_apply_rule",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["action"] = action,
                ["target"] = target,
                ["value"] = value,
                ["zone"] = zone,
                ["permanent"] = permanent,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks a backup plan for an approved root without creating a backup.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="scanRoot">The approved root to scan.</param>
    /// <param name="depth">The maximum scan depth.</param>
    /// <param name="limit">The maximum entries to inspect.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_backup_plan_check")]
    [Description("Checks backup scope, estimated file counts, and confirmation token without creating a backup.")]
    public static async Task<SshToolResult> CheckSshBackupPlanAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string scanRoot,
        string depth = "2",
        string limit = "100",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_backup_plan_check scanRoot={scanRoot}, depth={depth}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "backup_plan_check",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["scanRoot"] = scanRoot,
                ["depth"] = depth,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Creates a backup archive for an approved root after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="scanRoot">The approved root to scan.</param>
    /// <param name="depth">The maximum scan depth.</param>
    /// <param name="limit">The maximum files to archive.</param>
    /// <param name="confirmation">The required confirmation token: backup_run:&lt;scanRoot&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_backup_run")]
    [Description("Creates a bounded provider-approved backup archive after explicit confirmation.")]
    public static async Task<SshToolResult> RunSshBackupAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string scanRoot,
        string depth,
        string limit,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_backup_run scanRoot={scanRoot}, depth={depth}, limit={limit}, profile={profileName}");
        if (!TryGetConfirmationError("backup_run", scanRoot, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "backup_run", confirmationError);
        }

        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "backup_run",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["scanRoot"] = scanRoot,
                ["depth"] = depth,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Verifies a backup archive under the approved backup directory.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="backupPath">The backup archive path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_backup_verify")]
    [Description("Verifies whether an approved backup archive exists and can be listed without returning archive entries.")]
    public static async Task<SshToolResult> VerifySshBackupAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_backup_verify backupPath={backupPath}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "backup_verify",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["backupPath"] = backupPath,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Verifies an audit log hash chain without returning log bodies.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="logPath">The approved audit log path.</param>
    /// <param name="limit">The maximum records to inspect.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_audit_verify")]
    [Description("Verifies a Kelpie audit log hash chain without returning log bodies.")]
    public static async Task<SshToolResult> VerifySshAuditAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string logPath = "/var/log/kelpie/audit.log",
        string limit = "100",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_audit_verify logPath={logPath}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "audit_verify",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["logPath"] = logPath,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Exports a sanitized audit log summary without returning secrets or raw log bodies.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="logPath">The approved audit log path.</param>
    /// <param name="limit">The maximum records to export.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_audit_export")]
    [Description("Exports a sanitized Kelpie audit log summary without raw log bodies.")]
    public static async Task<SshToolResult> ExportSshAuditAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string logPath = "/var/log/kelpie/audit.log",
        string limit = "100",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_audit_export logPath={logPath}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "audit_export",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["logPath"] = logPath,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns systemd service status from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_service_status")]
    [Description("Runs systemctl status for one service without changing service state.")]
    public static async Task<SshToolResult> GetSshServiceStatusAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string service,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_service_status service={service}, profile={profileName}");
        return await ExecuteServiceCommandAsync(
            sshCommandService,
            profileCatalog,
            "service_status",
            service,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns whether a systemd service is active from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_service_is_active")]
    [Description("Runs systemctl is-active for one service without changing service state.")]
    public static async Task<SshToolResult> GetSshServiceIsActiveAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string service,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_service_is_active service={service}, profile={profileName}");
        return await ExecuteServiceCommandAsync(
            sshCommandService,
            profileCatalog,
            "service_is_active",
            service,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns whether a systemd service is enabled from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_service_is_enabled")]
    [Description("Runs systemctl is-enabled for one service without changing service state.")]
    public static async Task<SshToolResult> GetSshServiceIsEnabledAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string service,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_service_is_enabled service={service}, profile={profileName}");
        return await ExecuteServiceCommandAsync(
            sshCommandService,
            profileCatalog,
            "service_is_enabled",
            service,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns systemd service units from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="state">The systemd state filter.</param>
    /// <param name="limit">The maximum number of service lines to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_list_services")]
    [Description("Lists systemd service units with a validated state filter and line limit.")]
    public static async Task<SshToolResult> ListSshServicesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string state = "running",
        string limit = "100",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_list_services state={state}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "list_services",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["state"] = state,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns recent service log lines from a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="lines">The number of log lines to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_tail_log")]
    [Description("Runs the allowed tail_log command against a configured SSH profile for one systemd service.")]
    public static async Task<SshToolResult> TailSshLogAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string service,
        string profileName,
        string lines = "100",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_tail_log service={service}, lines={lines}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "tail_log",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["service"] = service,
                ["lines"] = lines,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Checks available package updates through a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_pkg_check_updates")]
    [Description("Runs the allowed pkg_check_updates command against a configured SSH profile.")]
    public static async Task<SshToolResult> CheckSshPackageUpdatesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_pkg_check_updates profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "pkg_check_updates",
            arguments: null,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns package metadata through a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="package">The package name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_pkg_info")]
    [Description("Runs the allowed pkg_info command against a configured SSH profile.")]
    public static async Task<SshToolResult> GetSshPackageInfoAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string package,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_pkg_info package={package}, profile={profileName}");
        return await ExecuteSshPackageCommandAsync(
            sshCommandService,
            profileCatalog,
            "pkg_info",
            package,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Searches package candidates through a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="query">The package search query.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="limit">The maximum number of output lines.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_pkg_search")]
    [Description("Runs a limited package search against a configured SSH profile.")]
    public static async Task<SshToolResult> SearchSshPackagesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string query,
        string profileName,
        string limit = "50",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_pkg_search query={query}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "pkg_search",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = query,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Lists installed packages matching a filter through a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="filter">The installed package filter.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="limit">The maximum number of output lines.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_pkg_list_installed")]
    [Description("Runs a limited installed-package listing against a configured SSH profile.")]
    public static async Task<SshToolResult> ListInstalledSshPackagesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string filter,
        string profileName,
        string limit = "50",
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_pkg_list_installed filter={filter}, limit={limit}, profile={profileName}");
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            "pkg_list_installed",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["filter"] = filter,
                ["limit"] = limit,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Simulates package installation through a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="package">The package name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_pkg_simulate_install")]
    [Description("Runs the allowed pkg_simulate_install dry-run command against a configured SSH profile.")]
    public static async Task<SshToolResult> SimulateSshPackageInstallAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string package,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_pkg_simulate_install package={package}, profile={profileName}");
        return await ExecuteSshPackageCommandAsync(
            sshCommandService,
            profileCatalog,
            "pkg_simulate_install",
            package,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns a confirmation request for package installation without executing it.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="package">The package name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <returns>The SSH command confirmation request.</returns>
    [McpServerTool(Name = "ssh_pkg_install")]
    [Description("Returns a confirmation request for the pkg_install command without executing it.")]
    public static SshConfirmationResult ConfirmSshPackageInstall(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string package,
        string profileName)
    {
        KpLog.Info($"MCP SSH tool called: ssh_pkg_install package={package}, profile={profileName}");
        return CreateSshPackageConfirmation(
            sshCommandService,
            profileCatalog,
            "pkg_install",
            package,
            profileName);
    }

    /// <summary>
    /// Installs a package after explicit caller confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="package">The package name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="confirmation">The required confirmation token: pkg_install:&lt;package&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_pkg_install_confirmed")]
    [Description("Runs the allowed pkg_install command after explicit confirmation. The confirmation argument must be pkg_install:<package>.")]
    public static async Task<SshToolResult> InstallSshPackageConfirmedAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string package,
        string profileName,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_pkg_install_confirmed package={package}, profile={profileName}");
        if (!TryGetConfirmationError("pkg_install", package, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "pkg_install", confirmationError);
        }

        return await ExecuteSshPackageCommandAsync(
            sshCommandService,
            profileCatalog,
            "pkg_install",
            package,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Simulates package removal through a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="package">The package name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_pkg_simulate_remove")]
    [Description("Runs the allowed pkg_simulate_remove dry-run command against a configured SSH profile.")]
    public static async Task<SshToolResult> SimulateSshPackageRemoveAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string package,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_pkg_simulate_remove package={package}, profile={profileName}");
        return await ExecuteSshPackageCommandAsync(
            sshCommandService,
            profileCatalog,
            "pkg_simulate_remove",
            package,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Returns a confirmation request for package removal without executing it.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="package">The package name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <returns>The SSH command confirmation request.</returns>
    [McpServerTool(Name = "ssh_pkg_remove")]
    [Description("Returns a confirmation request for the pkg_remove command without executing it.")]
    public static SshConfirmationResult ConfirmSshPackageRemove(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string package,
        string profileName)
    {
        KpLog.Info($"MCP SSH tool called: ssh_pkg_remove package={package}, profile={profileName}");
        return CreateSshPackageConfirmation(
            sshCommandService,
            profileCatalog,
            "pkg_remove",
            package,
            profileName);
    }

    private static async Task<SshToolResult> ExecuteSshPackageCommandAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string commandName,
        string package,
        string profileName,
        CancellationToken cancellationToken)
    {
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            commandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package"] = package,
            },
            profileName,
            cancellationToken);
    }

    private static async Task<SshToolResult> ExecuteLocalPortCommandAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string commandName,
        string port,
        string profileName,
        CancellationToken cancellationToken)
    {
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            commandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["port"] = port,
            },
            profileName,
            cancellationToken);
    }

    private static async Task<SshToolResult> ExecuteCertificateCommandAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string commandName,
        string path,
        string profileName,
        CancellationToken cancellationToken)
    {
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            commandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = path,
            },
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Enables and starts a systemd service after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="confirmation">The required confirmation token: service_enable_now:&lt;service&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_service_enable_now")]
    [Description("Runs systemctl enable --now for one service after explicit confirmation.")]
    public static async Task<SshToolResult> EnableNowSshServiceAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string service,
        string profileName,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_service_enable_now service={service}, profile={profileName}");
        if (!TryGetConfirmationError("service_enable_now", service, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "service_enable_now", confirmationError);
        }

        return await ExecuteServiceCommandAsync(
            sshCommandService,
            profileCatalog,
            "service_enable_now",
            service,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Reloads a systemd service after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="confirmation">The required confirmation token: service_reload:&lt;service&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_service_reload")]
    [Description("Runs systemctl reload for one service after explicit confirmation.")]
    public static async Task<SshToolResult> ReloadSshServiceAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string service,
        string profileName,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_service_reload service={service}, profile={profileName}");
        if (!TryGetConfirmationError("service_reload", service, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "service_reload", confirmationError);
        }

        return await ExecuteServiceCommandAsync(
            sshCommandService,
            profileCatalog,
            "service_reload",
            service,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Restarts a systemd service after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="confirmation">The required confirmation token: service_restart:&lt;service&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_service_restart")]
    [Description("Runs systemctl restart for one service after explicit confirmation.")]
    public static async Task<SshToolResult> RestartSshServiceAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string service,
        string profileName,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_service_restart service={service}, profile={profileName}");
        if (!TryGetConfirmationError("service_restart", service, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "service_restart", confirmationError);
        }

        return await ExecuteServiceCommandAsync(
            sshCommandService,
            profileCatalog,
            "service_restart",
            service,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Stops a systemd service after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="confirmation">The required confirmation token: service_stop:&lt;service&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_service_stop")]
    [Description("Runs systemctl stop for one service after explicit confirmation.")]
    public static async Task<SshToolResult> StopSshServiceAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string service,
        string profileName,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_service_stop service={service}, profile={profileName}");
        if (!TryGetConfirmationError("service_stop", service, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "service_stop", confirmationError);
        }

        return await ExecuteServiceCommandAsync(
            sshCommandService,
            profileCatalog,
            "service_stop",
            service,
            profileName,
            cancellationToken);
    }

    /// <summary>
    /// Disables a systemd service after explicit confirmation.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="service">The systemd service name.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="confirmation">The required confirmation token: service_disable:&lt;service&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "ssh_service_disable")]
    [Description("Runs systemctl disable for one service after explicit confirmation.")]
    public static async Task<SshToolResult> DisableSshServiceAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string service,
        string profileName,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_service_disable service={service}, profile={profileName}");
        if (!TryGetConfirmationError("service_disable", service, confirmation, out var confirmationError))
        {
            return CreateRejectedSshToolResult(profileName, "service_disable", confirmationError);
        }

        return await ExecuteServiceCommandAsync(
            sshCommandService,
            profileCatalog,
            "service_disable",
            service,
            profileName,
            cancellationToken);
    }

    private static async Task<SshToolResult> ExecuteServiceCommandAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string commandName,
        string service,
        string profileName,
        CancellationToken cancellationToken)
    {
        return await ExecuteSshCommandAsync(
            sshCommandService,
            profileCatalog,
            commandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["service"] = service,
            },
            profileName,
            cancellationToken);
    }

    private static bool TryGetConfirmationError(
        string operation,
        string target,
        string confirmation,
        out string error)
    {
        var required = string.IsNullOrWhiteSpace(target)
            ? operation
            : operation + ":" + target;
        if (string.Equals(confirmation, required, StringComparison.Ordinal))
        {
            error = string.Empty;
            return true;
        }

        error = $"Confirmation is required: {required}";
        return false;
    }

    private static SshConfirmationResult CreateSshPackageConfirmation(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string commandName,
        string package,
        string profileName)
    {
        var profile = ResolveSshProfile(profileCatalog, profileName);
        var preview = sshCommandService.Preview(
            profile,
            commandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package"] = package,
            },
            KelpieExecutionChannel.Mcp);

        return new SshConfirmationResult(
            profile.Name,
            profile.Host,
            profile.Port,
            profile.UserName,
            preview.CommandName,
            preview.CommandText,
            preview.RiskLevel.ToString(),
            preview.RequiresConfirmation,
            "Command requires confirmation and has not been executed.");
    }

    private static async Task<SshToolResult> ExecuteSshCommandAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string commandName,
        IReadOnlyDictionary<string, string>? arguments,
        string profileName,
        CancellationToken cancellationToken)
    {
        var profile = ResolveSshProfile(profileCatalog, profileName);
        var operation = SshRemoteOperation.FromProfile(
            profile,
            "managed",
            commandName,
            arguments,
            correlationId: profileName);
        var result = await sshCommandService.ExecuteAsync(
            operation,
            KelpieExecutionChannel.Mcp,
            cancellationToken);

        return new SshToolResult(
            profile.Name,
            profile.Host,
            profile.Port,
            profile.UserName,
            result.CommandName,
            result.CommandText,
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            SplitOutputLines(result.StandardOutput),
            SplitOutputLines(result.StandardError),
            SplitOutputLines(RemoveAnsiEscapeSequences(result.StandardOutput)),
            SplitOutputLines(RemoveAnsiEscapeSequences(result.StandardError)),
            result.StartedAt,
            result.CompletedAt,
            result.TimedOut);
    }

    private static SshRemoteOperationToolResult CreateRemoteOperationToolResult(
        SshRemoteOperation operation,
        SshCommandResult result)
    {
        return new SshRemoteOperationToolResult(
            operation.Options?.CorrelationId,
            operation.Endpoint.Host,
            operation.Endpoint.Port,
            operation.Credential.UserName,
            result.CommandName,
            result.CommandText,
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            SplitOutputLines(result.StandardOutput),
            SplitOutputLines(result.StandardError),
            SplitOutputLines(RemoveAnsiEscapeSequences(result.StandardOutput)),
            SplitOutputLines(RemoveAnsiEscapeSequences(result.StandardError)),
            result.StartedAt,
            result.CompletedAt,
            result.TimedOut);
    }

    private static SshToolResult CreateRejectedSshToolResult(
        string profileName,
        string commandName,
        string error)
    {
        var completedAt = DateTimeOffset.UtcNow;
        return new SshToolResult(
            ProfileName: profileName,
            Host: string.Empty,
            Port: 0,
            UserName: string.Empty,
            CommandName: commandName,
            CommandText: string.Empty,
            ExitCode: -1,
            StandardOutput: string.Empty,
            StandardError: error,
            Stdout: [],
            Stderr: [error],
            StdoutPlain: [],
            StderrPlain: [error],
            completedAt,
            completedAt,
            TimedOut: false,
            Error: error);
    }

    private static bool IsUnknownAllowedCommandError(InvalidOperationException ex)
    {
        return ex.Message.StartsWith("SSH command is not allowed:", StringComparison.Ordinal);
    }

    private static bool IsDedicatedConfirmationCommand(string commandName)
    {
        return commandName is "cron_write"
            or "cron_rollback"
            or "user_apply_group_change"
            or "user_rollback_group_change"
            or "user_apply_permission_change"
            or "user_rollback_permission_change"
            or "firewall_apply_rule"
            or "backup_run";
    }

    private static SshConnectionProfile ResolveSshProfile(
        ISshConnectionProfileCatalog profileCatalog,
        string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new InvalidOperationException("SSH profile name is required.");
        }

        if (profileCatalog.TryGet(profileName, out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException($"SSH profile was not found: {profileName}");
    }

    private static async Task<CommandOutput> RunCommandAsync(CommandSpec command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            Arguments = command.Arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process
        {
            StartInfo = startInfo,
        };

        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new CommandOutput(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static ListeningPortItem[] ParseWindowsNetstat(string output)
    {
        return output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseWindowsNetstatLine)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    private static ListeningPortItem? ParseWindowsNetstatLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 4)
        {
            return null;
        }

        var protocol = parts[0].ToUpperInvariant();
        if (protocol is not ("TCP" or "UDP"))
        {
            return null;
        }

        if (protocol == "TCP" && parts.Length >= 5 && !string.Equals(parts[3], "LISTENING", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var localAddress = parts[1];
        var state = protocol == "TCP" ? parts[3] : "LISTENING";
        var processId = protocol == "TCP" ? parts[4] : parts[3];

        return new ListeningPortItem(protocol, localAddress, state, processId);
    }

    private static ListeningPortItem[] ParseUnixSs(string output)
    {
        return output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .Select(ParseUnixSsLine)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
    }

    private static ListeningPortItem? ParseUnixSsLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 5)
        {
            return null;
        }

        return new ListeningPortItem(parts[0].ToUpperInvariant(), parts[4], parts[1], null);
    }

    private static string[] SplitOutputLines(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return [];
        }

        return output
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string RemoveAnsiEscapeSequences(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        return AnsiEscapeSequenceRegex().Replace(output, string.Empty);
    }

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant)]
    private static partial Regex AnsiEscapeSequenceRegex();

    /// <summary>
    /// Represents basic local system information.
    /// </summary>
    public sealed record SystemInfoResult(
        string MachineName,
        string UserName,
        string OSDescription,
        string OSArchitecture,
        string ProcessArchitecture,
        string FrameworkDescription,
        int ProcessorCount,
        bool Is64BitOperatingSystem,
        bool Is64BitProcess,
        int ProcessId,
        string BaseDirectory);

    /// <summary>
    /// Represents local disk usage.
    /// </summary>
    public sealed record DiskUsageResult(DiskUsageItem[] Drives);

    /// <summary>
    /// Represents one drive's disk usage.
    /// </summary>
    public sealed record DiskUsageItem(
        string Name,
        string DriveType,
        string DriveFormat,
        long TotalBytes,
        long AvailableFreeBytes,
        long TotalFreeBytes,
        long UsedBytes);

    /// <summary>
    /// Represents process and managed runtime memory usage.
    /// </summary>
    public sealed record MemoryUsageResult(
        long WorkingSetBytes,
        long PrivateMemoryBytes,
        long VirtualMemoryBytes,
        long ManagedTotalBytes,
        long HeapSizeBytes,
        long HighMemoryLoadThresholdBytes,
        long MemoryLoadBytes,
        long TotalAvailableMemoryBytes);

    /// <summary>
    /// Represents local listening port information.
    /// </summary>
    public sealed record ListeningPortsResult(
        string Command,
        string Arguments,
        int ExitCode,
        string StandardError,
        ListeningPortItem[] Ports);

    /// <summary>
    /// Represents one local listening port.
    /// </summary>
    public sealed record ListeningPortItem(
        string Protocol,
        string LocalAddress,
        string State,
        string? ProcessId);

    /// <summary>
    /// Represents an SSH MCP tool result.
    /// </summary>
    public sealed record SshToolResult(
        string ProfileName,
        string Host,
        int Port,
        string UserName,
        string CommandName,
        string CommandText,
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string[] Stdout,
        string[] Stderr,
        string[] StdoutPlain,
        string[] StderrPlain,
        DateTimeOffset StartedAt,
        DateTimeOffset CompletedAt,
        bool TimedOut,
        string? Error = null);

    /// <summary>
    /// Represents an SSH remote operation MCP tool result.
    /// </summary>
    public sealed record SshRemoteOperationToolResult(
        string? CorrelationId,
        string Host,
        int Port,
        string UserName,
        string CommandName,
        string CommandText,
        int ExitCode,
        string StandardOutput,
        string StandardError,
        string[] Stdout,
        string[] Stderr,
        string[] StdoutPlain,
        string[] StderrPlain,
        DateTimeOffset StartedAt,
        DateTimeOffset CompletedAt,
        bool TimedOut,
        string? Error = null);

    /// <summary>
    /// Represents an SSH command confirmation request.
    /// </summary>
    public sealed record SshConfirmationResult(
        string ProfileName,
        string Host,
        int Port,
        string UserName,
        string CommandName,
        string CommandText,
        string RiskLevel,
        bool RequiresConfirmation,
        string Message);

    private sealed record CommandSpec(string FileName, string Arguments);

    private sealed record CommandOutput(int ExitCode, string StandardOutput, string StandardError);
}
