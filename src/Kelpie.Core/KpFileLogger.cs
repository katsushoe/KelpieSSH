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
    private const string MutexPrefix = "Global\\KpFileLogger_";

    private readonly string _fileName;
    private readonly string _logDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="KpFileLogger"/> class.
    /// </summary>
    /// <param name="logDir">The log directory.</param>
    /// <param name="fileName">The log file name.</param>
    public KpFileLogger(string logDir, string fileName)
    {
        _logDir = logDir;
        _fileName = fileName;
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
