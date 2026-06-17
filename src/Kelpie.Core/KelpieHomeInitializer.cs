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
    /// <returns>The initialization result.</returns>
    public static KelpieHomeInitializationResult Initialize(string homeDirectory, string? profileName = null)
    {
        return Initialize(homeDirectory, Path.Combine(Path.GetFullPath(homeDirectory), "bin"), profileName);
    }

    /// <summary>
    /// Creates the Kelpie home directory layout and sample files.
    /// </summary>
    /// <param name="homeDirectory">The Kelpie home directory.</param>
    /// <param name="commandDirectory">The Kelpie command directory.</param>
    /// <param name="profileName">The SSH profile name to create.</param>
    /// <returns>The initialization result.</returns>
    public static KelpieHomeInitializationResult Initialize(
        string homeDirectory,
        string commandDirectory,
        string? profileName = null)
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

        WriteConfigFile(paths.KelpieConfigFile, CreateKelpieConfigJson(paths), paths, includeServerCommand: false, createdFiles, existingFiles);
        WriteConfigFile(paths.KelpieMcpConfigFile, CreateKelpieMcpConfigJson(paths), paths, includeServerCommand: true, createdFiles, existingFiles);
        WriteFileIfMissing(paths.ProfileFile, CreateProfileJson(normalizedProfileName), createdFiles, existingFiles);

        return new KelpieHomeInitializationResult(
            fullHomeDirectory,
            normalizedProfileName,
            createdDirectories.ToArray(),
            createdFiles.ToArray(),
            existingFiles.ToArray());
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
        ICollection<string> createdFiles,
        ICollection<string> existingFiles)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content);
            createdFiles.Add(path);
            return;
        }

        if (TryUpdateConfig(path, paths, includeServerCommand))
        {
            existingFiles.Add(path);
            return;
        }

        existingFiles.Add(path);
    }

    private static bool TryUpdateConfig(string path, KelpieHomePaths paths, bool includeServerCommand)
    {
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (node is null)
            {
                return false;
            }

            var updated = RemovePropertyIfExists(node, "KelpieHome");
            updated |= SetStringIfMissingOrWhiteSpace(node, "LogDirectory", paths.LogsDirectory);

            if (includeServerCommand)
            {
                var commands = GetOrCreateObject(node, "Commands", ref updated);
                updated |= SetStringIfMissingOrWhiteSpace(commands, "ExecutablePath", Path.Combine(paths.McpDirectory, GetMcpExecutableName()));
                updated |= SetWorkingDirectoryIfMissingOrLegacy(commands, paths);

                var profileOperations = GetOrCreateObject(node, "ProfileOperations", ref updated);
                var reload = GetOrCreateObject(profileOperations, "Reload", ref updated);
                updated |= SetBoolIfMissing(reload, "MCP", false);
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

    private static bool SetBoolIfMissing(JsonObject node, string propertyName, bool value)
    {
        if (node[propertyName] is not null)
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
        });
    }

    private static string CreateKelpieMcpConfigJson(KelpieHomePaths paths)
    {
        return Serialize(new
        {
            AllowedHosts = "localhost;127.0.0.1;[::1]",
            LogDirectory = paths.LogsDirectory,
            Server = new
            {
                Port = 45432,
                ControlPipeName = "KelpieMCPServer.Control",
            },
            Commands = new
            {
                ExecutablePath = Path.Combine(paths.McpDirectory, GetMcpExecutableName()),
                WorkingDirectory = paths.CommandDirectory,
            },
            ProfileOperations = new
            {
                Reload = new
                {
                    MCP = false,
                },
            },
        });
    }

    private static string CreateProfileJson(string profileName)
    {
        return Serialize(new
        {
            Host = new
            {
                Address = "example.invalid",
                Port = 22,
            },
            Auth = new
            {
                Method = "privateKey",
                PrivateKeyFile = $"{profileName}_ed25519",
            },
            DefaultUser = "deploy",
            Users = new Dictionary<string, object>
            {
                ["deploy"] = new
                {
                    Mode = "Safe",
                    AllowedRoots = new Dictionary<string, string>
                    {
                        ["/var/log"] = "$ReadOnly",
                        ["/var/www"] = "$ReadWrite",
                    },
                    SpecialPaths = new Dictionary<string, string>
                    {
                        ["**/.env"] = "Deny",
                    },
                },
            },
            Platform = new
            {
                OsFamily = "debian",
            },
        });
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
