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
        KpLog.AddLogger(new KpFileLogger(logDir, fileName), "IWDE");
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
}
