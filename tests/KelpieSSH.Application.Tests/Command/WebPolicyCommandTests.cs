using FluentAssertions;
using KelpieServerCommand;

namespace KelpieSSH.Application.Tests.Command;

public sealed class WebPolicyCommandTests
{
    [Fact]
    public void RunList_ShouldPrintValidatedEntries()
    {
        var directory = CreateDirectory();
        var policyPath = Path.Combine(directory, "policy.json");
        File.WriteAllText(policyPath, """
            {
              "Sites": {
                "/var/www": {
                  "AllowedFiles": {
                    "/index.php": "Update",
                    "/generated.php": "Create"
                  }
                }
              }
            }
            """);
        var interaction = new TestInteraction(isInteractive: false);

        try
        {
            var exitCode = WebPolicyCommand.Run(
                ["list"],
                interaction,
                policyPath,
                Path.Combine(directory, "audit.jsonl"));

            exitCode.Should().Be(0);
            interaction.OutputText.Should().Contain("/var/www  /index.php  Update");
            interaction.OutputText.Should().Contain("/var/www  /generated.php  Create");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RunList_ShouldRejectUnknownPolicyProperties()
    {
        var directory = CreateDirectory();
        var policyPath = Path.Combine(directory, "policy.json");
        File.WriteAllText(policyPath, """{"Sites":{},"Unexpected":true}""");
        var interaction = new TestInteraction(isInteractive: false);

        try
        {
            var exitCode = WebPolicyCommand.Run(
                ["list"],
                interaction,
                policyPath,
                Path.Combine(directory, "audit.jsonl"));

            exitCode.Should().Be(1);
            interaction.ErrorText.Should().Contain("must contain only a Sites object");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("Update")]
    [InlineData("Create")]
    public void RunList_ShouldAcceptSupportedAccess(string access)
    {
        var directory = CreateDirectory();
        var policyPath = Path.Combine(directory, "policy.json");
        File.WriteAllText(
            policyPath,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Sites = new Dictionary<string, object>
                {
                    ["/srv/www"] = new
                    {
                        AllowedFiles = new Dictionary<string, string>
                        {
                            ["/file.php"] = access,
                        },
                    },
                },
            }));
        var interaction = new TestInteraction(isInteractive: false);

        try
        {
            WebPolicyCommand.Run(["list"], interaction, policyPath, Path.Combine(directory, "audit.jsonl"))
                .Should().Be(0);
            interaction.OutputText.Should().Contain(access);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RunAdd_ShouldRejectNonInteractiveExecutionBeforeWriting()
    {
        var directory = CreateDirectory();
        var policyPath = Path.Combine(directory, "policy.json");
        var original = """{"Sites":{}}""";
        File.WriteAllText(policyPath, original);
        var interaction = new TestInteraction(isInteractive: false);

        try
        {
            var exitCode = WebPolicyCommand.Run(
                ["add", "/var/www", "/index.php", "Update"],
                interaction,
                policyPath,
                Path.Combine(directory, "audit.jsonl"));

            exitCode.Should().Be(1);
            interaction.ErrorText.Should().Contain("interactive human terminal");
            File.ReadAllText(policyPath).Should().Be(original);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Run_ShouldNotOfferConfirmationBypassOption()
    {
        var directory = CreateDirectory();
        var policyPath = Path.Combine(directory, "policy.json");
        File.WriteAllText(policyPath, """{"Sites":{}}""");
        var interaction = new TestInteraction(isInteractive: false);

        try
        {
            var exitCode = WebPolicyCommand.Run(
                ["add", "/var/www", "/index.php", "Update", "--yes"],
                interaction,
                policyPath,
                Path.Combine(directory, "audit.jsonl"));

            exitCode.Should().Be(1);
            interaction.ErrorText.Should().Contain("Usage:");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "kelpie-web-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestInteraction(bool isInteractive) : IWebPolicyInteraction
    {
        private readonly StringWriter _output = new();
        private readonly StringWriter _error = new();

        public bool IsInteractive { get; } = isInteractive;

        public TextWriter Output => _output;

        public TextWriter Error => _error;

        public string OutputText => _output.ToString();

        public string ErrorText => _error.ToString();

        public string? ReadLine()
        {
            return null;
        }
    }
}
