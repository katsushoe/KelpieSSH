using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;

namespace KelpieServerCommand;

/// <summary>
/// Manages a remote web permission helper policy through an explicit SSH profile.
/// </summary>
public static class RemoteWebPolicyCommand
{
    private const string HelperPath = "/usr/local/libexec/kelpie/kelpie-web-permission-helper";
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Executes a remote web policy command.
    /// </summary>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        IWebPolicyInteraction? interaction = null,
        IWebPolicyRemoteExecutor? executor = null,
        string? profilesDirectory = null,
        SshConnectionProfile? profileOverride = null,
        CancellationToken cancellationToken = default)
    {
        interaction ??= new ConsoleWebPolicyInteraction();
        profilesDirectory ??= KelpieRuntimePaths.GetProfilesDirectory(AppContext.BaseDirectory);
        executor ??= new SshWebPolicyRemoteExecutor();

        if (args.Count < 2)
        {
            return WriteUsage(interaction.Error);
        }

        var action = args[0].ToLowerInvariant();
        var profileName = args[1];
        var profile = profileOverride ?? LoadProfile(profilesDirectory, profileName);
        if (!string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SSH profile did not match the requested profile.");
        }
        return action switch
        {
            "list" => await ListAsync(args.Skip(2).ToArray(), profile, executor, interaction, cancellationToken),
            "add" => await ChangeAsync(args.Skip(2).ToArray(), profile, executor, interaction, "add", cancellationToken),
            "remove" => await ChangeAsync(args.Skip(2).ToArray(), profile, executor, interaction, "remove", cancellationToken),
            "rollback" => await RollbackAsync(args.Skip(2).ToArray(), profile, executor, interaction, cancellationToken),
            _ => WriteUsage(interaction.Error),
        };
    }

    private static SshConnectionProfile LoadProfile(string profilesDirectory, string profileName)
    {
        var result = SshConnectionProfileFileLoader.LoadDirectoryWithErrors(profilesDirectory);
        var catalog = new SshConnectionProfileCatalog(result.Profiles);
        if (!catalog.TryGet(profileName, out var profile))
        {
            throw new InvalidOperationException("SSH profile was not found.");
        }

        return profile;
    }

    private static async Task<int> ListAsync(
        IReadOnlyList<string> args,
        SshConnectionProfile profile,
        IWebPolicyRemoteExecutor executor,
        IWebPolicyInteraction interaction,
        CancellationToken cancellationToken)
    {
        if (args.Count > 1)
        {
            return WriteUsage(interaction.Error);
        }

        var remoteArgs = args.Count == 0 ? Array.Empty<string>() : [Encode(args[0])];
        var result = await executor.ExecuteAsync(profile, "list", remoteArgs, cancellationToken);
        return WriteResult(result, interaction);
    }

    private static async Task<int> ChangeAsync(
        IReadOnlyList<string> args,
        SshConnectionProfile profile,
        IWebPolicyRemoteExecutor executor,
        IWebPolicyInteraction interaction,
        string operation,
        CancellationToken cancellationToken)
    {
        var required = string.Equals(operation, "add", StringComparison.Ordinal) ? 3 : 2;
        if (args.Count != required)
        {
            return WriteUsage(interaction.Error);
        }

        EnsureInteractive(interaction);
        var remoteArgs = new List<string> { Encode(args[0]), Encode(args[1]) };
        if (required == 3)
        {
            if (!string.Equals(args[2], "Update", StringComparison.Ordinal)
                && !string.Equals(args[2], "Create", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Access must be Update or Create.");
            }

            remoteArgs.Add(args[2]);
        }

        var previewResult = await executor.ExecuteAsync(
            profile,
            "preview-" + operation,
            remoteArgs,
            cancellationToken);
        var preview = ReadPreview(previewResult);
        Confirm(interaction, preview);
        remoteArgs.Add(preview.CurrentSha256);
        var applyResult = await executor.ExecuteAsync(
            profile,
            "apply-" + operation,
            remoteArgs,
            cancellationToken);
        return WriteResult(applyResult, interaction);
    }

    private static async Task<int> RollbackAsync(
        IReadOnlyList<string> args,
        SshConnectionProfile profile,
        IWebPolicyRemoteExecutor executor,
        IWebPolicyInteraction interaction,
        CancellationToken cancellationToken)
    {
        if (args.Count != 0)
        {
            return WriteUsage(interaction.Error);
        }

        EnsureInteractive(interaction);
        var previewResult = await executor.ExecuteAsync(
            profile,
            "preview-rollback",
            [],
            cancellationToken);
        var preview = ReadPreview(previewResult);
        Confirm(interaction, preview);
        var result = await executor.ExecuteAsync(
            profile,
            "apply-rollback",
            [preview.CurrentSha256, Encode(preview.BackupName ?? string.Empty)],
            cancellationToken);
        return WriteResult(result, interaction);
    }

    private static RemotePolicyPreview ReadPreview(SshCommandResult result)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(CreateSafeError(result));
        }

        return JsonSerializer.Deserialize<RemotePolicyPreview>(
            result.StandardOutput,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Remote policy preview was empty.");
    }

    private static void Confirm(IWebPolicyInteraction interaction, RemotePolicyPreview preview)
    {
        interaction.Output.WriteLine("--- current");
        interaction.Output.Write(preview.Current);
        interaction.Output.WriteLine("+++ proposed");
        interaction.Output.Write(preview.Proposed);
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        interaction.Output.Write($"Type {code} to apply: ");
        if (!string.Equals(interaction.ReadLine(), code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Confirmation code did not match.");
        }
    }

    private static void EnsureInteractive(IWebPolicyInteraction interaction)
    {
        if (!interaction.IsInteractive)
        {
            throw new InvalidOperationException("web-policy changes require an interactive human terminal.");
        }
    }

    private static int WriteResult(SshCommandResult result, IWebPolicyInteraction interaction)
    {
        if (result.ExitCode != 0)
        {
            interaction.Error.WriteLine(CreateSafeError(result));
            return 1;
        }

        interaction.Output.Write(result.StandardOutput);
        return 0;
    }

    private static string CreateSafeError(SshCommandResult result)
    {
        var detail = result.StandardError
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        if (detail.Contains("unsupported action: policy", StringComparison.OrdinalIgnoreCase))
        {
            return "The VPS web permission helper is too old for remote web-policy management. "
                + "Update /usr/local/libexec/kelpie/kelpie-web-permission-helper to version 0.2.1.0 or later, "
                + "preserve root ownership and mode 0755, then retry.";
        }

        return detail.Length == 0
            ? $"Remote web-policy command failed. ExitCode={result.ExitCode}."
            : $"Remote web-policy command failed. ExitCode={result.ExitCode}. {detail}";
    }

    private static string Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private static int WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  kelpiemcp web-policy list <profile> [<site-root>]");
        writer.WriteLine("  kelpiemcp web-policy add <profile> <site-root> <file-path> <Update|Create>");
        writer.WriteLine("  kelpiemcp web-policy remove <profile> <site-root> <file-path>");
        writer.WriteLine("  kelpiemcp web-policy rollback <profile>");
        return 1;
    }

    private sealed record RemotePolicyPreview(
        string Current,
        string Proposed,
        string CurrentSha256,
        string? BackupName);

    private sealed class SshWebPolicyRemoteExecutor : IWebPolicyRemoteExecutor
    {
        private readonly ISshCommandRunner _runner = new SshNetCommandRunner();

        public async Task<SshCommandResult> ExecuteAsync(
            SshConnectionProfile profile,
            string action,
            IReadOnlyList<string> args,
            CancellationToken cancellationToken)
        {
            ValidateAction(action);
            foreach (var argument in args)
            {
                ValidateArgument(argument);
            }

            var commandText = "sudo -n " + HelperPath + " policy " + action;
            if (args.Count > 0)
            {
                commandText += " " + string.Join(' ', args);
            }

            var request = new SshCommandRequest(
                profile,
                "human_remote_web_policy",
                commandText,
                CommandTimeout,
                new Dictionary<string, string>(StringComparer.Ordinal));
            return await _runner.ExecuteAsync(request, cancellationToken);
        }

        private static void ValidateAction(string action)
        {
            if (action is not ("list"
                or "preview-add"
                or "apply-add"
                or "preview-remove"
                or "apply-remove"
                or "preview-rollback"
                or "apply-rollback"))
            {
                throw new InvalidOperationException("Remote policy action is invalid.");
            }
        }

        private static void ValidateArgument(string argument)
        {
            if (argument.Length > 8192
                || argument.Any(character => !(char.IsAsciiLetterOrDigit(character)
                    || character is '+' or '/' or '=')))
            {
                throw new InvalidOperationException("Remote policy argument is invalid.");
            }
        }
    }
}

/// <summary>
/// Executes one privileged helper action over SSH.
/// </summary>
public interface IWebPolicyRemoteExecutor
{
    /// <summary>Executes an action without exposing credentials or secret values.</summary>
    Task<SshCommandResult> ExecuteAsync(
        SshConnectionProfile profile,
        string action,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken);
}
