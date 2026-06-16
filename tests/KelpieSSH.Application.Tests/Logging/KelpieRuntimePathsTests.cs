using FluentAssertions;
using Kelpie.Core;

namespace KelpieSSH.Application.Tests.Logging;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class KelpieRuntimePathsTests
{
    [Fact]
    public void ConfigFileName_ShouldBeKelpieJson()
    {
        KelpieRuntimePaths.ConfigFileName.Should().Be("kelpie.json");
        KelpieRuntimePaths.KelpieConfigFileName.Should().Be("kelpie.json");
        KelpieRuntimePaths.KelpieMcpConfigFileName.Should().Be("kelpiemcp.json");
    }

    [Fact]
    public void GetConfigDirectory_ShouldUseParentDirectory()
    {
        var homeDirectory = CreateHomeDirectory();
        var baseDirectory = Path.Combine(homeDirectory, "bin");

        var configDirectory = KelpieRuntimePaths.GetConfigDirectory(baseDirectory);

        configDirectory.Should().Be(Path.Combine(Path.GetFullPath(homeDirectory), "config"));
    }

    [Fact]
    public void GetLogDirectory_ShouldUseRootConfiguredLogDirectory()
    {
        var homeDirectory = CreateHomeDirectory();
        var baseDirectory = Path.Combine(homeDirectory, "bin");
        var configDirectory = Path.Combine(homeDirectory, "config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName),
            """
            {
              "LogDirectory": "configured-logs"
            }
            """);

        var logDirectory = KelpieRuntimePaths.GetLogDirectory(
            baseDirectory,
            KelpieRuntimePaths.KelpieMcpConfigFileName,
            "kelpiemcp");

        logDirectory.Should().Be(Path.Combine(Path.GetFullPath(configDirectory), "configured-logs"));
    }

    [Fact]
    public void GetLogDirectory_ShouldAcceptSectionConfiguredLogDirectoryForCompatibility()
    {
        var homeDirectory = CreateHomeDirectory();
        var baseDirectory = Path.Combine(homeDirectory, "bin");
        var configDirectory = Path.Combine(homeDirectory, "config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName),
            """
            {
              "kelpiemcp": {
                "LogDirectory": "configured-logs"
              }
            }
            """);

        var logDirectory = KelpieRuntimePaths.GetLogDirectory(
            baseDirectory,
            KelpieRuntimePaths.KelpieMcpConfigFileName,
            "kelpiemcp");

        logDirectory.Should().Be(Path.Combine(Path.GetFullPath(configDirectory), "configured-logs"));
    }

    [Fact]
    public void GetLogDirectory_ShouldAcceptLogDirectoryTypoForCompatibility()
    {
        var homeDirectory = CreateHomeDirectory();
        var baseDirectory = Path.Combine(homeDirectory, "bin");
        var configDirectory = Path.Combine(homeDirectory, "config");
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(
            Path.Combine(configDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName),
            """
            {
              "kelpiemcp": {
                "LogDirecroy": "typo-logs"
              }
            }
            """);

        var logDirectory = KelpieRuntimePaths.GetLogDirectory(
            baseDirectory,
            KelpieRuntimePaths.KelpieMcpConfigFileName,
            "kelpiemcp");

        logDirectory.Should().Be(Path.Combine(Path.GetFullPath(configDirectory), "typo-logs"));
    }

    [Fact]
    public void GetLogDirectory_ShouldUseKelpieHomeWhenLogDirectoryIsNotConfigured()
    {
        var homeDirectory = CreateHomeDirectory();
        var baseDirectory = Path.Combine(homeDirectory, "bin");

        var logDirectory = KelpieRuntimePaths.GetLogDirectory(
            baseDirectory,
            KelpieRuntimePaths.KelpieMcpConfigFileName,
            "kelpiemcp");

        logDirectory.Should().Be(Path.Combine(Path.GetFullPath(homeDirectory), "logs"));
    }

    [Fact]
    public void GetLogDirectorySearchPaths_ShouldUseStartupLogsBeforeStartupDirectory()
    {
        var homeDirectory = CreateHomeDirectory();
        var baseDirectory = Path.Combine(homeDirectory, "bin");

        var logDirectories = KelpieRuntimePaths.GetLogDirectorySearchPaths(
            baseDirectory,
            KelpieRuntimePaths.KelpieMcpConfigFileName,
            "kelpiemcp");

        logDirectories.Should().Equal(
            Path.Combine(Path.GetFullPath(homeDirectory), "logs"),
            Path.Combine(Path.GetFullPath(baseDirectory), "logs"),
            Path.GetFullPath(baseDirectory));
    }

    [Fact]
    public void GetProfilesDirectory_ShouldUseKelpieHomeProfilesDirectory()
    {
        var homeDirectory = CreateHomeDirectory();
        var baseDirectory = Path.Combine(homeDirectory, "bin");

        var resolvedPath = KelpieRuntimePaths.GetProfilesDirectory(baseDirectory);

        resolvedPath.Should().Be(Path.Combine(Path.GetFullPath(homeDirectory), "profiles"));
    }

    [Fact]
    public void GetHomeDirectory_ShouldUseBaseDirectoryParent()
    {
        var homeDirectory = CreateHomeDirectory();
        var baseDirectory = Path.Combine(homeDirectory, "bin");

        var resolvedPath = KelpieRuntimePaths.GetHomeDirectory(baseDirectory);

        resolvedPath.Should().Be(Path.GetFullPath(homeDirectory));
    }

    private static string CreateHomeDirectory()
    {
        var homeDirectory = Path.Combine(Path.GetTempPath(), "kelpie-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(homeDirectory, "bin"));
        return homeDirectory;
    }
}
