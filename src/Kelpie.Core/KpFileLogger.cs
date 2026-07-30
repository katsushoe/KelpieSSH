using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Kelpie.Core;

/// <summary>
/// Writes KpLog entries to a file with process-wide mutual exclusion.
/// </summary>
public sealed class KpFileLogger : KpLog.ILogger
{
    /// <summary>
    /// The default maximum active log file size.
    /// </summary>
    public const long DefaultMaxFileBytes = 10 * 1024 * 1024;

    /// <summary>
    /// The default number of rotated log files to retain.
    /// </summary>
    public const int DefaultRetainedFileCount = 5;

    private const string MutexPrefix = "Global\\KpFileLogger_";

    private readonly string _fileName;
    private readonly string _logDir;
    private readonly long _maxFileBytes;
    private readonly int _retainedFileCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="KpFileLogger"/> class.
    /// </summary>
    /// <param name="logDir">The log directory.</param>
    /// <param name="fileName">The log file name.</param>
    /// <param name="maxFileBytes">The maximum active log file size in bytes.</param>
    /// <param name="retainedFileCount">The number of rotated log files to retain.</param>
    public KpFileLogger(
        string logDir,
        string fileName,
        long maxFileBytes = DefaultMaxFileBytes,
        int retainedFileCount = DefaultRetainedFileCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedFileCount);

        _logDir = logDir;
        _fileName = fileName;
        _maxFileBytes = maxFileBytes;
        _retainedFileCount = retainedFileCount;
    }

    /// <summary>
    /// Gets the full log file path.
    /// </summary>
    public string LogFilePath => Path.Combine(_logDir, _fileName);

    /// <inheritdoc />
    public void WriteLine(
        KpLog.Level level,
        ConsoleColor foregroundColor,
        ConsoleColor backgroundColor,
        string message,
        object? data,
        string filename,
        int line)
    {
        try
        {
            Directory.CreateDirectory(_logDir);

            var lineText = data is null
                ? $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {KpLog.LevelMarker(level)} {message}\t\t\t\t{filename}({line})"
                : $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} {KpLog.LevelMarker(level)} {message} data={SerializeData(data)}\t\t\t\t{filename}({line})";

            using var mutex = FileMutexRegistry.Get(LogFilePath);
            try
            {
                mutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                // Continue writing; the previous owner exited unexpectedly.
            }

            try
            {
                var options = new FileStreamOptions
                {
                    Mode = FileMode.Append,
                    Access = FileAccess.Write,
                    Share = FileShare.ReadWrite,
                    Options = FileOptions.WriteThrough,
                };

                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                var lineBytes = encoding.GetByteCount(lineText + Environment.NewLine);
                RotateIfNeeded(lineBytes);

                using var fileStream = new FileStream(LogFilePath, options);
                using var writer = new StreamWriter(fileStream, encoding);
                writer.WriteLine(lineText);
                writer.Flush();
                fileStream.Flush(true);
            }
            finally
            {
                try
                {
                    mutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore double release or shutdown races.
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[KpFileLogger] write failed: {ex.GetType().Name}: {ex.Message}, path={LogFilePath}");
            Console.ResetColor();
        }
    }

    private void RotateIfNeeded(int pendingBytes)
    {
        var file = new FileInfo(LogFilePath);
        if (!file.Exists || file.Length + pendingBytes <= _maxFileBytes)
        {
            return;
        }

        if (_retainedFileCount == 0)
        {
            File.Delete(LogFilePath);
            return;
        }

        File.Delete(GetRotatedFilePath(_retainedFileCount));
        for (var generation = _retainedFileCount - 1; generation >= 1; generation--)
        {
            var source = GetRotatedFilePath(generation);
            if (File.Exists(source))
            {
                File.Move(source, GetRotatedFilePath(generation + 1));
            }
        }

        File.Move(LogFilePath, GetRotatedFilePath(1));
    }

    private string GetRotatedFilePath(int generation)
    {
        return $"{LogFilePath}.{generation}";
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

    private static class FileMutexRegistry
    {
        private static readonly ConcurrentDictionary<string, string> NameCache = new();

        public static Mutex Get(string path)
        {
            var name = NameCache.GetOrAdd(path, value => MutexPrefix + Sha1Hex(value));
            return new Mutex(false, name);
        }

        private static string Sha1Hex(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = SHA1.HashData(bytes);
            var builder = new StringBuilder(hash.Length * 2);

            foreach (var item in hash)
            {
                builder.Append(item.ToString("x2"));
            }

            return builder.ToString();
        }
    }
}
