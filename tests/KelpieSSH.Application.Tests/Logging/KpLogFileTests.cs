using FluentAssertions;
using Kelpie.Core;

namespace KelpieSSH.Application.Tests.Logging;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class KpLogFileTests : IDisposable
{
    private readonly string _baseDirectory;
    private readonly string _homeDirectory;

    public KpLogFileTests()
    {
        _homeDirectory = Path.Combine(Path.GetTempPath(), "KelpieTests", Guid.NewGuid().ToString("N"));
        _baseDirectory = Path.Combine(_homeDirectory, "bin");
        Directory.CreateDirectory(_baseDirectory);
    }

    [Fact]
    public void Configure_ShouldWriteAndAppendLogFile()
    {
        KpLogSetup.Configure(_baseDirectory, "test.log");

        KpLog.Info("first log line");
        KpLog.Warn("second log line");
        KpLog.Flush();

        var logFilePath = Path.Combine(_homeDirectory, "logs", "test.log");
        File.Exists(logFilePath).Should().BeTrue();

        var content = ReadAllTextWithRetry(logFilePath);
        content.Should().Contain("[I] first log line");
        content.Should().Contain("[W] second log line");
        content.Should().Contain("KpLogFileTests.cs");
    }

    [Fact]
    public void KpFileLogger_ShouldAppendWithoutReinitializingKpLog()
    {
        var logDirectory = Path.Combine(_baseDirectory, "logs");
        var logger = new KpFileLogger(logDirectory, "append.log");

        logger.WriteLine(KpLog.Level.Info, ConsoleColor.White, ConsoleColor.Black, "first", null, "Test.cs", 10);
        logger.WriteLine(KpLog.Level.Error, ConsoleColor.Red, ConsoleColor.Black, "second", null, "Test.cs", 20);

        var content = ReadAllTextWithRetry(Path.Combine(logDirectory, "append.log"));
        content.Should().Contain("[I] first");
        content.Should().Contain("[E] second");
        content.Should().Contain("Test.cs(10)");
        content.Should().Contain("Test.cs(20)");
    }

    [Fact]
    public void KpFileLogger_WhenSizeLimitExceeded_ShouldRotateAndRetainConfiguredGenerations()
    {
        var logDirectory = Path.Combine(_baseDirectory, "logs");
        var logger = new KpFileLogger(logDirectory, "rotate.log", maxFileBytes: 80, retainedFileCount: 2);

        logger.WriteLine(KpLog.Level.Info, ConsoleColor.White, ConsoleColor.Black, "first-generation", null, "Test.cs", 10);
        logger.WriteLine(KpLog.Level.Info, ConsoleColor.White, ConsoleColor.Black, "second-generation", null, "Test.cs", 20);
        logger.WriteLine(KpLog.Level.Info, ConsoleColor.White, ConsoleColor.Black, "third-generation", null, "Test.cs", 30);
        logger.WriteLine(KpLog.Level.Info, ConsoleColor.White, ConsoleColor.Black, "active-generation", null, "Test.cs", 40);

        ReadAllTextWithRetry(Path.Combine(logDirectory, "rotate.log")).Should().Contain("active-generation");
        ReadAllTextWithRetry(Path.Combine(logDirectory, "rotate.log.1")).Should().Contain("third-generation");
        ReadAllTextWithRetry(Path.Combine(logDirectory, "rotate.log.2")).Should().Contain("second-generation");
        File.Exists(Path.Combine(logDirectory, "rotate.log.3")).Should().BeFalse();
    }

    [Fact]
    public void KpFileLogger_WhenRetentionIsZero_ShouldDiscardFullLog()
    {
        var logDirectory = Path.Combine(_baseDirectory, "logs");
        var logger = new KpFileLogger(logDirectory, "discard.log", maxFileBytes: 80, retainedFileCount: 0);

        logger.WriteLine(KpLog.Level.Info, ConsoleColor.White, ConsoleColor.Black, "discard-me", null, "Test.cs", 10);
        logger.WriteLine(KpLog.Level.Info, ConsoleColor.White, ConsoleColor.Black, "keep-me", null, "Test.cs", 20);

        var content = ReadAllTextWithRetry(Path.Combine(logDirectory, "discard.log"));
        content.Should().Contain("keep-me");
        content.Should().NotContain("discard-me");
        File.Exists(Path.Combine(logDirectory, "discard.log.1")).Should().BeFalse();
    }

    [Fact]
    public void Configure_WithRotationSettings_ShouldApplyConfiguredLimitAndRetention()
    {
        var configDirectory = Path.Combine(_homeDirectory, "config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, KelpieRuntimePaths.KelpieConfigFileName),
            """
            {
              "LogRotation": {
                "MaxFileBytes": 80,
                "RetainedFileCount": 1
              }
            }
            """);

        KpLogSetup.Configure(
            _baseDirectory,
            "configured.log",
            KelpieRuntimePaths.KelpieConfigFileName,
            "kelpie");

        KpLog.Info("first-generation");
        KpLog.Info("second-generation");
        KpLog.Info("active-generation");
        KpLog.Flush();

        var logDirectory = Path.Combine(_homeDirectory, "logs");
        ReadAllTextWithRetry(Path.Combine(logDirectory, "configured.log")).Should().Contain("active-generation");
        File.Exists(Path.Combine(logDirectory, "configured.log.1")).Should().BeTrue();
        File.Exists(Path.Combine(logDirectory, "configured.log.2")).Should().BeFalse();
    }

    public void Dispose()
    {
        KpLog.Init(addConsoleLogger: false);
        KpLog.Flush();

        if (Directory.Exists(_homeDirectory))
        {
            DeleteDirectoryWithRetry(_homeDirectory);
        }
    }

    private static string ReadAllTextWithRetry(string path)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(50);
            }
        }

        return File.ReadAllText(path);
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(50);
            }
        }

        Directory.Delete(path, recursive: true);
    }
}
