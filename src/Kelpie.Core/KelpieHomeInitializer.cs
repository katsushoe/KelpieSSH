using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kelpie.Core;

/// <summary>
/// Creates the initial Kelpie home directory layout and sample configuration files.
/// </summary>
public static class KelpieHomeInitializer
{
    /// <summary>
    /// Gets the default sample profile name.
    /// </summary>
    public const string DefaultProfileName = "sample";

    /// <summary>
    /// Creates the Kelpie home directory layout and sample files.
    /// </summary>
    /// <param name="homeDirectory">The Kelpie home directory.</param>
    /// <param name="profileName">The SSH profile name to create.</param>
    /// <param name="templateOptions">The SSH profile template values. Defaults are used when this is <see langword="null" />.</param>
    /// <returns>The initialization result.</returns>
    public static KelpieHomeInitializationResult Initialize(
        string homeDirectory,
        string? profileName = null,
        KelpieProfileTemplateOptions? templateOptions = null,
        KelpieMcpConfigTemplateOptions? mcpConfigOptions = null)
    {
        return Initialize(homeDirectory, Path.Combine(Path.GetFullPath(homeDirectory), "bin"), profileName, templateOptions, mcpConfigOptions);
    }

    /// <summary>
    /// Creates the Kelpie home directory layout and sample files.
    /// </summary>
    /// <param name="homeDirectory">The Kelpie home directory.</param>
    /// <param name="commandDirectory">The Kelpie command directory.</param>
    /// <param name="profileName">The SSH profile name to create.</param>
    /// <param name="templateOptions">The SSH profile template values. Defaults are used when this is <see langword="null" />.</param>
    /// <returns>The initialization result.</returns>
    public static KelpieHomeInitializationResult Initialize(
        string homeDirectory,
        string commandDirectory,
        string? profileName = null,
        KelpieProfileTemplateOptions? templateOptions = null,
        KelpieMcpConfigTemplateOptions? mcpConfigOptions = null)
    {
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            throw new ArgumentException("Home directory is required.", nameof(homeDirectory));
        }

        if (string.IsNullOrWhiteSpace(commandDirectory))
        {
            throw new ArgumentException("Command directory is required.", nameof(commandDirectory));
        }

        var normalizedProfileName = NormalizeProfileName(profileName);
        var fullHomeDirectory = Path.GetFullPath(homeDirectory);
        var paths = CreatePaths(fullHomeDirectory, Path.GetFullPath(commandDirectory), normalizedProfileName);
        var createdDirectories = CreateDirectories(paths.Directories);
        var createdFiles = new List<string>();
        var existingFiles = new List<string>();

        var normalizedMcpConfigOptions = NormalizeMcpConfigTemplateOptions(paths, mcpConfigOptions);
        WriteConfigFile(paths.KelpieConfigFile, CreateKelpieConfigJson(paths), paths, includeServerCommand: false, normalizedMcpConfigOptions, createdFiles, existingFiles);
        WriteConfigFile(paths.KelpieMcpConfigFile, CreateKelpieMcpConfigJson(paths, normalizedMcpConfigOptions), paths, includeServerCommand: true, normalizedMcpConfigOptions, createdFiles, existingFiles);
        WriteFileIfMissing(paths.ProfileFile, CreateProfileJson(normalizedProfileName, templateOptions), createdFiles, existingFiles);

        return new KelpieHomeInitializationResult(
            fullHomeDirectory,
            normalizedProfileName,
            createdDirectories.ToArray(),
            createdFiles.ToArray(),
            existingFiles.ToArray());
    }

    /// <summary>
    /// Creates a new SSH profile sample file in an initialized Kelpie home directory.
    /// </summary>
    /// <param name="homeDirectory">The initialized Kelpie home directory.</param>
    /// <param name="profileName">The SSH profile name to create.</param>
    /// <returns>The created profile file path.</returns>
    public static string CreateProfile(
        string homeDirectory,
        string? profileName,
        KelpieProfileTemplateOptions? templateOptions = null)
    {
        var normalizedProfileName = NormalizeProfileName(profileName);
        var profileFile = GetCreatableProfilePath(homeDirectory, normalizedProfileName);

        File.WriteAllText(profileFile, CreateProfileJson(normalizedProfileName, templateOptions));
        return profileFile;
    }

    /// <summary>
    /// Gets the profile path when a new SSH profile can be created.
    /// </summary>
    /// <param name="homeDirectory">The initialized Kelpie home directory.</param>
    /// <param name="profileName">The SSH profile name to create.</param>
    /// <returns>The profile file path.</returns>
    public static string GetCreatableProfilePath(string homeDirectory, string? profileName)
    {
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            throw new ArgumentException("Home directory is required.", nameof(homeDirectory));
        }

        var fullHomeDirectory = Path.GetFullPath(homeDirectory);
        var configDirectory = Path.Combine(fullHomeDirectory, "config");
        var profilesDirectory = Path.Combine(fullHomeDirectory, "profiles");

        if (!Directory.Exists(configDirectory)
            || !Directory.Exists(profilesDirectory)
            || !File.Exists(Path.Combine(configDirectory, KelpieRuntimePaths.KelpieConfigFileName))
            || !File.Exists(Path.Combine(configDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName)))
        {
            throw new DirectoryNotFoundException("Kelpie home is not initialized. Run `kelpie init` first.");
        }

        var profileFile = GetProfilePath(fullHomeDirectory, profileName);
        if (File.Exists(profileFile))
        {
            throw new IOException($"SSH profile already exists: {NormalizeProfileName(profileName)}");
        }

        return profileFile;
    }

    /// <summary>
    /// Gets the SSH profile file path for a Kelpie home directory.
    /// </summary>
    /// <param name="homeDirectory">The Kelpie home directory.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <returns>The profile file path.</returns>
    public static string GetProfilePath(string homeDirectory, string? profileName)
    {
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            throw new ArgumentException("Home directory is required.", nameof(homeDirectory));
        }

        var normalizedProfileName = NormalizeProfileName(profileName);
        var fullHomeDirectory = Path.GetFullPath(homeDirectory);
        return Path.Combine(fullHomeDirectory, "profiles", $"{normalizedProfileName}.json");
    }

    private static string NormalizeProfileName(string? profileName)
    {
        var normalizedProfileName = string.IsNullOrWhiteSpace(profileName)
            ? DefaultProfileName
            : profileName.Trim();

        if (normalizedProfileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || normalizedProfileName.Contains('/', StringComparison.Ordinal)
            || normalizedProfileName.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("Profile name must be a file name without path separators.", nameof(profileName));
        }

        return normalizedProfileName;
    }

    private static KelpieHomePaths CreatePaths(string homeDirectory, string commandDirectory, string profileName)
    {
        var configDirectory = Path.Combine(homeDirectory, "config");
        var profilesDirectory = Path.Combine(homeDirectory, "profiles");
        var keysDirectory = Path.Combine(homeDirectory, "keys");
        var dataDirectory = Path.Combine(homeDirectory, "dat");
        var logsDirectory = Path.Combine(homeDirectory, "logs");
        var mcpDirectory = Path.Combine(commandDirectory, "mcp");

        return new KelpieHomePaths(
            homeDirectory,
            configDirectory,
            commandDirectory,
            profilesDirectory,
            keysDirectory,
            dataDirectory,
            logsDirectory,
            mcpDirectory,
            Path.Combine(configDirectory, KelpieRuntimePaths.KelpieConfigFileName),
            Path.Combine(configDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName),
            Path.Combine(profilesDirectory, $"{profileName}.json"));
    }

    private static IReadOnlyCollection<string> CreateDirectories(IReadOnlyCollection<string> directories)
    {
        var createdDirectories = new List<string>();
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                createdDirectories.Add(directory);
            }

            Directory.CreateDirectory(directory);
        }

        return createdDirectories;
    }

    private static void WriteFileIfMissing(
        string path,
        string content,
        ICollection<string> createdFiles,
        ICollection<string> existingFiles)
    {
        if (File.Exists(path))
        {
            existingFiles.Add(path);
            return;
        }

        File.WriteAllText(path, content);
        createdFiles.Add(path);
    }

    private static void WriteConfigFile(
        string path,
        string content,
        KelpieHomePaths paths,
        bool includeServerCommand,
        KelpieMcpConfigTemplateOptions mcpConfigOptions,
        ICollection<string> createdFiles,
        ICollection<string> existingFiles)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content);
            createdFiles.Add(path);
            return;
        }

        if (TryUpdateConfig(path, paths, includeServerCommand, mcpConfigOptions))
        {
            existingFiles.Add(path);
            return;
        }

        existingFiles.Add(path);
    }

    private static bool TryUpdateConfig(
        string path,
        KelpieHomePaths paths,
        bool includeServerCommand,
        KelpieMcpConfigTemplateOptions mcpConfigOptions)
    {
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (node is null)
            {
                return false;
            }

            var updated = RemovePropertyIfExists(node, "KelpieHome");
            updated |= SetStringIfMissingOrWhiteSpace(node, "LogDirectory", includeServerCommand ? mcpConfigOptions.LogDirectory : paths.LogsDirectory);

            if (includeServerCommand)
            {
                var server = GetOrCreateObject(node, "Server", ref updated);
                updated |= SetIntIfMissingOrInvalid(server, "Port", mcpConfigOptions.Port);
                updated |= SetStringIfMissingOrWhiteSpace(server, "ControlPipeName", mcpConfigOptions.ControlPipeName);

                var commands = GetOrCreateObject(node, "Commands", ref updated);
                updated |= SetStringIfMissingOrWhiteSpace(commands, "ExecutablePath", Path.Combine(paths.McpDirectory, GetMcpExecutableName()));
                updated |= SetWorkingDirectoryIfMissingOrLegacy(commands, paths);

                var profileOperations = GetOrCreateObject(node, "ProfileOperations", ref updated);
                updated |= SetProfileOperationDefaults(profileOperations);
            }
            else
            {
                updated |= SetStringIfMissing(node, "editor", string.Empty);
            }

            if (!updated)
            {
                return false;
            }

            File.WriteAllText(path, node.ToJsonString(GetJsonOptions()) + Environment.NewLine);
            return updated;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonObject GetOrCreateObject(JsonObject node, string propertyName, ref bool updated)
    {
        if (node[propertyName] is JsonObject child)
        {
            return child;
        }

        var created = new JsonObject();
        node[propertyName] = created;
        updated = true;
        return created;
    }

    private static bool RemovePropertyIfExists(JsonObject node, string propertyName)
    {
        foreach (var item in node)
        {
            if (string.Equals(item.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return node.Remove(item.Key);
            }
        }

        return false;
    }

    private static bool SetStringIfMissingOrWhiteSpace(JsonObject node, string propertyName, string value)
    {
        if (node[propertyName] is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out var currentValue)
            && !string.IsNullOrWhiteSpace(currentValue))
        {
            return false;
        }

        node[propertyName] = value;
        return true;
    }

    private static bool SetStringIfMissing(JsonObject node, string propertyName, string value)
    {
        if (node.ContainsKey(propertyName))
        {
            return false;
        }

        node[propertyName] = value;
        return true;
    }

    private static bool SetIntIfMissingOrInvalid(JsonObject node, string propertyName, int value)
    {
        if (node[propertyName] is JsonValue jsonValue
            && jsonValue.TryGetValue<int>(out var currentValue)
            && currentValue > 0)
        {
            return false;
        }

        node[propertyName] = value;
        return true;
    }

    private static bool SetWorkingDirectoryIfMissingOrLegacy(JsonObject node, KelpieHomePaths paths)
    {
        if (node["WorkingDirectory"] is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out var currentValue)
            && !string.IsNullOrWhiteSpace(currentValue)
            && !string.Equals(
                Path.GetFullPath(currentValue),
                Path.GetFullPath(paths.McpDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        node["WorkingDirectory"] = paths.CommandDirectory;
        return true;
    }

    private static string CreateKelpieConfigJson(KelpieHomePaths paths)
    {
        return Serialize(new
        {
            LogDirectory = paths.LogsDirectory,
            editor = string.Empty,
        });
    }

    private static string CreateKelpieMcpConfigJson(
        KelpieHomePaths paths,
        KelpieMcpConfigTemplateOptions mcpConfigOptions)
    {
        return Serialize(new
        {
            AllowedHosts = "localhost;127.0.0.1;[::1]",
            LogDirectory = mcpConfigOptions.LogDirectory,
            Server = new
            {
                Port = mcpConfigOptions.Port,
                ControlPipeName = mcpConfigOptions.ControlPipeName,
            },
            Commands = new
            {
                ExecutablePath = Path.Combine(paths.McpDirectory, GetMcpExecutableName()),
                WorkingDirectory = paths.CommandDirectory,
            },
            ProfileOperations = new
            {
                Add = new
                {
                    CLI = "Allow",
                    MCP = "Deny",
                },
                Reload = new
                {
                    CLI = "Allow",
                    MCP = "Deny",
                },
                Revoke = new
                {
                    CLI = "Allow",
                    MCP = "Deny",
                },
            },
        });
    }

    private static string CreateProfileJson(string profileName, KelpieProfileTemplateOptions? templateOptions = null)
    {
        var options = NormalizeProfileTemplateOptions(profileName, templateOptions);
        var allowedRoots = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(options.ReadOnlyRoot))
        {
            allowedRoots[options.ReadOnlyRoot] = "$ReadOnly";
        }

        if (!string.IsNullOrWhiteSpace(options.ReadWriteRoot))
        {
            allowedRoots[options.ReadWriteRoot] = "$ReadWrite";
        }

        var specialPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(options.DenyPattern))
        {
            specialPaths[options.DenyPattern] = "Deny";
        }

        var authentication = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Method"] = options.AuthMethod,
        };
        if (string.Equals(options.AuthMethod, "password", StringComparison.OrdinalIgnoreCase))
        {
            authentication["PasswordSecretName"] = options.PasswordSecretName;
        }
        else
        {
            authentication["PrivateKeyFile"] = options.PrivateKeyFile;
        }

        return Serialize(new
        {
            Host = new
            {
                Address = options.HostAddress,
                Port = options.Port,
            },
            Auth = authentication,
            DefaultUser = options.DefaultUser,
            Users = new Dictionary<string, object>
            {
                [options.DefaultUser] = new
                {
                    Mode = options.Mode,
                    AllowedRoots = allowedRoots,
                    SpecialPaths = specialPaths,
                },
            },
            Platform = new
            {
                OsFamily = options.OsFamily,
            },
        });
    }

    private static KelpieProfileTemplateOptions NormalizeProfileTemplateOptions(
        string profileName,
        KelpieProfileTemplateOptions? templateOptions)
    {
        var defaults = KelpieProfileTemplateOptions.CreateDefault(profileName);
        if (templateOptions is null)
        {
            return defaults;
        }

        var authMethod = string.IsNullOrWhiteSpace(templateOptions.AuthMethod)
            ? defaults.AuthMethod
            : templateOptions.AuthMethod.Trim();

        return new KelpieProfileTemplateOptions(
            HostAddress: string.IsNullOrWhiteSpace(templateOptions.HostAddress) ? defaults.HostAddress : templateOptions.HostAddress.Trim(),
            Port: templateOptions.Port > 0 ? templateOptions.Port : defaults.Port,
            AuthMethod: authMethod,
            PrivateKeyFile: string.IsNullOrWhiteSpace(templateOptions.PrivateKeyFile) ? defaults.PrivateKeyFile : templateOptions.PrivateKeyFile.Trim(),
            PasswordSecretName: string.IsNullOrWhiteSpace(templateOptions.PasswordSecretName) ? defaults.PasswordSecretName : templateOptions.PasswordSecretName.Trim(),
            DefaultUser: string.IsNullOrWhiteSpace(templateOptions.DefaultUser) ? defaults.DefaultUser : templateOptions.DefaultUser.Trim(),
            Mode: string.IsNullOrWhiteSpace(templateOptions.Mode) ? defaults.Mode : templateOptions.Mode.Trim(),
            OsFamily: string.IsNullOrWhiteSpace(templateOptions.OsFamily) ? defaults.OsFamily : templateOptions.OsFamily.Trim(),
            ReadOnlyRoot: templateOptions.ReadOnlyRoot?.Trim() ?? string.Empty,
            ReadWriteRoot: templateOptions.ReadWriteRoot?.Trim() ?? string.Empty,
            DenyPattern: templateOptions.DenyPattern?.Trim() ?? string.Empty);
    }

    private static KelpieMcpConfigTemplateOptions NormalizeMcpConfigTemplateOptions(
        KelpieHomePaths paths,
        KelpieMcpConfigTemplateOptions? templateOptions)
    {
        var defaults = KelpieMcpConfigTemplateOptions.CreateDefault(paths.LogsDirectory);
        if (templateOptions is null)
        {
            return defaults;
        }

        return new KelpieMcpConfigTemplateOptions(
            LogDirectory: string.IsNullOrWhiteSpace(templateOptions.LogDirectory) ? defaults.LogDirectory : templateOptions.LogDirectory.Trim(),
            Port: templateOptions.Port is >= 1 and <= 65535 ? templateOptions.Port : defaults.Port,
            ControlPipeName: string.IsNullOrWhiteSpace(templateOptions.ControlPipeName) ? defaults.ControlPipeName : templateOptions.ControlPipeName.Trim());
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, GetJsonOptions()) + Environment.NewLine;
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
        };
    }

    private static bool SetProfileOperationDefaults(JsonObject profileOperations)
    {
        var updated = false;
        SetOperationDefault(profileOperations, "Add", ref updated);
        SetOperationDefault(profileOperations, "Reload", ref updated);
        SetOperationDefault(profileOperations, "Revoke", ref updated);
        return updated;
    }

    private static void SetOperationDefault(JsonObject profileOperations, string operation, ref bool updated)
    {
        var operationNode = GetOrCreateObject(profileOperations, operation, ref updated);
        updated |= SetPermissionIfMissingOrLegacy(operationNode, "CLI", "Allow");
        updated |= SetPermissionIfMissingOrLegacy(operationNode, "MCP", "Deny");
    }

    private static bool SetPermissionIfMissingOrLegacy(JsonObject node, string propertyName, string defaultValue)
    {
        if (node[propertyName] is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolValue))
            {
                node[propertyName] = boolValue ? "Allow" : "Deny";
                return true;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && !string.IsNullOrWhiteSpace(stringValue))
            {
                return false;
            }
        }

        node[propertyName] = defaultValue;
        return true;
    }

    private static string GetMcpExecutableName()
    {
        return OperatingSystem.IsWindows()
            ? "KelpieMCPServer.exe"
            : "KelpieMCPServer";
    }

    private sealed record KelpieHomePaths(
        string HomeDirectory,
        string ConfigDirectory,
        string CommandDirectory,
        string ProfilesDirectory,
        string KeysDirectory,
        string DataDirectory,
        string LogsDirectory,
        string McpDirectory,
        string KelpieConfigFile,
        string KelpieMcpConfigFile,
        string ProfileFile)
    {
        public IReadOnlyCollection<string> Directories { get; } =
        [
            HomeDirectory,
            ConfigDirectory,
            CommandDirectory,
            ProfilesDirectory,
            KeysDirectory,
            DataDirectory,
            LogsDirectory,
            McpDirectory,
        ];
    }
}

/// <summary>
/// Defines values used to generate an SSH profile template.
/// </summary>
public sealed record KelpieProfileTemplateOptions(
    string HostAddress,
    int Port,
    string AuthMethod,
    string? PrivateKeyFile,
    string? PasswordSecretName,
    string DefaultUser,
    string Mode,
    string OsFamily,
    string ReadOnlyRoot,
    string ReadWriteRoot,
    string DenyPattern)
{
    /// <summary>
    /// Creates the default SSH profile template values.
    /// </summary>
    /// <param name="profileName">The SSH profile name.</param>
    /// <returns>The default template values.</returns>
    public static KelpieProfileTemplateOptions CreateDefault(string profileName)
    {
        return new KelpieProfileTemplateOptions(
            HostAddress: "example.invalid",
            Port: 22,
            AuthMethod: "privateKey",
            PrivateKeyFile: $"{profileName}_ed25519",
            PasswordSecretName: $"kelpie:{profileName}",
            DefaultUser: "deploy",
            Mode: "Safe",
            OsFamily: "debian",
            ReadOnlyRoot: "/var/log",
            ReadWriteRoot: "/var/www",
            DenyPattern: "**/.env");
    }
}

/// <summary>
/// Defines values used to generate an MCP server configuration template.
/// </summary>
public sealed record KelpieMcpConfigTemplateOptions(
    string LogDirectory,
    int Port,
    string ControlPipeName)
{
    /// <summary>
    /// Creates the default MCP server configuration template values.
    /// </summary>
    /// <param name="logDirectory">The default log directory.</param>
    /// <returns>The default template values.</returns>
    public static KelpieMcpConfigTemplateOptions CreateDefault(string logDirectory)
    {
        return new KelpieMcpConfigTemplateOptions(
            LogDirectory: logDirectory,
            Port: 45432,
            ControlPipeName: "KelpieMCPServer.Control");
    }
}
