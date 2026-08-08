using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;

namespace KelpieServerCommand;

/// <summary>
/// Updates the fixed VPS web permission helper through a human-only workflow.
/// </summary>
public static class HelperUpdateCommand
{
    private const long MaxArtifactBytes = 64 * 1024 * 1024;

    /// <summary>
    /// Runs the helper update workflow.
    /// </summary>
    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        IWebPolicyInteraction? interaction = null,
        IHelperUpdateRemote? remote = null,
        string? profilesDirectory = null,
        SshConnectionProfile? profileOverride = null,
        CancellationToken cancellationToken = default)
    {
        interaction ??= new ConsoleWebPolicyInteraction();
        remote ??= new SshHelperUpdateRemote();
        profilesDirectory ??= KelpieRuntimePaths.GetProfilesDirectory(AppContext.BaseDirectory);
        try
        {
            return await RunCoreAsync(args, interaction, remote, profilesDirectory, profileOverride, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            interaction.Error.WriteLine("ERROR: " + ex.Message.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal));
            interaction.Error.WriteLine("No changes were applied.");
            return 1;
        }
    }

    private static async Task<int> RunCoreAsync(
        IReadOnlyList<string> args,
        IWebPolicyInteraction interaction,
        IHelperUpdateRemote remote,
        string profilesDirectory,
        SshConnectionProfile? profileOverride,
        CancellationToken cancellationToken)
    {
        if (args.Count != 3 || !string.Equals(args[0], "update", StringComparison.OrdinalIgnoreCase))
        {
            interaction.Error.WriteLine("Usage: kelpiemcp helper update <profile> <local-artifact>");
            return 1;
        }

        if (!interaction.IsInteractive)
        {
            throw new InvalidOperationException("helper update requires an interactive human terminal.");
        }

        var artifactPath = ValidateArtifact(args[2]);
        var artifactHash = await HashFileAsync(artifactPath, cancellationToken);
        var profile = profileOverride ?? LoadProfile(profilesDirectory, args[1]);
        await remote.UploadAsync(profile, artifactPath, cancellationToken);
        var preview = await remote.PreviewAsync(profile, artifactHash, cancellationToken);
        if (!preview.StagedHashMatches)
        {
            throw new InvalidOperationException("Uploaded helper SHA-256 did not match the local artifact.");
        }

        interaction.Output.WriteLine($"Current: {preview.CurrentVersion}");
        interaction.Output.WriteLine($"Current SHA-256: {preview.CurrentHash}");
        interaction.Output.WriteLine($"Proposed SHA-256: {artifactHash}");
        if (string.Equals(preview.CurrentHash, artifactHash, StringComparison.OrdinalIgnoreCase))
        {
            interaction.Output.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                changed = false,
                message = "Helper is already up to date.",
            }));
            return 0;
        }

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant();
        interaction.Output.Write($"Type {code} to apply: ");
        if (!string.Equals(interaction.ReadLine(), code, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Confirmation code did not match.");
        }

        var result = await remote.ApplyAsync(profile, artifactHash, cancellationToken);
        if (!result.Succeeded)
        {
            interaction.Error.WriteLine(result.Message);
            return 1;
        }

        interaction.Output.WriteLine(JsonSerializer.Serialize(new
        {
            success = true,
            changed = true,
            message = result.Message,
        }));
        return 0;
    }

    private static string ValidateArtifact(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException("Helper artifact must be an existing non-reparse-point file.");
        }

        if (info.Length is <= 0 or > MaxArtifactBytes)
        {
            throw new InvalidOperationException("Helper artifact size is invalid.");
        }

        return fullPath;
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken))
            .ToLowerInvariant();
    }

    private static SshConnectionProfile LoadProfile(string profilesDirectory, string profileName)
    {
        var result = SshConnectionProfileFileLoader.LoadDirectoryWithErrors(profilesDirectory);
        var catalog = new SshConnectionProfileCatalog(result.Profiles);
        return catalog.TryGet(profileName, out var profile)
            ? profile
            : throw new InvalidOperationException("SSH profile was not found.");
    }
}

/// <summary>
/// Provides the private remote operations used only by the human helper update command.
/// </summary>
public interface IHelperUpdateRemote
{
    /// <summary>Uploads the artifact to the fixed staging path.</summary>
    Task UploadAsync(SshConnectionProfile profile, string localPath, CancellationToken cancellationToken);

    /// <summary>Reads current version and verifies the staged hash.</summary>
    Task<HelperUpdatePreview> PreviewAsync(
        SshConnectionProfile profile,
        string expectedHash,
        CancellationToken cancellationToken);

    /// <summary>Applies the fixed-path update transaction.</summary>
    Task<HelperUpdateResult> ApplyAsync(
        SshConnectionProfile profile,
        string expectedHash,
        CancellationToken cancellationToken);
}

/// <summary>Represents helper update preflight information.</summary>
public sealed record HelperUpdatePreview(
    string CurrentVersion,
    string CurrentHash,
    bool StagedHashMatches);

/// <summary>Represents the final helper update result.</summary>
public sealed record HelperUpdateResult(bool Succeeded, string Message);

internal sealed class SshHelperUpdateRemote : IHelperUpdateRemote
{
    private const string StagingPath = "/tmp/kelpie-web-permission-helper.update";
    private const string TargetPath = "/usr/local/libexec/kelpie/kelpie-web-permission-helper";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly string UpdateScriptBase64 = Convert.ToBase64String(
        Encoding.UTF8.GetBytes(
            """
            expected_hash="$1"
            staging="/tmp/kelpie-web-permission-helper.update"
            target="/usr/local/libexec/kelpie/kelpie-web-permission-helper"
            directory="/usr/local/libexec/kelpie"
            temporary="$directory/.kelpie-web-permission-helper.update"
            backup="$directory/.kelpie-web-permission-helper.backup"

            case "$expected_hash" in
                *[!0-9a-fA-F]*|"") printf 'error=invalidHash\n'; exit 2 ;;
            esac
            [ "${#expected_hash}" -eq 64 ] || { printf 'error=invalidHash\n'; exit 2; }
            [ -f "$staging" ] && [ ! -L "$staging" ] || { printf 'error=invalidStaging\n'; exit 2; }
            [ -f "$target" ] && [ ! -L "$target" ] || { printf 'error=invalidTarget\n'; exit 2; }
            [ -d "$directory" ] && [ ! -L "$directory" ] || { printf 'error=invalidDirectory\n'; exit 2; }

            staged_hash=$(sha256sum "$staging" | awk '{print $1}')
            [ "$staged_hash" = "$expected_hash" ] || { printf 'error=stagedHashMismatch\n'; exit 2; }
            logger -t kelpie-helper-update confirmed

            rm -f -- "$temporary"
            cp --preserve=mode,ownership,timestamps -- "$target" "$backup" || exit 3
            chown root:root "$backup" || exit 3
            chmod 0755 "$backup" || exit 3
            cp -- "$staging" "$temporary" || exit 3
            chown root:root "$temporary" || exit 3
            chmod 0755 "$temporary" || exit 3

            temporary_hash=$(sha256sum "$temporary" | awk '{print $1}')
            if [ "$temporary_hash" != "$expected_hash" ]; then
                rm -f -- "$temporary"
                printf 'error=temporaryHashMismatch\n'
                exit 3
            fi

            proposed_version=$("$temporary" --version 2>/dev/null)
            case "$proposed_version" in
                "kelpie-web-permission-helper "*) ;;
                *) rm -f -- "$temporary"; printf 'error=invalidVersionIdentity\n'; exit 3 ;;
            esac
            version_value=${proposed_version##* }
            minimum_version="0.2.1.0"
            first_version=$(printf '%s\n%s\n' "$minimum_version" "$version_value" | sort -V | head -n 1)
            if [ "$first_version" != "$minimum_version" ]; then
                rm -f -- "$temporary"
                printf 'error=incompatibleVersion\n'
                exit 3
            fi

            if ! mv -f -- "$temporary" "$target"; then
                printf 'error=atomicReplaceFailed\n'
                exit 3
            fi

            installed_hash=$(sha256sum "$target" | awk '{print $1}')
            installed_version=$("$target" --version 2>/dev/null)
            if [ "$installed_hash" != "$expected_hash" ] ||
               [ "$installed_version" != "$proposed_version" ]; then
                cp -- "$backup" "$temporary" &&
                    chown root:root "$temporary" &&
                    chmod 0755 "$temporary" &&
                    mv -f -- "$temporary" "$target"
                logger -t kelpie-helper-update rollback
                printf 'error=postVerificationFailed\n'
                exit 4
            fi

            rm -f -- "$staging"
            logger -t kelpie-helper-update completed
            printf 'updated=true\n'
            printf 'version=%s\n' "$installed_version"
            printf 'sha256=%s\n' "$installed_hash"
            """));
    private readonly SshNetFileUploader _uploader = new();
    private readonly ISshCommandRunner _runner = new SshNetCommandRunner();

    public Task UploadAsync(
        SshConnectionProfile profile,
        string localPath,
        CancellationToken cancellationToken)
    {
        return _uploader.UploadAsync(profile, localPath, StagingPath, cancellationToken);
    }

    public async Task<HelperUpdatePreview> PreviewAsync(
        SshConnectionProfile profile,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        ValidateHash(expectedHash);
        var version = await ExecuteAsync(profile, TargetPath + " --version", cancellationToken);
        var currentHash = await ExecuteAsync(profile, "/usr/bin/sha256sum " + TargetPath, cancellationToken);
        var stagedHash = await ExecuteAsync(profile, "/usr/bin/sha256sum " + StagingPath, cancellationToken);
        return new HelperUpdatePreview(
            version.StandardOutput.Trim(),
            ReadHash(currentHash),
            stagedHash.ExitCode == 0
                && stagedHash.StandardOutput.StartsWith(expectedHash, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<HelperUpdateResult> ApplyAsync(
        SshConnectionProfile profile,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        ValidateHash(expectedHash);
        var result = await ExecuteAsync(
            profile,
            BuildPrivilegedCommand(expectedHash),
            cancellationToken);
        if (result.ExitCode != 0)
        {
            return new HelperUpdateResult(
                false,
                "Helper update failed. The internal transaction attempted rollback when replacement had started.");
        }

        return result.StandardOutput.Contains("updated=true", StringComparison.Ordinal)
            && result.StandardOutput.Contains(expectedHash, StringComparison.OrdinalIgnoreCase)
            ? new HelperUpdateResult(true, "Helper update completed.")
            : new HelperUpdateResult(false, "Helper update verification output was invalid.");
    }

    internal static string BuildPrivilegedCommand(string expectedHash)
    {
        ValidateHash(expectedHash);
        return "sudo -n sh -c \"printf %s '"
            + UpdateScriptBase64
            + "' | base64 -d | sh -s -- '"
            + expectedHash.ToLowerInvariant()
            + "'\"";
    }

    private Task<SshCommandResult> ExecuteAsync(
        SshConnectionProfile profile,
        string command,
        CancellationToken cancellationToken)
    {
        return _runner.ExecuteAsync(
            new SshCommandRequest(
                profile,
                "human_helper_update_internal",
                command,
                Timeout,
                new Dictionary<string, string>(StringComparer.Ordinal)),
            cancellationToken);
    }

    private static void ValidateHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new InvalidOperationException("Helper SHA-256 is invalid.");
        }
    }

    private static string ReadHash(SshCommandResult result)
    {
        if (result.ExitCode != 0)
        {
            return "(unavailable)";
        }

        var value = result.StandardOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return value is not null
            && value.Length == 64
            && value.All(char.IsAsciiHexDigit)
                ? value.ToLowerInvariant()
                : "(unavailable)";
    }

}
