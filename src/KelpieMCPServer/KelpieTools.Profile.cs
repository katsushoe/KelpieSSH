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
}
