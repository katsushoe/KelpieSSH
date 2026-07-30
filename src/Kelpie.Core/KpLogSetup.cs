using System.Text.Json;

namespace Kelpie.Core;

/// <summary>
/// Provides common KpLog initialization helpers.
/// </summary>
public static class KpLogSetup
{
    /// <summary>
    /// Initializes KpLog with console and file logging.
    /// </summary>
    /// <param name="baseDirectory">The command base directory used to resolve the Kelpie home directory.</param>
    /// <param name="fileName">The log file name.</param>
    /// <param name="configSectionName">The optional configuration section that may contain LogDirectory.</param>
    public static void Configure(
        string baseDirectory,
        string fileName,
        string? configSectionName = null,
        bool addConsoleLogger = false)
    {
        Configure(baseDirectory, fileName, KelpieRuntimePaths.ConfigFileName, configSectionName, addConsoleLogger);
    }

    /// <summary>
    /// Initializes KpLog with console and file logging.
    /// </summary>
    /// <param name="baseDirectory">The command base directory used to resolve the Kelpie home directory.</param>
    /// <param name="fileName">The log file name.</param>
    /// <param name="configFileName">The configuration file name.</param>
    /// <param name="configSectionName">The optional configuration section that may contain LogDirectory.</param>
    /// <param name="addConsoleLogger">Whether to add the console logger.</param>
    public static void Configure(
        string baseDirectory,
        string fileName,
        string configFileName,
        string? configSectionName,
        bool addConsoleLogger = false)
    {
        KpLog.Init(addConsoleLogger);
        var logDir = ResolveWritableLogDirectory(baseDirectory, configFileName, configSectionName);
        var rotation = ReadRotationOptions(baseDirectory, configFileName, configSectionName);
        KpLog.AddLogger(
            new KpFileLogger(logDir, fileName, rotation.MaxFileBytes, rotation.RetainedFileCount),
            "IWDE");
        KpLog.Flush();
        KpLog.Buffered = false;
    }

    private static string ResolveWritableLogDirectory(
        string baseDirectory,
        string configFileName,
        string? configSectionName)
    {
        var candidates = KelpieRuntimePaths.GetLogDirectorySearchPaths(baseDirectory, configFileName, configSectionName);
        foreach (var candidate in candidates)
        {
            try
            {
                Directory.CreateDirectory(candidate);
                return candidate;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return Path.GetFullPath(baseDirectory);
    }

    private static LogRotationOptions ReadRotationOptions(
        string baseDirectory,
        string configFileName,
        string? configSectionName)
    {
        var configPath = KelpieRuntimePaths.GetConfigFilePath(baseDirectory, configFileName);
        if (!File.Exists(configPath))
        {
            return LogRotationOptions.Default;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));
            var root = document.RootElement;
            if (!string.IsNullOrWhiteSpace(configSectionName)
                && TryGetProperty(root, configSectionName, out var section)
                && TryReadRotationOptions(section, out var sectionOptions))
            {
                return sectionOptions;
            }

            return TryReadRotationOptions(root, out var rootOptions)
                ? rootOptions
                : LogRotationOptions.Default;
        }
        catch (JsonException)
        {
            return LogRotationOptions.Default;
        }
        catch (IOException)
        {
            return LogRotationOptions.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return LogRotationOptions.Default;
        }
    }

    private static bool TryReadRotationOptions(JsonElement parent, out LogRotationOptions options)
    {
        options = LogRotationOptions.Default;
        if (!TryGetProperty(parent, "LogRotation", out var rotation)
            || rotation.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var maxFileBytes = TryGetInt64(rotation, "MaxFileBytes", out var configuredMaxFileBytes)
            && configuredMaxFileBytes > 0
                ? configuredMaxFileBytes
                : KpFileLogger.DefaultMaxFileBytes;
        var retainedFileCount = TryGetInt32(rotation, "RetainedFileCount", out var configuredRetainedFileCount)
            && configuredRetainedFileCount >= 0
                ? configuredRetainedFileCount
                : KpFileLogger.DefaultRetainedFileCount;

        options = new LogRotationOptions(maxFileBytes, retainedFileCount);
        return true;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetInt64(JsonElement element, string name, out long value)
    {
        value = default;
        return TryGetProperty(element, name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out value);
    }

    private static bool TryGetInt32(JsonElement element, string name, out int value)
    {
        value = default;
        return TryGetProperty(element, name, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private sealed record LogRotationOptions(long MaxFileBytes, int RetainedFileCount)
    {
        public static LogRotationOptions Default { get; } = new(
            KpFileLogger.DefaultMaxFileBytes,
            KpFileLogger.DefaultRetainedFileCount);
    }
}
