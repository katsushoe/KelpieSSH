using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>
    /// Reloads SSH profiles from disk on demand.
    /// </summary>
    /// <param name="profileCatalog">The reloadable SSH profile catalog.</param>
    /// <param name="profileOperations">The trusted profile operation policy snapshot.</param>
    /// <param name="inventoryCache">The target inventory cache.</param>
    /// <returns>The reload result.</returns>
    [McpServerTool(Name = "profile_reload")]
    [Description("Reloads SSH profile JSON files from the Kelpie profiles directory on demand.")]
    public static ProfileReloadToolResult ReloadProfiles(
        ReloadingSshConnectionProfileCatalog profileCatalog,
        KelpieProfileOperationsOptions profileOperations,
        TargetInventoryCache inventoryCache)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        const string source = "tool_request";
        KpLog.Info($"MCP tool called: profile_reload correlationId={correlationId}");
        if (!profileOperations.IsAllowed("reload", "MCP"))
        {
            const string reason = "disabled-by-config";
            KpLog.Warn($"Profile reload forbidden. source={source}, reason={reason}, correlationId={correlationId}");
            var activeProfiles = profileCatalog.List();
            return new ProfileReloadToolResult(
                Success: false,
                Status: "forbidden",
                Reason: reason,
                Source: source,
                CorrelationId: correlationId,
                ProfilesDirectory: profileCatalog.ProfilesDirectory,
                ProfileCount: activeProfiles.Count,
                ProfileNames: activeProfiles.Select(profile => profile.Name).ToArray(),
                AffectedProfiles: [],
                ErrorMessage: "MCP profile reload is disabled by trusted server configuration.");
        }

        var result = profileCatalog.Reload();
        if (!result.Success)
        {
            var reason = profileCatalog.ProfileLoadErrors.FirstOrDefault()?.Reason ?? "reload-failed";
            KpLog.Warn($"Profile reload blocked. source=file_reload, reason={reason}, correlationId={correlationId}");
            return new ProfileReloadToolResult(
                Success: false,
                Status: "blocked",
                Reason: reason,
                Source: "file_reload",
                CorrelationId: correlationId,
                ProfilesDirectory: result.ProfilesDirectory,
                ProfileCount: result.ProfileCount,
                ProfileNames: result.ProfileNames.ToArray(),
                AffectedProfiles: profileCatalog.ProfileLoadErrors.Select(error => error.ProfileName).ToArray(),
                ErrorMessage: result.ErrorMessage);
        }

        inventoryCache.Clear();

        KpLog.Info($"Profile reload completed. profilesDirectory={result.ProfilesDirectory}, profileCount={result.ProfileCount}, correlationId={correlationId}");

        return new ProfileReloadToolResult(
            Success: true,
            Status: "ok",
            Reason: string.Empty,
            Source: source,
            CorrelationId: correlationId,
            ProfilesDirectory: result.ProfilesDirectory,
            ProfileCount: result.ProfileCount,
            ProfileNames: result.ProfileNames.ToArray(),
            AffectedProfiles: [],
            ErrorMessage: null);
    }

    /// <summary>
    /// Returns profile operation capabilities for an open SSH terminal connection.
    /// </summary>
    /// <param name="terminalSessionManager">The SSH terminal session manager.</param>
    /// <param name="profileOperations">The trusted profile operation policy snapshot.</param>
    /// <param name="handle">The SSH terminal handle.</param>
    /// <returns>The profile capabilities for the connection.</returns>
    [McpServerTool(Name = "ssh_profile_capabilities")]
    [Description("Returns profile operation capabilities for an open SSH terminal connection.")]
    public static SshProfileCapabilitiesToolResult GetSshProfileCapabilities(
        SshTerminalSessionManager terminalSessionManager,
        KelpieProfileOperationsOptions profileOperations,
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

        var reloadAllowed = profileOperations.IsAllowed("reload", "MCP");
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
    /// <param name="Status">The structured operation status.</param>
    /// <param name="Reason">The structured result reason.</param>
    /// <param name="Source">The source of the reload decision.</param>
    /// <param name="CorrelationId">The audit correlation identifier.</param>
    /// <param name="ProfilesDirectory">The profile directory.</param>
    /// <param name="ProfileCount">The active profile count after the reload attempt.</param>
    /// <param name="ProfileNames">The active profile names after the reload attempt.</param>
    /// <param name="AffectedProfiles">The profile names associated with validation failures.</param>
    /// <param name="ErrorMessage">The reload error message when reload failed.</param>
    public sealed record ProfileReloadToolResult(
        bool Success,
        string Status,
        string Reason,
        string Source,
        string CorrelationId,
        string ProfilesDirectory,
        int ProfileCount,
        string[] ProfileNames,
        string[] AffectedProfiles,
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
