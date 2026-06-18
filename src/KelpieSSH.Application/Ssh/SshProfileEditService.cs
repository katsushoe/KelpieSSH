using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Edits SSH profile JSON files while preserving existing fields.
/// </summary>
public sealed class SshProfileEditService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly IReadOnlyCollection<string> AllowedScalarPaths =
    [
        "Host.Address",
        "Host.Port",
        "Auth.Method",
        "Auth.PrivateKeyFile",
        "Auth.PasswordSecretName",
        "DefaultUser",
        "Users.<user>.Mode",
        "Platform.OsFamily",
        "Platform.PackageManager",
    ];

    private readonly IEditorLauncher editorLauncher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshProfileEditService" /> class.
    /// </summary>
    /// <param name="editorLauncher">The editor launcher used by editor mode.</param>
    public SshProfileEditService(IEditorLauncher editorLauncher)
    {
        this.editorLauncher = editorLauncher;
    }

    /// <summary>
    /// Sets a supported scalar value.
    /// </summary>
    /// <param name="profilePath">The profile JSON file path.</param>
    /// <param name="dotPath">The allowed scalar dot path.</param>
    /// <param name="value">The new value.</param>
    /// <returns>The edit result.</returns>
    public ProfileEditResult SetScalar(string profilePath, string dotPath, string value)
    {
        return EditNode(profilePath, node =>
        {
            var normalizedPath = dotPath?.Trim() ?? string.Empty;
            if (string.Equals(normalizedPath, "Host.Address", StringComparison.OrdinalIgnoreCase))
            {
                SetString(GetOrCreateObject(node, "Host"), "Address", value);
                return null;
            }

            if (string.Equals(normalizedPath, "Host.Port", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
                {
                    return "Host.Port must be a number between 1 and 65535.";
                }

                SetNumber(GetOrCreateObject(node, "Host"), "Port", port);
                return null;
            }

            if (string.Equals(normalizedPath, "Auth.Method", StringComparison.OrdinalIgnoreCase))
            {
                SetString(GetOrCreateObject(node, "Auth"), "Method", value);
                return null;
            }

            if (string.Equals(normalizedPath, "Auth.PrivateKeyFile", StringComparison.OrdinalIgnoreCase))
            {
                SetString(GetOrCreateObject(node, "Auth"), "PrivateKeyFile", value);
                return null;
            }

            if (string.Equals(normalizedPath, "Auth.PasswordSecretName", StringComparison.OrdinalIgnoreCase))
            {
                SetString(GetOrCreateObject(node, "Auth"), "PasswordSecretName", value);
                return null;
            }

            if (string.Equals(normalizedPath, "DefaultUser", StringComparison.OrdinalIgnoreCase))
            {
                SetString(node, "DefaultUser", value);
                return null;
            }

            if (string.Equals(normalizedPath, "Platform.OsFamily", StringComparison.OrdinalIgnoreCase))
            {
                SetString(GetOrCreateObject(node, "Platform"), "OsFamily", value);
                return null;
            }

            if (string.Equals(normalizedPath, "Platform.PackageManager", StringComparison.OrdinalIgnoreCase))
            {
                SetString(GetOrCreateObject(node, "Platform"), "PackageManager", value);
                return null;
            }

            var userMode = ParseUserModePath(normalizedPath);
            if (userMode is not null)
            {
                var userObject = GetOrCreateUserObject(node, userMode);
                SetString(userObject, "Mode", value);
                return null;
            }

            return CreateUnsupportedDotPathMessage(normalizedPath);
        });
    }

    /// <summary>
    /// Adds or updates an allowed root entry.
    /// </summary>
    /// <param name="profilePath">The profile JSON file path.</param>
    /// <param name="path">The allowed path or glob.</param>
    /// <param name="access">The access preset.</param>
    /// <returns>The edit result.</returns>
    public ProfileEditResult AddRoot(string profilePath, string path, string access)
    {
        var normalizedAccess = NormalizeAllowedRootAccess(access);
        if (normalizedAccess.Error is not null)
        {
            return ProfileEditResult.Fail(profilePath, normalizedAccess.Error);
        }

        return EditNode(profilePath, node =>
        {
            var allowedRoots = GetEffectiveUserRuleObject(node, "AllowedRoots");
            SetString(allowedRoots, path, normalizedAccess.Value!);
            return null;
        });
    }

    /// <summary>
    /// Removes an allowed root entry.
    /// </summary>
    /// <param name="profilePath">The profile JSON file path.</param>
    /// <param name="path">The allowed path or glob.</param>
    /// <returns>The edit result.</returns>
    public ProfileEditResult RemoveRoot(string profilePath, string path)
    {
        return EditNode(profilePath, node =>
        {
            var allowedRoots = GetEffectiveUserRuleObject(node, "AllowedRoots");
            RemoveCaseSensitive(allowedRoots, path);
            return null;
        });
    }

    /// <summary>
    /// Adds or updates a Deny special path entry.
    /// </summary>
    /// <param name="profilePath">The profile JSON file path.</param>
    /// <param name="pattern">The special path glob pattern.</param>
    /// <returns>The edit result.</returns>
    public ProfileEditResult AddDeny(string profilePath, string pattern)
    {
        return EditNode(profilePath, node =>
        {
            var specialPaths = GetEffectiveUserRuleObject(node, "SpecialPaths");
            SetString(specialPaths, pattern, "Deny");
            return null;
        });
    }

    /// <summary>
    /// Removes a Deny special path entry.
    /// </summary>
    /// <param name="profilePath">The profile JSON file path.</param>
    /// <param name="pattern">The special path glob pattern.</param>
    /// <returns>The edit result.</returns>
    public ProfileEditResult RemoveDeny(string profilePath, string pattern)
    {
        return EditNode(profilePath, node =>
        {
            var specialPaths = GetEffectiveUserRuleObject(node, "SpecialPaths");
            RemoveCaseSensitive(specialPaths, pattern);
            return null;
        });
    }

    /// <summary>
    /// Opens a profile in an editor and validates the result after the editor exits.
    /// </summary>
    /// <param name="profilePath">The profile JSON file path.</param>
    /// <param name="editorCommand">The editor command line.</param>
    /// <param name="chooseAfterValidationError">The decision provider called after a validation error.</param>
    /// <returns>The edit result.</returns>
    public ProfileEditResult EditWithEditor(
        string profilePath,
        string editorCommand,
        Func<string, ProfileEditRecoveryAction> chooseAfterValidationError)
    {
        if (!File.Exists(profilePath))
        {
            return ProfileEditResult.Fail(profilePath, $"SSH profile was not found: {Path.GetFileNameWithoutExtension(profilePath)}");
        }

        var originalContent = File.ReadAllText(profilePath);
        while (true)
        {
            var launchResult = editorLauncher.Launch(editorCommand, profilePath);
            if (!launchResult.Success)
            {
                WriteProfileContent(profilePath, originalContent);
                return ProfileEditResult.Fail(profilePath, launchResult.ErrorMessage ?? "Editor exited with an error.");
            }

            var validationError = ValidateProfileFile(profilePath);
            if (validationError is null)
            {
                return ProfileEditResult.Ok(profilePath);
            }

            var action = chooseAfterValidationError(validationError);
            if (action == ProfileEditRecoveryAction.Retry)
            {
                continue;
            }

            WriteProfileContent(profilePath, originalContent);
            return ProfileEditResult.Fail(profilePath, "Profile edit was canceled. Original file was restored.");
        }
    }

    private static ProfileEditResult EditNode(string profilePath, Func<JsonObject, string?> edit)
    {
        if (!File.Exists(profilePath))
        {
            return ProfileEditResult.Fail(profilePath, $"SSH profile was not found: {Path.GetFileNameWithoutExtension(profilePath)}");
        }

        JsonObject node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(profilePath)) as JsonObject
                ?? throw new InvalidOperationException("SSH profile JSON root must be an object.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return ProfileEditResult.Fail(profilePath, ex.Message);
        }

        var editError = edit(node);
        if (editError is not null)
        {
            return ProfileEditResult.Fail(profilePath, editError);
        }

        var content = Serialize(node);
        var validationError = ValidateProfileContent(profilePath, content);
        if (validationError is not null)
        {
            return ProfileEditResult.Fail(profilePath, validationError);
        }

        WriteProfileContent(profilePath, content);
        return ProfileEditResult.Ok(profilePath);
    }

    private static string? ValidateProfileFile(string profilePath)
    {
        try
        {
            _ = SshConnectionProfileFileLoader.LoadFile(profilePath);
            return null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return ex.Message;
        }
    }

    private static string? ValidateProfileContent(string profilePath, string content)
    {
        try
        {
            var options = JsonSerializer.Deserialize<SshConnectionProfileOptions>(content, ReadOptions)
                ?? throw new InvalidOperationException($"SSH profile file is empty: {profilePath}");
            options.Name = Path.GetFileNameWithoutExtension(profilePath);
            _ = options.ToProfile(Path.GetDirectoryName(Path.GetFullPath(profilePath)) ?? AppContext.BaseDirectory);
            return null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return ex.Message;
        }
    }

    private static string Serialize(JsonObject node)
    {
        return NormalizeLf(node.ToJsonString(WriteOptions)) + "\n";
    }

    private static void WriteProfileContent(string profilePath, string content)
    {
        var normalizedContent = NormalizeLf(content);
        var directory = Path.GetDirectoryName(Path.GetFullPath(profilePath)) ?? AppContext.BaseDirectory;
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(profilePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, normalizedContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, profilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string NormalizeLf(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        var existingName = FindPropertyName(parent, propertyName, StringComparison.OrdinalIgnoreCase);
        if (existingName is not null && parent[existingName] is JsonObject existingObject)
        {
            return existingObject;
        }

        var child = new JsonObject();
        SetNode(parent, propertyName, child);
        return child;
    }

    private static JsonObject GetOrCreateUserObject(JsonObject root, string userName)
    {
        var users = GetOrCreateObject(root, "Users");
        if (users[userName] is JsonObject existingUser)
        {
            return existingUser;
        }

        var user = new JsonObject();
        users[userName] = user;
        return user;
    }

    private static JsonObject GetEffectiveUserRuleObject(JsonObject root, string propertyName)
    {
        var defaultUser = ReadString(root, "DefaultUser");
        if (!string.IsNullOrWhiteSpace(defaultUser)
            && root[FindPropertyName(root, "Users", StringComparison.OrdinalIgnoreCase) ?? "Users"] is JsonObject users
            && users[defaultUser] is JsonObject userObject)
        {
            return GetOrCreateObject(userObject, propertyName);
        }

        return GetOrCreateObject(root, propertyName);
    }

    private static string? ReadString(JsonObject parent, string propertyName)
    {
        var existingName = FindPropertyName(parent, propertyName, StringComparison.OrdinalIgnoreCase);
        return existingName is not null
            && parent[existingName] is JsonValue value
            && value.TryGetValue<string>(out var text)
                ? text
                : null;
    }

    private static void SetString(JsonObject parent, string propertyName, string value)
    {
        SetNode(parent, propertyName, JsonValue.Create(value) ?? throw new InvalidOperationException("Failed to create JSON string value."));
    }

    private static void SetNumber(JsonObject parent, string propertyName, int value)
    {
        SetNode(parent, propertyName, JsonValue.Create(value) ?? throw new InvalidOperationException("Failed to create JSON number value."));
    }

    private static void SetNode(JsonObject parent, string propertyName, JsonNode value)
    {
        var existingName = FindPropertyName(parent, propertyName, StringComparison.OrdinalIgnoreCase);
        if (existingName is not null && !string.Equals(existingName, propertyName, StringComparison.Ordinal))
        {
            parent.Remove(existingName);
        }

        parent[propertyName] = value;
    }

    private static void RemoveCaseSensitive(JsonObject parent, string propertyName)
    {
        parent.Remove(propertyName);
    }

    private static string? FindPropertyName(JsonObject parent, string propertyName, StringComparison comparison)
    {
        foreach (var item in parent)
        {
            if (string.Equals(item.Key, propertyName, comparison))
            {
                return item.Key;
            }
        }

        return null;
    }

    private static string? ParseUserModePath(string dotPath)
    {
        var parts = dotPath.Split('.', StringSplitOptions.None);
        if (parts.Length == 3
            && string.Equals(parts[0], "Users", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(parts[1])
            && string.Equals(parts[2], "Mode", StringComparison.OrdinalIgnoreCase))
        {
            return parts[1];
        }

        return null;
    }

    private static (string? Value, string? Error) NormalizeAllowedRootAccess(string access)
    {
        var value = access?.Trim() ?? string.Empty;
        if (string.Equals(value, "ReadOnly", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "$ReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            return ("$ReadOnly", null);
        }

        if (string.Equals(value, "ReadWrite", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "$ReadWrite", StringComparison.OrdinalIgnoreCase))
        {
            return ("$ReadWrite", null);
        }

        return (null, "Allowed root access must be ReadOnly or ReadWrite.");
    }

    private static string CreateUnsupportedDotPathMessage(string dotPath)
    {
        var hint = dotPath.Contains("AllowedRoots", StringComparison.OrdinalIgnoreCase)
            || dotPath.Contains("SpecialPaths", StringComparison.OrdinalIgnoreCase)
                ? " Use add-root, rm-root, add-deny, or rm-deny for dictionary settings."
                : string.Empty;

        return $"Unsupported scalar path: {dotPath}.{hint} Allowed paths: {string.Join(", ", AllowedScalarPaths)}.";
    }
}

/// <summary>
/// Launches an external editor and waits until it exits.
/// </summary>
public interface IEditorLauncher
{
    /// <summary>
    /// Launches the editor command for a profile file.
    /// </summary>
    /// <param name="editorCommand">The editor command line.</param>
    /// <param name="profilePath">The profile file path.</param>
    /// <returns>The launch result.</returns>
    EditorLaunchResult Launch(string editorCommand, string profilePath);
}

/// <summary>
/// The result of launching an editor.
/// </summary>
/// <param name="Success">Whether the editor exited successfully.</param>
/// <param name="ExitCode">The editor exit code.</param>
/// <param name="ErrorMessage">The error message when launch failed.</param>
public sealed record EditorLaunchResult(bool Success, int ExitCode, string? ErrorMessage = null)
{
    /// <summary>
    /// Creates a successful launch result.
    /// </summary>
    /// <param name="exitCode">The editor exit code.</param>
    /// <returns>The launch result.</returns>
    public static EditorLaunchResult FromExitCode(int exitCode)
    {
        return exitCode == 0
            ? new EditorLaunchResult(true, exitCode)
            : new EditorLaunchResult(false, exitCode, $"Editor exited with code {exitCode}.");
    }

    /// <summary>
    /// Creates a failed launch result.
    /// </summary>
    /// <param name="message">The failure message.</param>
    /// <returns>The launch result.</returns>
    public static EditorLaunchResult Fail(string message)
    {
        return new EditorLaunchResult(false, -1, message);
    }
}

/// <summary>
/// The result of editing a profile.
/// </summary>
/// <param name="Success">Whether the edit succeeded.</param>
/// <param name="ProfilePath">The profile file path.</param>
/// <param name="ErrorMessage">The error message when the edit failed.</param>
public sealed record ProfileEditResult(bool Success, string ProfilePath, string? ErrorMessage = null)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="profilePath">The profile file path.</param>
    /// <returns>The edit result.</returns>
    public static ProfileEditResult Ok(string profilePath)
    {
        return new ProfileEditResult(true, profilePath);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="profilePath">The profile file path.</param>
    /// <param name="message">The failure message.</param>
    /// <returns>The edit result.</returns>
    public static ProfileEditResult Fail(string profilePath, string message)
    {
        return new ProfileEditResult(false, profilePath, message);
    }
}

/// <summary>
/// Defines how to continue after an editor validation error.
/// </summary>
public enum ProfileEditRecoveryAction
{
    /// <summary>
    /// Reopen the editor and validate again.
    /// </summary>
    Retry,

    /// <summary>
    /// Abort the edit and restore the original content.
    /// </summary>
    Abort,
}

/// <summary>
/// Resolves the editor command line used by profile edit mode.
/// </summary>
public static class ProfileEditorCommandResolver
{
    /// <summary>
    /// Resolves the editor command.
    /// </summary>
    /// <param name="configuredEditor">The configured kelpie.json editor value.</param>
    /// <param name="getEnvironmentVariable">The environment variable reader.</param>
    /// <param name="isWindows">Whether the current OS is Windows.</param>
    /// <returns>The editor command line.</returns>
    public static string Resolve(
        string? configuredEditor,
        Func<string, string?> getEnvironmentVariable,
        bool isWindows)
    {
        foreach (var value in new[]
        {
            configuredEditor,
            getEnvironmentVariable("KELPIE_EDITOR"),
            getEnvironmentVariable("VISUAL"),
            getEnvironmentVariable("EDITOR"),
        })
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return isWindows ? "notepad" : "vi";
    }
}

/// <summary>
/// Starts an external editor process.
/// </summary>
public sealed class ProcessEditorLauncher : IEditorLauncher
{
    /// <inheritdoc />
    public EditorLaunchResult Launch(string editorCommand, string profilePath)
    {
        var parts = SplitCommandLine(editorCommand);
        if (parts.Count == 0)
        {
            return EditorLaunchResult.Fail("Editor command is not configured.");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = parts[0],
                UseShellExecute = false,
            };

            foreach (var argument in parts.Skip(1))
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.ArgumentList.Add(profilePath);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return EditorLaunchResult.Fail("Failed to start editor.");
            }

            process.WaitForExit();
            return EditorLaunchResult.FromExitCode(process.ExitCode);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return EditorLaunchResult.Fail(ex.Message);
        }
    }

    private static IReadOnlyList<string> SplitCommandLine(string commandLine)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddCurrent(parts, current);
                continue;
            }

            current.Append(character);
        }

        AddCurrent(parts, current);
        return parts;
    }

    private static void AddCurrent(List<string> parts, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        parts.Add(current.ToString());
        current.Clear();
    }
}
