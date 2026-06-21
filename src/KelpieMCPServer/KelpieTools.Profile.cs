using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>
    /// Reloads SSH profiles from disk on demand.
    /// </summary>
    /// <param name="profileCatalog">The reloadable SSH profile catalog.</param>
    /// <returns>The reload result.</returns>
    [McpServerTool(Name = "profile_reload")]
    [Description("Reloads SSH profile JSON files from the Kelpie profiles directory on demand.")]
    public static ProfileReloadToolResult ReloadProfiles(ReloadingSshConnectionProfileCatalog profileCatalog)
    {
        KpLog.Info("MCP tool called: profile_reload");
        var result = profileCatalog.Reload();
        if (!result.Success)
        {
            KpLog.Warn($"Profile reload failed. profilesDirectory={result.ProfilesDirectory}, reason={result.ErrorMessage}");
        }
        else
        {
            KpLog.Info($"Profile reload completed. profilesDirectory={result.ProfilesDirectory}, profileCount={result.ProfileCount}");
        }

        return new ProfileReloadToolResult(
            result.Success,
            result.ProfilesDirectory,
            result.ProfileCount,
            result.ProfileNames.ToArray(),
            result.ErrorMessage);
    }

    /// <summary>
    /// Returns profile operation capabilities for an open SSH terminal connection.
    /// </summary>
    /// <param name="terminalSessionManager">The SSH terminal session manager.</param>
    /// <param name="configuration">The MCP server configuration.</param>
    /// <param name="handle">The SSH terminal handle.</param>
    /// <returns>The profile capabilities for the connection.</returns>
    [McpServerTool(Name = "ssh_profile_capabilities")]
    [Description("Returns profile operation capabilities for an open SSH terminal connection.")]
    public static SshProfileCapabilitiesToolResult GetSshProfileCapabilities(
        SshTerminalSessionManager terminalSessionManager,
        IConfiguration configuration,
        string handle)
    {
        KpLog.Info($"MCP tool called: ssh_profile_capabilities handle={handle}");
        if (!terminalSessionManager.TryGetProfileName(handle, out var profileName))
        {
            return new SshProfileCapabilitiesToolResult(
                handle,
                string.Empty,
                ReloadAllowed: false,
                Reason: "session-not-found");
        }

        var reloadAllowed = KelpieProfileOperationsOptions
            .FromConfiguration(configuration)
            .IsAllowed("reload", "MCP");
        return new SshProfileCapabilitiesToolResult(
            handle,
            profileName,
            reloadAllowed,
            reloadAllowed ? "allowed-by-config" : "disabled-by-config");
    }

    /// <summary>
    /// Represents the MCP profile reload result.
    /// </summary>
    /// <param name="Success">A value indicating whether reload succeeded.</param>
    /// <param name="ProfilesDirectory">The profile directory.</param>
    /// <param name="ProfileCount">The active profile count after the reload attempt.</param>
    /// <param name="ProfileNames">The active profile names after the reload attempt.</param>
    /// <param name="ErrorMessage">The reload error message when reload failed.</param>
    public sealed record ProfileReloadToolResult(
        bool Success,
        string ProfilesDirectory,
        int ProfileCount,
        string[] ProfileNames,
        string? ErrorMessage);

    /// <summary>
    /// Represents profile operation capabilities for one SSH terminal connection.
    /// </summary>
    /// <param name="Handle">The SSH terminal handle.</param>
    /// <param name="ProfileName">The SSH profile name.</param>
    /// <param name="ReloadAllowed">A value indicating whether MCP profile reload is allowed.</param>
    /// <param name="Reason">The reason for the capability result.</param>
    public sealed record SshProfileCapabilitiesToolResult(
        string Handle,
        string ProfileName,
        bool ReloadAllowed,
        string Reason);
}
