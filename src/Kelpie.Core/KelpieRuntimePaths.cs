using System.Text.Json;

namespace Kelpie.Core;

/// <summary>
/// Resolves Kelpie runtime directories from configuration files.
/// </summary>
public static class KelpieRuntimePaths
{
    private const string HomeEnvironmentVariableName = "KELPIE_HOME";
    private static readonly AsyncLocal<KelpieRuntimePathOverrides?> CurrentOverrides = new();

    /// <summary>
    /// Gets the Kelpie configuration file name.
    /// </summary>
    public const string ConfigFileName = KelpieConfigFileName;

    /// <summary>
    /// Gets the Kelpie CLI configuration file name.
    /// </summary>
    public const string KelpieConfigFileName = "kelpie.json";

    /// <summary>
    /// Gets the Kelpie MCP frontend/server configuration file name.
    /// </summary>
    public const string KelpieMcpConfigFileName = "kelpiemcp.json";

    /// <summary>
    /// Gets the MCP trust store file name.
    /// </summary>
    public const string KelpieMcpTrustStoreFileName = "mcp_trusted_store.dat";

    /// <summary>
    /// Gets the canonical Kelpie CLI client-state file name.
    /// </summary>
    public const string KelpieClientStateFileName = "kelpie_client_state.json";

    /// <summary>
    /// Gets the legacy Kelpie CLI client-state file name.
    /// </summary>
    public const string LegacyStormStateFileName = "storm_state.dat";

    /// <summary>
    /// Gets the Kelpie Desktop configuration file name.
    /// </summary>
    public const string KelpieDesktopConfigFileName = "kelpie_desktop.json";

    /// <summary>
    /// Gets the current command-line runtime directory overrides.
    /// </summary>
    public static KelpieRuntimePathOverrides Overrides => CurrentOverrides.Value ?? KelpieRuntimePathOverrides.Empty;

    /// <summary>
    /// Sets command-line runtime directory overrides.
    /// </summary>
    /// <param name="pathOverrides">The runtime directory overrides.</param>
    public static void SetOverrides(KelpieRuntimePathOverrides pathOverrides)
    {
        CurrentOverrides.Value = pathOverrides;
    }

    /// <summary>
    /// Gets the canonical Kelpie CLI client-state path.
    /// </summary>
    public static string GetClientStatePath(string baseDirectory)
    {
        return Path.Combine(GetDataDirectory(baseDirectory), KelpieClientStateFileName);
    }

    /// <summary>
    /// Gets the legacy Kelpie CLI client-state path.
    /// </summary>
    public static string GetLegacyClientStatePath(string baseDirectory)
    {
        return Path.Combine(GetDataDirectory(baseDirectory), LegacyStormStateFileName);
    }

    /// <summary>
    /// Renames the legacy client-state file once when the canonical file does not exist.
    /// </summary>
    /// <returns><c>true</c> when migration occurred.</returns>
    public static bool MigrateLegacyClientStateFile(string baseDirectory)
    {
        var clientStatePath = GetClientStatePath(baseDirectory);
        if (File.Exists(clientStatePath))
        {
            return false;
        }

        var legacyStatePath = GetLegacyClientStatePath(baseDirectory);
        if (!File.Exists(legacyStatePath))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(clientStatePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            File.Move(legacyStatePath, clientStatePath);
            return true;
        }
        catch (IOException) when (File.Exists(clientStatePath))
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the Kelpie home directory from the command base directory.
    /// </summary>
    /// <param name="baseDirectory">The command base directory.</param>
    /// <returns>The Kelpie home directory.</returns>
    public static string GetHomeDirectory(string baseDirectory)
    {
        var pathOverrides = Overrides;
        if (!string.IsNullOrWhiteSpace(pathOverrides.BinDirectory))
        {
            var binDirectory = new DirectoryInfo(
                Path.GetFullPath(pathOverrides.BinDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return binDirectory.Parent?.FullName ?? binDirectory.FullName;
        }

        var environmentHomeDirectory = Environment.GetEnvironmentVariable(HomeEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentHomeDirectory)
            && Directory.Exists(environmentHomeDirectory))
        {
            return Path.GetFullPath(environmentHomeDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return GetLayoutHomeDirectory(baseDirectory);
    }

    /// <summary>
    /// Gets the home configuration file path.
    /// </summary>
    /// <param name="baseDirectory">The command base directory.</param>
    /// <param name="fileName">The configuration file name.</param>
    /// <returns>The home configuration file path.</returns>
    public static string GetHomeConfigFilePath(string baseDirectory, string fileName)
    {
        return Path.Combine(GetConfigDirectory(baseDirectory), fileName);
    }

    private static string GetLayoutHomeDirectory(string baseDirectory)
    {
        var commandDirectory = new DirectoryInfo(
            Path.GetFullPath(baseDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return commandDirectory.Parent?.FullName ?? commandDirectory.FullName;
    }

    /// <summary>
    /// Gets the configuration directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <returns>The configuration directory.</returns>
    public static string GetConfigDirectory(string fallbackDirectory)
    {
        var pathOverrides = Overrides;
        if (!string.IsNullOrWhiteSpace(pathOverrides.ConfigDirectory))
        {
            return Path.GetFullPath(pathOverrides.ConfigDirectory);
        }

        return Path.Combine(GetHomeDirectory(fallbackDirectory), "config");
    }

    /// <summary>
    /// Gets the binary directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no binary directory is configured.</param>
    /// <returns>The binary directory.</returns>
    public static string GetBinDirectory(string fallbackDirectory)
    {
        var pathOverrides = Overrides;
        return !string.IsNullOrWhiteSpace(pathOverrides.BinDirectory)
            ? Path.GetFullPath(pathOverrides.BinDirectory)
            : Path.GetFullPath(fallbackDirectory);
    }

    /// <summary>
    /// Gets the key directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no key directory is configured.</param>
    /// <returns>The key directory.</returns>
    public static string GetKeysDirectory(string fallbackDirectory)
    {
        var pathOverrides = Overrides;
        return !string.IsNullOrWhiteSpace(pathOverrides.KeysDirectory)
            ? Path.GetFullPath(pathOverrides.KeysDirectory)
            : Path.Combine(GetHomeDirectory(fallbackDirectory), "keys");
    }

    /// <summary>
    /// Gets the runtime data directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <returns>The runtime data directory.</returns>
    public static string GetDataDirectory(string fallbackDirectory)
    {
        var pathOverrides = Overrides;
        if (!string.IsNullOrWhiteSpace(pathOverrides.DataDirectory))
        {
            return Path.GetFullPath(pathOverrides.DataDirectory);
        }

        return Path.Combine(GetHomeDirectory(fallbackDirectory), "dat");
    }

    /// <summary>
    /// Gets the log directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <returns>The log directory.</returns>
    public static string GetLogDirectory(string fallbackDirectory)
    {
        return GetLogDirectory(fallbackDirectory, configSectionName: null);
    }

    /// <summary>
    /// Gets the log directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <param name="configSectionName">The optional configuration section that may contain LogDirectory.</param>
    /// <returns>The log directory.</returns>
    public static string GetLogDirectory(string fallbackDirectory, string? configSectionName)
    {
        return GetLogDirectory(fallbackDirectory, ConfigFileName, configSectionName);
    }

    /// <summary>
    /// Gets the log directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <param name="configFileName">The configuration file name.</param>
    /// <param name="configSectionName">The optional configuration section that may contain LogDirectory.</param>
    /// <returns>The log directory.</returns>
    public static string GetLogDirectory(string fallbackDirectory, string configFileName, string? configSectionName)
    {
        return GetLogDirectorySearchPaths(fallbackDirectory, configFileName, configSectionName).First();
    }

    /// <summary>
    /// Gets log directory candidates in search order.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <param name="configSectionName">The optional configuration section that may contain LogDirectory.</param>
    /// <returns>The log directory candidates.</returns>
    public static IReadOnlyCollection<string> GetLogDirectorySearchPaths(string fallbackDirectory, string? configSectionName)
    {
        return GetLogDirectorySearchPaths(fallbackDirectory, ConfigFileName, configSectionName);
    }

    /// <summary>
    /// Gets log directory candidates in search order.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <param name="configFileName">The configuration file name.</param>
    /// <param name="configSectionName">The optional configuration section that may contain LogDirectory.</param>
    /// <returns>The log directory candidates.</returns>
    public static IReadOnlyCollection<string> GetLogDirectorySearchPaths(
        string fallbackDirectory,
        string configFileName,
        string? configSectionName)
    {
        var paths = new List<string>();
        var pathOverrides = Overrides;
        if (!string.IsNullOrWhiteSpace(pathOverrides.LogsDirectory))
        {
            paths.Add(Path.GetFullPath(pathOverrides.LogsDirectory));
            return paths;
        }

        var configuredLogDirectory = GetConfiguredLogDirectory(fallbackDirectory, configFileName, configSectionName);
        if (!string.IsNullOrWhiteSpace(configuredLogDirectory))
        {
            paths.Add(configuredLogDirectory);
        }

        var homeDirectory = GetHomeDirectory(fallbackDirectory);
        paths.Add(Path.Combine(homeDirectory, "logs"));
        var baseDirectory = Path.GetFullPath(fallbackDirectory);
        paths.Add(Path.Combine(baseDirectory, "logs"));
        paths.Add(baseDirectory);

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Gets a configuration file path under the configured config directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <param name="fileName">The configuration file name.</param>
    /// <returns>The configuration file path.</returns>
    public static string GetConfigFilePath(string fallbackDirectory, string fileName)
    {
        return GetHomeConfigFilePath(fallbackDirectory, fileName);
    }

    /// <summary>
    /// Gets the fixed SSH profile directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <returns>The SSH profile directory.</returns>
    public static string GetProfilesDirectory(string fallbackDirectory)
    {
        var pathOverrides = Overrides;
        if (!string.IsNullOrWhiteSpace(pathOverrides.ProfilesDirectory))
        {
            return Path.GetFullPath(pathOverrides.ProfilesDirectory);
        }

        return Path.Combine(GetHomeDirectory(fallbackDirectory), "profiles");
    }

    /// <summary>
    /// Resolves a path relative to the configuration directory.
    /// </summary>
    /// <param name="fallbackDirectory">The fallback directory used when no home directory is configured.</param>
    /// <param name="path">The path to resolve.</param>
    /// <returns>The resolved path.</returns>
    public static string ResolveConfigPath(string fallbackDirectory, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(GetConfigDirectory(fallbackDirectory), path));
    }

    private static string? GetConfiguredLogDirectory(
        string fallbackDirectory,
        string configFileName,
        string? configSectionName)
    {
        var configFilePath = GetConfigFilePath(fallbackDirectory, configFileName);
        if (!File.Exists(configFilePath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configFilePath));
            var root = document.RootElement;

            if (!string.IsNullOrWhiteSpace(configSectionName)
                && TryGetProperty(root, configSectionName, out var section)
                && TryReadLogDirectory(section, out var sectionLogDirectory))
            {
                return ResolveLogDirectoryPath(fallbackDirectory, sectionLogDirectory);
            }

            return TryReadLogDirectory(root, out var rootLogDirectory)
                ? ResolveLogDirectoryPath(fallbackDirectory, rootLogDirectory)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool TryReadLogDirectory(JsonElement element, out string logDirectory)
    {
        if (TryGetProperty(element, "LogDirectory", out var logDirectoryElement)
            && logDirectoryElement.ValueKind == JsonValueKind.String)
        {
            logDirectory = logDirectoryElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(logDirectory);
        }

        if (TryGetProperty(element, "LogDirecroy", out var typoLogDirectoryElement)
            && typoLogDirectoryElement.ValueKind == JsonValueKind.String)
        {
            logDirectory = typoLogDirectoryElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(logDirectory);
        }

        logDirectory = string.Empty;
        return false;
    }

    private static string ResolveLogDirectoryPath(string fallbackDirectory, string logDirectory)
    {
        return Path.IsPathRooted(logDirectory)
            ? Path.GetFullPath(logDirectory)
            : Path.GetFullPath(Path.Combine(GetConfigDirectory(fallbackDirectory), logDirectory));
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        foreach (var item in element.EnumerateObject())
        {
            if (string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = item.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
