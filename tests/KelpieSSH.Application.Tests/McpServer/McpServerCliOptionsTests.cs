using FluentAssertions;
using KelpieMCPServer;
using Microsoft.Extensions.Configuration;

namespace KelpieSSH.Application.Tests.McpServer;

public sealed class McpServerCliOptionsTests
{
    [Fact]
    public void ParseStartupOptions_WhenPortIs45432_UsesPort45432()
    {
        var options = McpServerCliOptions.ParseStartupOptions(["--port", "45432"]);

        options.Port.Should().Be(45432);
        options.ServerUrl.Should().Be("http://127.0.0.1:45432");
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("65535", 65535)]
    public void ParseStartupOptions_WhenPortIsBoundary_AcceptsPort(string value, int expected)
    {
        var options = McpServerCliOptions.ParseStartupOptions(["--port", value]);

        options.Port.Should().Be(expected);
    }

    [Fact]
    public void ParseStartupOptions_WhenPortIsOmitted_UsesDefaultPort()
    {
        var options = McpServerCliOptions.ParseStartupOptions([]);

        options.Port.Should().Be(45432);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("65536")]
    public void ParseStartupOptions_WhenPortIsOutOfRange_RejectsPort(string value)
    {
        var action = () => McpServerCliOptions.ParseStartupOptions(["--port", value]);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParseStartupOptions_WhenPortIsNotNumeric_RejectsPort()
    {
        var action = () => McpServerCliOptions.ParseStartupOptions(["--port", "abc"]);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [MemberData(nameof(MissingPortValues))]
    public void ParseStartupOptions_WhenPortValueIsMissing_RejectsArguments(string[] args)
    {
        var action = () => McpServerCliOptions.ParseStartupOptions(args);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ParseStartupOptions_WhenConfigurationContainsServerPort_IgnoresConfigurationPort()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Port"] = "12345",
            })
            .Build();

        var options = McpServerCliOptions.ParseStartupOptions([]);

        configuration["Server:Port"].Should().Be("12345");
        options.Port.Should().Be(45432);
    }

    public static TheoryData<string[]> MissingPortValues => new()
    {
        new[] { "--port" },
        new[] { "--port=" },
        new[] { "--port", "--runtime-base", "runtime" },
    };
}
