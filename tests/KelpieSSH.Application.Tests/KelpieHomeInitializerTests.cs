using System.Text.Json;
using FluentAssertions;
using Kelpie.Core;

namespace KelpieSSH.Application.Tests;

public sealed class KelpieHomeInitializerTests
{
    [Fact]
    public void Initialize_creates_sample_profile_by_default()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            var result = KelpieHomeInitializer.Initialize(homeDirectory);

            result.ProfileName.Should().Be("sample");
            File.Exists(Path.Combine(homeDirectory, "config", "kelpie.json")).Should().BeTrue();
            File.Exists(Path.Combine(homeDirectory, "config", "kelpiemcp.json")).Should().BeTrue();
            File.Exists(Path.Combine(homeDirectory, "profiles", "sample.json")).Should().BeTrue();
            Directory.Exists(Path.Combine(homeDirectory, "config")).Should().BeTrue();
            Directory.Exists(Path.Combine(homeDirectory, "keys")).Should().BeTrue();
            Directory.Exists(Path.Combine(homeDirectory, "dat")).Should().BeTrue();
            Directory.Exists(Path.Combine(homeDirectory, "logs")).Should().BeTrue();
            Directory.Exists(Path.Combine(homeDirectory, "bin", "mcp")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    [Fact]
    public void Initialize_creates_named_profile()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            KelpieHomeInitializer.Initialize(homeDirectory, "vps01");
            var profilePath = Path.Combine(homeDirectory, "profiles", "vps01.json");

            var profileJson = File.ReadAllText(profilePath);

            profileJson.Should().Contain("vps01_ed25519");
            profileJson.Should().Contain("example.invalid");
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    [Fact]
    public void Initialize_does_not_overwrite_existing_files()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            var configDirectory = Path.Combine(homeDirectory, "config");
            Directory.CreateDirectory(configDirectory);
            var kelpieConfigPath = Path.Combine(configDirectory, "kelpie.json");
            File.WriteAllText(kelpieConfigPath, "{ \"KelpieHome\": \"legacy\", \"LogDirectory\": \"custom\" }");

            var result = KelpieHomeInitializer.Initialize(homeDirectory);

            File.ReadAllText(kelpieConfigPath).Should().Contain("custom");
            File.ReadAllText(kelpieConfigPath).Should().NotContain("KelpieHome");
            result.ExistingFiles.Should().Contain(kelpieConfigPath);
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    [Fact]
    public void Initialize_updates_legacy_mcp_working_directory()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            var configDirectory = Path.Combine(homeDirectory, "config");
            Directory.CreateDirectory(configDirectory);
            var kelpieMcpConfigPath = Path.Combine(configDirectory, "kelpiemcp.json");
            File.WriteAllText(
                kelpieMcpConfigPath,
                $$"""
                {
                  "Commands": {
                    "WorkingDirectory": "{{Path.Combine(homeDirectory, "bin", "mcp").Replace("\\", "\\\\", StringComparison.Ordinal)}}"
                  }
                }
                """);

            KelpieHomeInitializer.Initialize(homeDirectory);

            File.ReadAllText(kelpieMcpConfigPath)
                .Should()
                .Contain(Path.Combine(homeDirectory, "bin").Replace("\\", "\\\\", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    [Fact]
    public void Initialize_creates_profile_operations_with_cli_and_mcp_policy()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            KelpieHomeInitializer.Initialize(homeDirectory);
            var kelpieMcpConfigPath = Path.Combine(homeDirectory, "config", "kelpiemcp.json");

            using var document = JsonDocument.Parse(File.ReadAllText(kelpieMcpConfigPath));
            var profileOperations = document.RootElement.GetProperty("ProfileOperations");

            profileOperations.GetProperty("Add").GetProperty("CLI").GetString().Should().Be("Allowed");
            profileOperations.GetProperty("Add").GetProperty("MCP").GetString().Should().Be("Deny");
            profileOperations.GetProperty("Reload").GetProperty("CLI").GetString().Should().Be("Allowed");
            profileOperations.GetProperty("Reload").GetProperty("MCP").GetString().Should().Be("Deny");
            profileOperations.GetProperty("Revoke").GetProperty("CLI").GetString().Should().Be("Allowed");
            profileOperations.GetProperty("Revoke").GetProperty("MCP").GetString().Should().Be("Deny");
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    [Fact]
    public void Initialize_migrates_legacy_profile_operations_boolean_values()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            var configDirectory = Path.Combine(homeDirectory, "config");
            Directory.CreateDirectory(configDirectory);
            var kelpieMcpConfigPath = Path.Combine(configDirectory, "kelpiemcp.json");
            File.WriteAllText(
                kelpieMcpConfigPath,
                """
                {
                  "Server": {
                    "ControlPipeName": "KelpieMCPServer.Control"
                  },
                  "ProfileOperations": {
                    "Reload": {
                      "MCP": false
                    }
                  }
                }
                """);

            KelpieHomeInitializer.Initialize(homeDirectory);

            using var document = JsonDocument.Parse(File.ReadAllText(kelpieMcpConfigPath));
            var profileOperations = document.RootElement.GetProperty("ProfileOperations");
            profileOperations.GetProperty("Reload").GetProperty("MCP").GetString().Should().Be("Deny");
            profileOperations.GetProperty("Reload").GetProperty("CLI").GetString().Should().Be("Allowed");
            profileOperations.GetProperty("Add").GetProperty("CLI").GetString().Should().Be("Allowed");
            profileOperations.GetProperty("Revoke").GetProperty("CLI").GetString().Should().Be("Allowed");
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    [Fact]
    public void Initialize_writes_valid_json_files()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            KelpieHomeInitializer.Initialize(homeDirectory, "vps01");
            var jsonPaths = new[]
            {
                Path.Combine(homeDirectory, "config", "kelpie.json"),
                Path.Combine(homeDirectory, "config", "kelpiemcp.json"),
                Path.Combine(homeDirectory, "profiles", "vps01.json"),
            };

            foreach (var jsonPath in jsonPaths)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
                document.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
            }
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProfile_creates_profile_in_initialized_home()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            KelpieHomeInitializer.Initialize(homeDirectory);

            var profilePath = KelpieHomeInitializer.CreateProfile(homeDirectory, "vps02");

            File.Exists(profilePath).Should().BeTrue();
            profilePath.Should().Be(Path.Combine(homeDirectory, "profiles", "vps02.json"));
            File.ReadAllText(profilePath).Should().Contain("vps02_ed25519");
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProfile_rejects_existing_profile()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            KelpieHomeInitializer.Initialize(homeDirectory, "vps01");

            var action = () => KelpieHomeInitializer.CreateProfile(homeDirectory, "vps01");

            action.Should().Throw<IOException>().WithMessage("SSH profile already exists: vps01");
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreateProfile_requires_initialized_home()
    {
        var homeDirectory = CreateTempDirectory();

        try
        {
            var action = () => KelpieHomeInitializer.CreateProfile(homeDirectory, "vps01");

            action.Should().Throw<DirectoryNotFoundException>()
                .WithMessage("Kelpie home is not initialized. Run `kelpie init` first.");
        }
        finally
        {
            Directory.Delete(homeDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kelpie-init-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
