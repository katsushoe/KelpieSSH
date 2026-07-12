using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>
    /// Lists remote environment variable keys for a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "get_environment_keys")]
    [Description("Lists remote environment variable keys for a configured SSH profile when profile policy allows it.")]
    public static async Task<SshToolResult> GetEnvironmentKeysAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: get_environment_keys profile={profileName}");
        try
        {
            var profile = ResolveSshProfile(profileCatalog, profileName);
            var result = await sshCommandService.GetEnvironmentKeysAsync(
                profile,
                cancellationToken: cancellationToken);

            return CreateSshToolResult(profile, result);
        }
        catch (InvalidOperationException ex)
        {
            return CreateRejectedSshToolResult(profileName, "get_environment_keys", ex.Message);
        }
    }

    /// <summary>
    /// Reads one remote environment variable value for a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="key">The environment variable key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "peek_environment_value")]
    [Description("Reads one remote environment variable value only when profile policy allows the key.")]
    public static async Task<SshToolResult> PeekEnvironmentValueAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string key,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: peek_environment_value profile={profileName}, key={key}");
        try
        {
            var profile = ResolveSshProfile(profileCatalog, profileName);
            var result = await sshCommandService.PeekEnvironmentValueAsync(
                profile,
                key,
                cancellationToken: cancellationToken);

            return CreateSshToolResult(profile, result);
        }
        catch (InvalidOperationException ex)
        {
            return CreateRejectedSshToolResult(profileName, "peek_environment_value", ex.Message);
        }
    }

    /// <summary>
    /// Runs one command with one environment variable value set for that execution only.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="key">The environment variable key.</param>
    /// <param name="value">The environment variable value.</param>
    /// <param name="command">The command to run with the environment variable set.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "set_environment_value")]
    [Description("Runs one command with one remote environment variable set for that execution only. The value is not persisted.")]
    public static async Task<SshToolResult> SetEnvironmentValueAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string key,
        string value,
        string command,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: set_environment_value profile={profileName}, key={key}");
        try
        {
            var profile = ResolveSshProfile(profileCatalog, profileName);
            var result = await sshCommandService.SetEnvironmentValueAsync(
                profile,
                key,
                value,
                command,
                channel: KelpieExecutionChannel.Mcp,
                cancellationToken: cancellationToken);

            return CreateSshToolResult(
                profile,
                result,
                sanitizedCommandText: $"env {key}=(hidden) {command.Trim()}");
        }
        catch (InvalidOperationException ex)
        {
            return CreateRejectedSshToolResult(profileName, "set_environment_value", ex.Message);
        }
    }

    /// <summary>
    /// Lists persistent remote environment variable keys in the Kelpie env file.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "list_persistent_environment_keys")]
    [Description("Lists environment variable keys persisted in ~/.kelpie/.env when profile policy allows key listing.")]
    public static async Task<SshToolResult> ListPersistentEnvironmentKeysAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: list_persistent_environment_keys profile={profileName}");
        try
        {
            var profile = ResolveSshProfile(profileCatalog, profileName);
            var result = await sshCommandService.ListPersistentEnvironmentKeysAsync(
                profile,
                cancellationToken: cancellationToken);

            return CreateSshToolResult(profile, result);
        }
        catch (InvalidOperationException ex)
        {
            return CreateRejectedSshToolResult(profileName, "list_persistent_environment_keys", ex.Message);
        }
    }

    /// <summary>
    /// Persists one remote environment variable value in the Kelpie env file.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="key">The environment variable key.</param>
    /// <param name="value">The environment variable value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "persist_environment_value")]
    [Description("Persists one remote environment variable value in ~/.kelpie/.env when profile policy allows setting the key.")]
    public static async Task<SshToolResult> PersistEnvironmentValueAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: persist_environment_value profile={profileName}, key={key}");
        try
        {
            var profile = ResolveSshProfile(profileCatalog, profileName);
            var result = await sshCommandService.PersistEnvironmentValueAsync(
                profile,
                key,
                value,
                cancellationToken: cancellationToken);

            return CreateSshToolResult(
                profile,
                result,
                sanitizedCommandText: $"persist {key}=(hidden) ~/.kelpie/.env");
        }
        catch (InvalidOperationException ex)
        {
            return CreateRejectedSshToolResult(profileName, "persist_environment_value", ex.Message);
        }
    }

    /// <summary>
    /// Removes one remote environment variable value from the Kelpie env file.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="key">The environment variable key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    [McpServerTool(Name = "remove_persistent_environment_value")]
    [Description("Removes one remote environment variable value from ~/.kelpie/.env when profile policy allows setting the key.")]
    public static async Task<SshToolResult> RemovePersistentEnvironmentValueAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        string key,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: remove_persistent_environment_value profile={profileName}, key={key}");
        try
        {
            var profile = ResolveSshProfile(profileCatalog, profileName);
            var result = await sshCommandService.RemovePersistentEnvironmentValueAsync(
                profile,
                key,
                cancellationToken: cancellationToken);

            return CreateSshToolResult(profile, result);
        }
        catch (InvalidOperationException ex)
        {
            return CreateRejectedSshToolResult(profileName, "remove_persistent_environment_value", ex.Message);
        }
    }
}
