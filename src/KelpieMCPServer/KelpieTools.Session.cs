using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>
    /// Closes a persistent SSH terminal connection by handle.
    /// </summary>
    /// <param name="terminalSessionManager">The terminal session manager.</param>
    /// <param name="handle">The terminal connection handle.</param>
    /// <returns>The close result.</returns>
    [McpServerTool(Name = "ssh_connection_close")]
    [Description("Closes a persistent SSH terminal connection opened by ssh_terminal_open.")]
    public static async Task<SshTerminalCloseResult> CloseSshConnectionAsync(
        SshTerminalSessionManager terminalSessionManager,
        string handle)
    {
        KpLog.Info($"MCP SSH tool called: ssh_connection_close handle={handle}");
        return await terminalSessionManager.CloseAsync(handle);
    }

    /// <summary>
    /// Clears the in-memory SSH password session for a profile.
    /// </summary>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="passwordSessionStore">The password session store.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <returns>The logout result.</returns>
    [McpServerTool(Name = "ssh_logout")]
    [Description("Clears the in-memory SSH password session for a configured SSH profile.")]
    public static SshLogoutResult LogoutSshProfile(
        ISshConnectionProfileCatalog profileCatalog,
        ISshPasswordSessionStore passwordSessionStore,
        string profileName)
    {
        KpLog.Info($"MCP SSH tool called: ssh_logout profile={profileName}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        if (string.IsNullOrWhiteSpace(profile.PasswordSecretName))
        {
            return new SshLogoutResult(
                profile.Name,
                LoggedOut: false,
                "SSH password secret name is not configured.");
        }

        var loggedOut = passwordSessionStore.ClearPassword(profile.PasswordSecretName);
        if (loggedOut)
        {
            KpLog.Info($"SSH password session cleared by MCP logout. profile={profile.Name}");
        }

        return new SshLogoutResult(profile.Name, loggedOut, string.Empty);
    }

    /// <summary>
    /// Represents an SSH logout result.
    /// </summary>
    /// <param name="ProfileName">The SSH profile name.</param>
    /// <param name="LoggedOut">A value indicating whether a password session was removed.</param>
    /// <param name="Error">The error message when logout could not be performed.</param>
    public sealed record SshLogoutResult(
        string ProfileName,
        bool LoggedOut,
        string Error);
}
