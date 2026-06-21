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

    [Fact]
    public void RuntimePathOverrides_ShouldOverrideIndividualDirectories()
    {
        var homeDirectory = CreateHomeDirectory();
        var baseDirectory = Path.Combine(homeDirectory, "bin");
        var overrideRoot = Path.Combine(Path.GetTempPath(), "kelpie-overrides-" + Guid.NewGuid().ToString("N"));
        var overrides = new KelpieRuntimePathOverrides(
            ConfigDirectory: Path.Combine(overrideRoot, "cfg"),
            ProfilesDirectory: Path.Combine(overrideRoot, "ssh-profiles"),
            LogsDirectory: Path.Combine(overrideRoot, "logs"),
            BinDirectory: Path.Combine(overrideRoot, "runtime-bin"),
            KeysDirectory: Path.Combine(overrideRoot, "secret-keys"),
            DataDirectory: Path.Combine(overrideRoot, "state"));

        try
        {
            KelpieRuntimePaths.SetOverrides(overrides);

            KelpieRuntimePaths.GetConfigDirectory(baseDirectory).Should().Be(Path.GetFullPath(overrides.ConfigDirectory!));
            KelpieRuntimePaths.GetProfilesDirectory(baseDirectory).Should().Be(Path.GetFullPath(overrides.ProfilesDirectory!));
            KelpieRuntimePaths.GetLogDirectory(baseDirectory).Should().Be(Path.GetFullPath(overrides.LogsDirectory!));
            KelpieRuntimePaths.GetBinDirectory(baseDirectory).Should().Be(Path.GetFullPath(overrides.BinDirectory!));
            KelpieRuntimePaths.GetKeysDirectory(baseDirectory).Should().Be(Path.GetFullPath(overrides.KeysDirectory!));
            KelpieRuntimePaths.GetDataDirectory(baseDirectory).Should().Be(Path.GetFullPath(overrides.DataDirectory!));
            KelpieRuntimePaths.GetHomeDirectory(baseDirectory).Should().Be(Path.GetFullPath(overrideRoot));
        }
        finally
        {
            KelpieRuntimePaths.SetOverrides(KelpieRuntimePathOverrides.Empty);
        }
    }

    [Fact]
    public void RuntimePathOverrideParser_ShouldRemoveGlobalDirectoryOptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "kelpie-overrides-" + Guid.NewGuid().ToString("N"));

        var parsed = KelpieRuntimePathOverrideParser.TryParse(
            [
                "--config-dir",
                Path.Combine(root, "config"),
                "profile",
                "show",
                "vps01",
                "--profiles-dir",
                Path.Combine(root, "profiles"),
                "--logs-dir",
                Path.Combine(root, "logs"),
                "--bin-dir",
                Path.Combine(root, "bin"),
                "--keys-dir",
                Path.Combine(root, "keys"),
                "--dat-dir",
                Path.Combine(root, "dat"),
            ],
            out var remainingArgs,
            out var overrides,
            out var errorMessage);

        parsed.Should().BeTrue(errorMessage);
        remainingArgs.Should().Equal("profile", "show", "vps01");
        overrides.ConfigDirectory.Should().Be(Path.GetFullPath(Path.Combine(root, "config")));
        overrides.ProfilesDirectory.Should().Be(Path.GetFullPath(Path.Combine(root, "profiles")));
        overrides.LogsDirectory.Should().Be(Path.GetFullPath(Path.Combine(root, "logs")));
        overrides.BinDirectory.Should().Be(Path.GetFullPath(Path.Combine(root, "bin")));
        overrides.KeysDirectory.Should().Be(Path.GetFullPath(Path.Combine(root, "keys")));
        overrides.DataDirectory.Should().Be(Path.GetFullPath(Path.Combine(root, "dat")));
    }

    [Fact]
    public void RuntimePathOverrideParser_ShouldPreserveArgumentsAfterDoubleDash()
    {
        var parsed = KelpieRuntimePathOverrideParser.TryParse(
            [
                "--config-dir",
                Path.Combine(Path.GetTempPath(), "kelpie-config"),
                "env",
                "set",
                "vps01",
                "KEY",
                "VALUE",
                "--",
                "tool",
                "--profiles-dir",
                "remote-value",
            ],
            out var remainingArgs,
            out _,
            out var errorMessage);

        parsed.Should().BeTrue(errorMessage);
        remainingArgs.Should().Equal(
            "env",
            "set",
            "vps01",
            "KEY",
            "VALUE",
            "--",
            "tool",
            "--profiles-dir",
            "remote-value");
    }

    private static string CreateHomeDirectory()
    {
        var homeDirectory = Path.Combine(Path.GetTempPath(), "kelpie-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(homeDirectory, "bin"));
        return homeDirectory;
    }
}
