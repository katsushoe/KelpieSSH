using System.Text;
using System.Text.Json;
using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class WebFileReadWireTests
{
    [Theory]
    [InlineData("")]
    [InlineData("AAH/")]
    [InlineData("44GCXG4=")]
    public void Decode_ShouldPreserveExactContentBytes(string content)
    {
        var result = WebFileReadWire.Decode(Result(Wire(content)), 64);
        result.ExitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(result.StandardOutput);
        doc.RootElement.GetProperty("contentBase64").GetString().Should().Be(content);
        doc.RootElement.GetProperty("size").GetInt32().Should().Be(Convert.FromBase64String(content).Length);
        doc.RootElement.GetProperty("lastModified").GetString().Should().Be("1970-01-01T00:00:00.123457Z");
        doc.RootElement.GetProperty("resolvedPath").GetString().Should().Be("/web/日本語\nfile");
    }

    [Fact]
    public void Decode_WhenMissing_ShouldReturnMissingWithoutContent()
    {
        var result = WebFileReadWire.Decode(Result("KELPIE_READ_1\nL21pc3Npbmc=\n0\n"), 1);
        using var doc = JsonDocument.Parse(result.StandardOutput);
        doc.RootElement.GetProperty("exists").GetBoolean().Should().BeFalse();
        doc.RootElement.TryGetProperty("contentBase64", out _).Should().BeFalse();
    }

    [Fact]
    public void Decode_WhenFileGrowsPastLimit_ShouldDiscardContent()
    {
        var result = WebFileReadWire.Decode(Result(Wire("YWJj")), 2);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().Contain("maximum read size");
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("KELPIE_READ_1\n/w==\n0\n")]
    [InlineData("KELPIE_READ_1\nL3dlYg==\n0\nextra")]
    public void Decode_WhenMalformed_ShouldDiscardPayload(string wire)
    {
        var result = WebFileReadWire.Decode(Result(wire), 64);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().Be("ERROR: invalid web read response");
    }

    [Theory]
    [InlineData("YQ")]
    [InlineData("YR==")]
    [InlineData("Y Q==")]
    public void Decode_WhenContentBase64IsNotCanonical_ShouldFail(string content)
    {
        WebFileReadWire.Decode(Result(Wire(content)), 64).ExitCode.Should().NotBe(0);
    }

    [Fact]
    public void Decode_WhenTimedOut_ShouldDiscardValidLookingOutput()
    {
        var result = WebFileReadWire.Decode(Result(Wire("YQ==")) with { TimedOut = true }, 64);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
    }

    private static string Wire(string content) => "KELPIE_READ_1\n" + Encode("/web/日本語\nfile") + "\n1\n" + Encode("0\0" + "1970-01-01 00:00:00.123456789 +0000\0") + "\n" + content + "\n";
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    private static SshCommandResult Result(string output) => new("synthetic", "fixed", 0, output, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
}
