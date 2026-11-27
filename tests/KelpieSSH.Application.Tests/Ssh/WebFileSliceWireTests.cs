using System.Text;
using System.Text.Json;
using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class WebFileSliceWireTests
{
    [Theory]
    [InlineData("", "head", 1, "")]
    [InlineData("a\nb\n", "head", 1, "a\n")]
    [InlineData("a\nb\n", "tail", 1, "b\n")]
    [InlineData("a\r\nb\r\n", "head", 1, "a\r\n")]
    [InlineData("a\r\nb\r\n", "tail", 1, "b\r\n")]
    [InlineData("a\rb", "head", 1, "a\r")]
    [InlineData("a\rb", "tail", 1, "b")]
    [InlineData("a\nb", "tail", 1, "b")]
    [InlineData("a\n", "tail", 1, "a\n")]
    [InlineData("\n\n", "tail", 1, "\n")]
    [InlineData("a\v\fb\u001c\u0085c", "head", 1, "a\v\fb\u001c\u0085c")]
    [InlineData("a\nb\n", "head", 0, "a\nb\n")]
    [InlineData("a\nb\n", "tail", 1000, "a\nb\n")]
    public void LimitLines_ShouldMatchByteSplitlines(string input, string mode, int limit, string expected)
    {
        WebFileSliceWire.LimitLines(Encoding.UTF8.GetBytes(input), mode, limit)
            .Should().Equal(Encoding.UTF8.GetBytes(expected));
    }

    [Theory]
    [InlineData("head", "AP8K")]
    [InlineData("tail", "gA0=")]
    public void Decode_ShouldPreserveRawBytesAndReturnSelectedSize(string mode, string expected)
    {
        var payload = Convert.ToBase64String(new byte[] { 0, 255, 10, 128, 13 });
        var result = WebFileSliceWire.Decode(Result(Wire(payload)), mode, 5, 1);
        result.ExitCode.Should().Be(0);
        using var doc = JsonDocument.Parse(result.StandardOutput);
        doc.RootElement.GetProperty("contentBase64").GetString().Should().Be(expected);
        doc.RootElement.GetProperty("size").GetInt32().Should().Be(Convert.FromBase64String(expected).Length);
    }

    [Theory]
    [InlineData("other", 1, 0)]
    [InlineData("head", 0, 0)]
    [InlineData("head", 1048577, 1)]
    [InlineData("tail", 1, 1001)]
    public void Decode_ShouldRejectInvalidBounds(string mode, int bytes, int lines)
    {
        var result = WebFileSliceWire.Decode(Result(Wire("YQ==")), mode, bytes, lines);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
    }

    [Fact]
    public void Decode_ShouldRejectOversizeWireBeforeLineSelection()
    {
        var result = WebFileSliceWire.Decode(Result(Wire("YQpi")), "head", 2, 1);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
    }

    [Fact]
    public void LimitLines_WhenManyEmptyLines_ShouldRetainOnlyTheTailLimit()
    {
        var bytes = Enumerable.Repeat((byte)10, 1048576).ToArray();
        WebFileSliceWire.LimitLines(bytes, "tail", 1000).Should().HaveCount(1000);
    }

    [Fact]
    public async Task ReadHeadAsync_WhenTransportLimitIsExceeded_ShouldReturnErrorWithoutRetry()
    {
        var runner = new LimitRunner();
        var profile = new SshConnectionProfile {
            Name = "synthetic", Host = "127.0.0.1", UserName = "reader", OsFamily = "debian", PackageManager = "apt", Mode = KelpiePolicyMode.ReadOnly,
            WebPublicSites = [new WebPublicSite { SiteKey = "test", DisplayName = "Synthetic", RootPath = "/site", AllowedFiles = [new WebPublicFileRule("**", AllowedRootAccess.Read)] }]
        };
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var result = await new WebPublicFileProvider().ReadHeadAsync(service, profile, "test", "/file.txt");
        result.Error.Should().Contain("output limit");
        result.ContentBase64.Should().BeNull();
        runner.Calls.Should().Be(1);
    }

    private sealed class LimitRunner : ISshCommandRunner
    {
        internal int Calls { get; private set; }
        public Task<SshCommandResult> ExecuteAsync(SshCommandRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromException<SshCommandResult>(new SshCommandOutputLimitException());
        }
    }

    [Fact]
    public void Decode_ShouldDiscardTimedOutOutput()
    {
        WebFileSliceWire.Decode(Result(Wire("YQ==")) with { TimedOut = true }, "head", 1, 1)
            .StandardOutput.Should().BeEmpty();
    }

    private static string Wire(string content) => "KELPIE_READ_1\nL2ZpbGU=\n1\n" + Convert.ToBase64String(Encoding.UTF8.GetBytes("0\0" + "1970-01-01 00:00:00.000000000 +0000\0")) + "\n" + content + "\n";
    private static SshCommandResult Result(string output) => new("synthetic", "fixed", 0, output, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
}
