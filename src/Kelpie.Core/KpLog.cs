using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Kelpie.Core;

/// <summary>
/// Provides simple buffered logging for Kelpie processes.
/// </summary>
public static class KpLog
{
    private static readonly object LoggerLock = new();
    private static readonly object BufferedLock = new();
    private static readonly object FlushLock = new();
    private static readonly ConcurrentQueue<LogItem> LogBuffer = new();

    private static List<Action<Level, ConsoleColor, ConsoleColor, string, object?, string, int>> _infoLoggers = new();
    private static List<Action<Level, ConsoleColor, ConsoleColor, string, object?, string, int>> _warnLoggers = new();
    private static List<Action<Level, ConsoleColor, ConsoleColor, string, object?, string, int>> _debugLoggers = new();
    private static List<Action<Level, ConsoleColor, ConsoleColor, string, object?, string, int>> _errorLoggers = new();

    private static bool _buffered = true;

    static KpLog()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Flush();
            }
            catch
            {
                // Logging must not prevent process exit.
            }
        };
    }

    /// <summary>
    /// Gets or sets a value indicating whether log entries are buffered until <see cref="Flush"/> is called.
    /// </summary>
    public static bool Buffered
    {
        get
        {
            lock (BufferedLock)
            {
                return _buffered;
            }
        }
        set
        {
            lock (BufferedLock)
            {
                _buffered = value;
            }
        }
    }

    /// <summary>
    /// Defines a log output target.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Writes one log entry.
        /// </summary>
        /// <param name="level">The log level.</param>
        /// <param name="foregroundColor">The console foreground color.</param>
        /// <param name="backgroundColor">The console background color.</param>
        /// <param name="message">The log message.</param>
        /// <param name="data">Optional structured data.</param>
        /// <param name="filename">The source filename.</param>
        /// <param name="line">The source line number.</param>
        void WriteLine(
            Level level,
            ConsoleColor foregroundColor,
            ConsoleColor backgroundColor,
            string message,
            object? data,
            string filename,
            int line);
    }

    /// <summary>
    /// Defines supported log levels.
    /// </summary>
    public enum Level
    {
        /// <summary>
        /// Informational log entry.
        /// </summary>
        Info,

        /// <summary>
        /// Warning log entry.
        /// </summary>
        Warn,

        /// <summary>
        /// Debug log entry.
        /// </summary>
        Debug,

        /// <summary>
        /// Error log entry.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Initializes logger state and adds the console logger.
    /// </summary>
    public static void Init(bool addConsoleLogger = true)
    {
        lock (LoggerLock)
        {
            _infoLoggers = new();
            _warnLoggers = new();
            _debugLoggers = new();
            _errorLoggers = new();

            if (addConsoleLogger)
            {
                _infoLoggers.Add(ConsoleLogHandler);
                _warnLoggers.Add(ConsoleLogHandler);
                _debugLoggers.Add(ConsoleLogHandler);
                _errorLoggers.Add(ConsoleLogHandler);
            }
        }

        Info("==========================================");
    }

    /// <summary>
    /// Adds a logger for the specified level marker characters.
    /// </summary>
    /// <param name="logger">The logger action.</param>
    /// <param name="types">The level marker characters: I, W, D, E.</param>
    public static void AddLogger(
        Action<Level, ConsoleColor, ConsoleColor, string, object?, string, int> logger,
        string types)
    {
        var normalizedTypes = (types ?? string.Empty).ToUpperInvariant();
        lock (LoggerLock)
        {
            if (normalizedTypes.Contains('I', StringComparison.Ordinal))
            {
                _infoLoggers.Add(logger);
            }

            if (normalizedTypes.Contains('W', StringComparison.Ordinal))
            {
                _warnLoggers.Add(logger);
            }

            if (normalizedTypes.Contains('D', StringComparison.Ordinal))
            {
                _debugLoggers.Add(logger);
            }

            if (normalizedTypes.Contains('E', StringComparison.Ordinal))
            {
                _errorLoggers.Add(logger);
            }
        }
    }

    /// <summary>
    /// Adds a logger for the specified level marker characters.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="types">The level marker characters: I, W, D, E.</param>
    public static void AddLogger(ILogger logger, string types)
    {
        AddLogger(logger.WriteLine, types);
    }

    /// <summary>
    /// Flushes buffered log entries to configured loggers.
    /// </summary>
    public static void Flush()
    {
        lock (FlushLock)
        {
            List<Action<Level, ConsoleColor, ConsoleColor, string, object?, string, int>> infoLoggers;
            List<Action<Level, ConsoleColor, ConsoleColor, string, object?, string, int>> warnLoggers;
            List<Action<Level, ConsoleColor, ConsoleColor, string, object?, string, int>> debugLoggers;
            List<Action<Level, ConsoleColor, ConsoleColor, string, object?, string, int>> errorLoggers;

            lock (LoggerLock)
            {
                infoLoggers = _infoLoggers.ToList();
                warnLoggers = _warnLoggers.ToList();
                debugLoggers = _debugLoggers.ToList();
                errorLoggers = _errorLoggers.ToList();
            }

            while (LogBuffer.TryDequeue(out var item))
            {
                var targets = item.Level switch
                {
                    Level.Info => infoLoggers,
                    Level.Warn => warnLoggers,
                    Level.Debug => debugLoggers,
                    Level.Error => errorLoggers,
                    _ => infoLoggers,
                };

                foreach (var logger in targets)
                {
                    try
                    {
                        logger(
                            item.Level,
                            item.ForegroundColor,
                            item.BackgroundColor,
                            item.Message,
                            item.Data,
                            item.Filename,
                            item.LineNumber);
                    }
                    catch
                    {
                        // Logging targets must not take down the process.
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns a compact marker for a log level.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <returns>The level marker.</returns>
    public static string LevelMarker(Level level)
    {
        return level switch
        {
            Level.Info => "[I]",
            Level.Warn => "[W]",
            Level.Debug => "[D]",
            Level.Error => "[E]",
            _ => "[?]",
        };
    }

    /// <summary>
    /// Writes an informational log entry.
    /// </summary>
    public static void Info(
        string message,
        ConsoleColor color = ConsoleColor.White,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
    {
        Enqueue(Level.Info, color, message, null, lineNumber, filePath);
    }

    /// <summary>
    /// Writes a warning log entry.
    /// </summary>
    public static void Warn(
        string message,
        ConsoleColor color = ConsoleColor.Yellow,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
    {
        Enqueue(Level.Warn, color, message, null, lineNumber, filePath);
    }

    /// <summary>
    /// Writes a debug log entry.
    /// </summary>
    public static void Debug(
        string message,
        ConsoleColor color = ConsoleColor.Gray,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
    {
        Enqueue(Level.Debug, color, message, null, lineNumber, filePath);
    }

    /// <summary>
    /// Writes an error log entry.
    /// </summary>
    public static void Err(
        string message,
        ConsoleColor color = ConsoleColor.Red,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
    {
        Enqueue(Level.Error, color, message, null, lineNumber, filePath);
    }

    /// <summary>
    /// Writes an error log entry with exception details.
    /// </summary>
    public static void Err(
        string message,
        Exception exception,
        ConsoleColor color = ConsoleColor.Red,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
    {
        var detail = string.Join(
            Environment.NewLine,
            message,
            $"Exception: {exception.GetType().FullName ?? "UnknownException"}",
            $"exMessage: {exception.Message}",
            $"exInnerEx: {exception.InnerException?.ToString() ?? "(NoInnerException)"}",
            $"exStacktrace: {exception.StackTrace ?? "(NoStacktrace)"}");

        Enqueue(Level.Error, color, detail, null, lineNumber, filePath);
    }

    /// <summary>
    /// Writes an error log entry with exception details.
    /// </summary>
    public static void Err(
        Exception exception,
        ConsoleColor color = ConsoleColor.Red,
        [CallerLineNumber] int lineNumber = 0,
        [CallerFilePath] string filePath = "")
    {
        Err("Exception occurred.", exception, color, lineNumber, filePath);
    }

    private static void Enqueue(
        Level level,
        ConsoleColor color,
        string message,
        object? data,
        int lineNumber,
        string filePath)
    {
        LogBuffer.Enqueue(new LogItem(
            level,
            color,
            ConsoleColor.Black,
            message,
            data,
            Path.GetFileName(filePath),
            lineNumber));

        bool shouldFlush;
        lock (BufferedLock)
        {
            shouldFlush = !_buffered;
        }

        if (shouldFlush)
        {
            Flush();
        }
    }

    private static void ConsoleLogHandler(
        Level level,
        ConsoleColor foregroundColor,
        ConsoleColor backgroundColor,
        string message,
        object? data,
        string filename,
        int line)
    {
        Console.ForegroundColor = foregroundColor;
        Console.BackgroundColor = backgroundColor;
        Console.WriteLine(data is null ? message : $"{message} data={SerializeData(data)}");
        Console.ResetColor();
    }

    private static string SerializeData(object data)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        return JsonSerializer.Serialize(data, options);
    }

    private sealed record LogItem(
        Level Level,
        ConsoleColor ForegroundColor,
        ConsoleColor BackgroundColor,
        string Message,
        object? Data,
        string Filename,
        int LineNumber);
}
