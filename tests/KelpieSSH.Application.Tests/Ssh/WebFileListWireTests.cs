using System.Text;
using System.Text.Json;
using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class WebFileListWireTests
{
    [Theory]
    [InlineData("0\n", false)]
    [InlineData("1\n\nEND:0\n", true)]
    public void Decode_ShouldDistinguishMissingAndEmpty(string suffix, bool exists)
    {
        var result = WebFileListWire.Decode(Result(Prefix + suffix), 0, 1);
        result.ExitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(result.StandardOutput);
        doc.RootElement.GetProperty("exists").GetBoolean().Should().Be(exists);
        doc.RootElement.GetProperty("entries").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("81ed", "file", "755", false)]
    [InlineData("41ff", "directory", "777", false)]
    [InlineData("a1ff", "symlink", "777", true)]
    [InlineData("11a4", "other", "644", false)]
    [InlineData("8fff", "file", "7777", false)]
    public void Decode_ShouldPreserveMetadata(string mode, string type, string permissions, bool link)
    {
        var result = WebFileListWire.Decode(Result(Prefix + "1\n" + Entry(mode) + "\nEND:1\n"), 1, 1);
        result.ExitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(result.StandardOutput);
        var entry = doc.RootElement.GetProperty("entries")[0];
        entry.GetProperty("name").GetString().Should().Be("line\n雪.txt");
        entry.GetProperty("type").GetString().Should().Be(type);
        entry.GetProperty("mode").GetString().Should().Be(permissions);
        entry.GetProperty("isSymlink").GetBoolean().Should().Be(link);
        entry.GetProperty("owner").GetString().Should().Be("4294967294");
        entry.GetProperty("lastModified").GetString().Should().Be("1970-01-01T00:00:00.123457Z");
        entry.GetProperty("depth").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("1\nEND:1\n")]
    [InlineData("1\nEND:2\n")]
    [InlineData("0\nEND:0\n")]
    [InlineData("1\nAAAA\nEND:0\n")]
    [InlineData("1\n/w==\nEND:0\n")]
    [InlineData("1\nEND:0\nextra\n")]
    public void Decode_ShouldRejectMalformedFraming(string suffix)
    {
        var result = WebFileListWire.Decode(Result(Prefix + suffix), 1, 1);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
    }

    [Fact]
    public void Decode_ShouldRejectTooManyEntries()
    {
        var payload = Convert.ToBase64String(Convert.FromBase64String(Entry("81a4")).Concat(Convert.FromBase64String(Entry("81a4"))).ToArray());
        WebFileListWire.Decode(Result(Prefix + "1\n" + payload + "\nEND:0\n"), 1, 1)
            .ExitCode.Should().NotBe(0);
    }

    [Fact]
    public void Decode_ShouldRejectEntryBeyondDepth()
    {
        WebFileListWire.Decode(Result(Prefix + "1\n" + Entry("81a4") + "\nEND:0\n"), 0, 1)
            .ExitCode.Should().NotBe(0);
    }

    [Fact]
    public void Decode_ShouldDiscardPartialTimedOutOutput()
    {
        var result = WebFileListWire.Decode(Result(Prefix) with { TimedOut = true }, 1, 1);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
    }

    private const string Prefix = "KELPIE_LIST_1\nL29wdC9jYXNlcw==\n";
    private static string Entry(string mode) => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        string.Join('\0', new[] { "line\n雪.txt", "/line\n雪.txt", "/opt/cases/line\n雪.txt", "1", mode, "128", "4294967294", "1", "4294967294", "group", "0", "1970-01-01 00:00:00.123456789 +0000", "" })));
    private static SshCommandResult Result(string stdout) => new("list", "", 0, stdout, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
}
