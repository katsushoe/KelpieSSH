using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>
    /// Returns profile-specific SSH capabilities.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH capability result.</returns>
    [McpServerTool(Name = "ssh_get_capabilities")]
    [Description("Checks profile-specific SSH command and MCP tool capabilities.")]
    public static async Task<SshCapabilityResult> GetSshCapabilitiesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: ssh_get_capabilities profile={profileName}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        var catalog = AllowedCommandCatalog.CreateForProfile(profile, CommandProcessingProviderCatalog.CreateDefault());
        var commands = catalog.List()
            .Select(command => new SshCommandCapability(
                command.Name,
                command.RiskLevel.ToString(),
                command.RiskLevel == SshCommandRiskLevel.ConfirmRequired))
            .ToArray();

        var commandLookup = commands.ToDictionary(command => command.CommandName, StringComparer.OrdinalIgnoreCase);
        var tools = CreateSshToolCapabilities(profile, commandLookup);
        var probe = await sshCommandService.ExecuteAsync(
            profile,
            "get_os_release",
            arguments: null,
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        return new SshCapabilityResult(
            profile.Name,
            profile.OsFamily,
            profile.PackageManager,
            profile.Mode.ToString(),
            probe.ExitCode == 0,
            probe.CommandName,
            probe.CommandText,
            probe.ExitCode,
            commands,
            tools);
    }

    private static SshToolCapability[] CreateSshToolCapabilities(
        SshConnectionProfile profile,
        IReadOnlyDictionary<string, SshCommandCapability> commands)
    {
        return
        [
            CreateSshToolCapability("ssh_get_system_info", "get_system_info", commands),
            CreateSshToolCapability("ssh_get_os_release", "get_os_release", commands),
            CreateSshToolCapability("get_target_inventory", "target_inventory", commands),
            CreateSshToolCapability("ssh_get_uptime", "get_uptime", commands),
            CreateSshToolCapability("ssh_get_disk_usage", "get_disk_usage", commands),
            CreateSshToolCapability("ssh_get_memory_usage", "get_memory_usage", commands),
            CreateSshToolCapability("ssh_get_process_summary", "get_process_summary", commands),
            CreateSshToolCapability("ssh_get_inode_usage", "get_inode_usage", commands),
            CreateSshToolCapability("ssh_get_mounts", "get_mounts", commands),
            CreateSshToolCapability("ssh_get_network_addresses", "get_network_addresses", commands),
            CreateSshToolCapability("ssh_get_routes", "get_routes", commands),
            CreateSshToolCapability("ssh_get_dns_config", "get_dns_config", commands),
            CreateSshToolCapability("ssh_cron_list", "cron_list", commands),
            CreateSshToolCapability("ssh_cron_validate", "cron_validate", commands),
            CreateSshToolCapability("ssh_cron_check_write", "cron_check_write", commands),
            CreateSshToolCapability("ssh_cron_write", "cron_write", commands),
            CreateSshToolCapability("ssh_cron_rollback", "cron_rollback", commands),
            CreateSshToolCapability("ssh_cert_inspect", "cert_inspect", commands),
            CreateSshToolCapability("ssh_cert_expiry_check", "cert_expiry_check", commands),
            CreateSshToolCapability("ssh_user_list", "user_list", commands),
            CreateSshToolCapability("ssh_user_info", "user_info", commands),
            CreateSshToolCapability("ssh_group_list", "group_list", commands),
            CreateSshToolCapability("ssh_group_info", "group_info", commands),
            CreateSshToolCapability("ssh_sudoers_check", "sudoers_check", commands),
            CreateSshToolCapability("ssh_user_usage_check", "user_usage_check", commands),
            CreateSshToolCapability("ssh_user_check_group_change", "user_check_group_change", commands),
            CreateSshToolCapability("ssh_user_apply_group_change", "user_apply_group_change", commands),
            CreateSshToolCapability("ssh_user_rollback_group_change", "user_rollback_group_change", commands),
            CreateSshToolCapability("ssh_user_check_permission_change", "user_check_permission_change", commands),
            CreateSshToolCapability("ssh_user_apply_permission_change", "user_apply_permission_change", commands),
            CreateSshToolCapability("ssh_user_rollback_permission_change", "user_rollback_permission_change", commands),
            CreateSshToolCapability("ssh_user_file_ownership_check", "user_file_ownership_check", commands),
            CreateSshToolCapability("ssh_user_service_usage_check", "user_service_usage_check", commands),
            CreateSshToolCapability("ssh_service_residual_config_check", "service_residual_config_check", commands),
            CreateSshToolCapability("ssh_firewall_status", "firewall_status", commands),
            CreateSshToolCapability("ssh_firewall_check_rule", "firewall_check_rule", commands),
            CreateSshToolCapability("ssh_firewall_apply_rule", "firewall_apply_rule", commands),
            CreateSshToolCapability("ssh_backup_plan_check", "backup_plan_check", commands),
            CreateSshToolCapability("ssh_backup_run", "backup_run", commands),
            CreateSshToolCapability("ssh_backup_verify", "backup_verify", commands),
            CreateSshToolCapability("ssh_audit_verify", "audit_verify", commands),
            CreateSshToolCapability("ssh_audit_export", "audit_export", commands),
            CreateSshToolCapability("ssh_check_http_local", "check_http_local", commands),
            CreateSshToolCapability("ssh_check_tcp_connect_local", "check_tcp_connect_local", commands),
            CreateSshToolCapability("ssh_get_listening_ports", "get_listening_ports", commands),
            CreateSshToolCapability("ssh_get_failed_services", "get_failed_services", commands),
            CreateSshToolCapability("ssh_get_journal_recent", "get_journal_recent", commands),
            CreateSshToolCapability("ssh_service_status", "service_status", commands),
            CreateSshToolCapability("ssh_service_is_active", "service_is_active", commands),
            CreateSshToolCapability("ssh_service_is_enabled", "service_is_enabled", commands),
            CreateSshToolCapability("ssh_list_services", "list_services", commands),
            CreateSshToolCapability("ssh_support_report_collect", "support_report_collect", commands),
            CreateSshToolCapability("ssh_tail_log", "tail_log", commands),
            CreateSshToolCapability("ssh_pkg_check_updates", "pkg_check_updates", commands),
            CreateSshToolCapability("ssh_pkg_info", "pkg_info", commands),
            CreateSshToolCapability("ssh_pkg_search", "pkg_search", commands),
            CreateSshToolCapability("ssh_pkg_list_installed", "pkg_list_installed", commands),
            CreateSshToolCapability("ssh_pkg_simulate_install", "pkg_simulate_install", commands),
            CreateSshToolCapability("ssh_pkg_install", "pkg_install", commands),
            CreateSshToolCapability("ssh_pkg_simulate_remove", "pkg_simulate_remove", commands),
            CreateSshToolCapability("ssh_pkg_remove", "pkg_remove", commands),
            CreateSshToolCapability("ssh_service_restart", "service_restart", commands),
            CreateSshToolCapability("ssh_service_stop", "service_stop", commands),
            CreateSshToolCapability("ssh_service_disable", "service_disable", commands),
            CreateEnvironmentToolCapability(
                profile,
                "get_environment_keys",
                "get_environment_keys",
                KelpiePolicyNames.AllowPeekEnvironmentKeys,
                nameof(SshCommandRiskLevel.ReadOnly)),
            CreateEnvironmentToolCapability(
                profile,
                "peek_environment_value",
                "peek_environment_value",
                KelpiePolicyNames.AllowPeekEnvironmentValues,
                nameof(SshCommandRiskLevel.ReadOnly)),
            CreateEnvironmentToolCapability(
                profile,
                "set_environment_value",
                "set_environment_value",
                KelpiePolicyNames.AllowSetEnvironmentValues,
                "EnvironmentSet"),
            CreateEnvironmentToolCapability(
                profile,
                "list_persistent_environment_keys",
                "list_persistent_environment_keys",
                KelpiePolicyNames.AllowPeekEnvironmentKeys,
                nameof(SshCommandRiskLevel.ReadOnly)),
            CreateEnvironmentToolCapability(
                profile,
                "persist_environment_value",
                "persist_environment_value",
                KelpiePolicyNames.AllowSetEnvironmentValues,
                "EnvironmentSet"),
            CreateEnvironmentToolCapability(
                profile,
                "remove_persistent_environment_value",
                "remove_persistent_environment_value",
                KelpiePolicyNames.AllowSetEnvironmentValues,
                "EnvironmentSet"),
        ];
    }

    private static SshToolCapability CreateSshToolCapability(
        string toolName,
        string commandName,
        IReadOnlyDictionary<string, SshCommandCapability> commands)
    {
        if (commands.TryGetValue(commandName, out var command))
        {
            return new SshToolCapability(
                toolName,
                commandName,
                Available: true,
                command.RiskLevel,
                command.RequiresConfirmation,
                UnavailableReason: null);
        }

        return new SshToolCapability(
            toolName,
            commandName,
            Available: false,
            RiskLevel: string.Empty,
            RequiresConfirmation: false,
            UnavailableReason: "Command is not available for this profile.");
    }

    private static SshToolCapability CreateEnvironmentToolCapability(
        SshConnectionProfile profile,
        string toolName,
        string commandName,
        string requiredCapability,
        string riskLevel)
    {
        if (profile.Capabilities.Allows(requiredCapability))
        {
            return new SshToolCapability(
                toolName,
                commandName,
                Available: true,
                riskLevel,
                RequiresConfirmation: false,
                UnavailableReason: null);
        }

        return new SshToolCapability(
            toolName,
            commandName,
            Available: false,
            RiskLevel: string.Empty,
            RequiresConfirmation: false,
            UnavailableReason: $"{requiredCapability} is not enabled for this profile.");
    }

    /// <summary>
    /// Represents profile-specific SSH capabilities.
    /// </summary>
    public sealed record SshCapabilityResult(
        string ProfileName,
        string OsFamily,
        string PackageManager,
        string Mode,
        bool ProbeSucceeded,
        string ProbeCommandName,
        string ProbeCommandText,
        int ProbeExitCode,
        SshCommandCapability[] Commands,
        SshToolCapability[] Tools);

    /// <summary>
    /// Represents one allowed SSH command capability.
    /// </summary>
    public sealed record SshCommandCapability(
        string CommandName,
        string RiskLevel,
        bool RequiresConfirmation);

    /// <summary>
    /// Represents one SSH MCP tool capability.
    /// </summary>
    public sealed record SshToolCapability(
        string ToolName,
        string CommandName,
        bool Available,
        string RiskLevel,
        bool RequiresConfirmation,
        string? UnavailableReason);
}
