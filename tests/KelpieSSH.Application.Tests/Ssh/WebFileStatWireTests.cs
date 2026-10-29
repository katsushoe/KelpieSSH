using System.Text;
using System.Text.Json;
using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class WebFileStatWireTests
{
    [Fact]
    public void Decode_ShouldPreserveMetadataAndSpecialPath()
    {
        var payload = Encode("81a4\0" + "12\0" + "123\0" + "456\0" + "123\0" + "456\0" + "0\0" + "1970-01-01 00:00:00.123456789 +0000\0");
        var result = WebFileStatWire.Decode(Result("KELPIE_STAT_1\n" + Encode("/web/日本語\n") + "\n1\n" + payload + "\n"));
        result.ExitCode.Should().Be(0);
        using var json = JsonDocument.Parse(result.StandardOutput);
        json.RootElement.GetProperty("resolvedPath").GetString().Should().Be("/web/日本語\n");
        json.RootElement.GetProperty("owner").GetString().Should().Be("123");
        json.RootElement.GetProperty("group").GetString().Should().Be("456");
        json.RootElement.GetProperty("mode").GetString().Should().Be("644");
        json.RootElement.GetProperty("lastModified").GetString().Should().Be("1970-01-01T00:00:00.123457Z");
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("KELPIE_STAT_1\nL3dlYg==\n1\n/w==\n")]
    [InlineData("KELPIE_STAT_1\nL3dlYg==\n0\nextra")]
    public void Decode_ShouldRejectMalformedWireWithoutReturningPayload(string wire)
    {
        var result = WebFileStatWire.Decode(Result(wire));
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().Be("ERROR: invalid web stat response");
    }

    [Theory]
    [InlineData("a1ff", "symlink", "777", true)]
    [InlineData("41ed", "directory", "755", false)]
    [InlineData("11a4", "other", "644", false)]
    [InlineData("8fff", "file", "7777", false)]
    public void Decode_ShouldPreserveTypeAndSpecialPermissions(string mode, string type, string expectedMode, bool link)
    {
        var result = WebFileStatWire.Decode(Metadata(mode, "0", "UNKNOWN", "-1", "1969-12-31 23:59:59.000000000 +0000"));
        result.ExitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(result.StandardOutput);
        doc.RootElement.GetProperty("type").GetString().Should().Be(type);
        doc.RootElement.GetProperty("mode").GetString().Should().Be(expectedMode);
        doc.RootElement.GetProperty("isSymlink").GetBoolean().Should().Be(link);
        doc.RootElement.GetProperty("owner").GetString().Should().Be("UNKNOWN");
        doc.RootElement.GetProperty("lastModified").GetString().Should().Be("1969-12-31T23:59:59Z");
    }

    [Theory]
    [InlineData("10000", "0")]
    [InlineData("81a4", "-1")]
    [InlineData("81a4", "999999999999999999999999")]
    public void Decode_ShouldRejectOutOfRangeNumbers(string mode, string size)
    {
        var result = WebFileStatWire.Decode(Metadata(mode, size, "reader", "0", "1970-01-01 00:00:00.000000000 +0000"));
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
    }

    [Fact]
    public void Decode_ShouldPreserveMissingPathWithoutMetadata()
    {
        var result = WebFileStatWire.Decode(Result("KELPIE_STAT_1\n" + Encode("/missing") + "\n0\n"));
        using var doc = JsonDocument.Parse(result.StandardOutput);
        doc.RootElement.GetProperty("exists").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Decode_ShouldDiscardTimedOutOutput()
    {
        var result = WebFileStatWire.Decode(Result("untrusted") with { TimedOut = true });
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
    }

    private static SshCommandResult Metadata(string mode, string size, string owner, string seconds, string date) =>
        Result("KELPIE_STAT_1\n" + Encode("/file") + "\n1\n" + Encode(string.Join('\0', mode, size, "1000", "1000", owner, "group", seconds, date, "")) + "\n");

    private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    private static SshCommandResult Result(string output) => new("synthetic", "fixed", 0, output, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
}
