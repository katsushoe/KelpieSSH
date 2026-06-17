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
        var profile = ResolveSshProfile(profileCatalog, profileName);
        var result = await sshCommandService.GetEnvironmentKeysAsync(
            profile,
            cancellationToken: cancellationToken);

        return CreateSshToolResult(profile, result);
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
        var profile = ResolveSshProfile(profileCatalog, profileName);
        var result = await sshCommandService.PeekEnvironmentValueAsync(
            profile,
            key,
            cancellationToken: cancellationToken);

        return CreateSshToolResult(profile, result);
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

    private static SshToolResult CreateSshToolResult(
        SshConnectionProfile profile,
        SshCommandResult result,
        string? sanitizedCommandText = null)
    {
        return new SshToolResult(
            profile.Name,
            profile.Host,
            profile.Port,
            profile.UserName,
            result.CommandName,
            sanitizedCommandText ?? result.CommandText,
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
}
