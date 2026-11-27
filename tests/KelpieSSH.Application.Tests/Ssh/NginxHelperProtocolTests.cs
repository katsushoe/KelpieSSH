using System.Text;
using System.Text.Json;
using FluentAssertions;
using KelpieWebPermissionHelper;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class NginxHelperProtocolTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void ParseRequest_ShouldPreserveContentAtTheExactPublicByteLimit()
    {
        var content = new string('a', 65536);
        var request = Parse(new { protocol = 1, requestId = Id, target = "/etc/nginx/example.conf", contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) });
        Encoding.UTF8.GetString(Convert.FromBase64String(request.ContentBase64!)).Should().Be(content);
    }

    [Theory]
    [InlineData("relative.conf")]
    [InlineData("/etc/nginx/../shadow")]
    [InlineData("/etc//nginx/a")]
    [InlineData("/etc/nginx/.")]
    [InlineData("/etc/nginx/a/")]
    [InlineData("/")]
    [InlineData("/etc/nginx/evil\\name")]
    public void ParseRequest_ShouldRejectNonCanonicalPaths(string path)
    {
        var act = () => Parse(new { protocol = 1, requestId = Id, target = path });
        act.Should().Throw<NginxContractException>();
    }

    [Theory]
    [InlineData("{\"protocol\":2}")]
    [InlineData("{\"protocol\":1,\"requestId\":\"bad\",\"target\":\"/etc/nginx/a\"}")]
    [InlineData("{\"protocol\":1,\"protocol\":1}")]
    [InlineData("{\"protocol\":1,\"requestId\":\"0123456789abcdef0123456789abcdef\",\"target\":\"/etc/nginx/a\",\"shell\":\"anything\"}")]
    [InlineData("{\"protocol\":1,\"requestId\":\"0123456789abcdef0123456789abcdef\",\"target\":null}")]
    [InlineData("{\"protocol\":1,\"requestId\":\"0123456789abcdef0123456789abcdef\",\"target\":\"/etc/nginx/a\",\"transactionId\":null}")]
    public void ParseRequest_ShouldRejectInvalidEnvelopes(string text)
    {
        Action act = () => NginxHelperCommand.ParseRequest(Encoding.UTF8.GetBytes(text));
        act.Should().Throw<Exception>().Where(ex => ex.GetType() == typeof(JsonException) || ex.GetType() == typeof(NginxContractException));
    }

    [Theory]
    [InlineData(65537, false)]
    [InlineData(1, true)]
    public void ParseRequest_ShouldRejectOversizeOrBinaryContent(int size, bool binary)
    {
        var content = binary ? new byte[size] : Enumerable.Repeat((byte)'a', size).ToArray();
        Action act = () => Parse(new { protocol = 1, requestId = Id, target = "/etc/nginx/a", contentBase64 = Convert.ToBase64String(content) });
        act.Should().Throw<NginxContractException>();
    }

    [Fact]
    public void ParseRequest_ShouldRejectInvalidUtf8Content()
    {
        Action act = () => Parse(new { protocol = 1, requestId = Id, target = "/etc/nginx/a", contentBase64 = "/w==" });
        act.Should().Throw<DecoderFallbackException>();
    }

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void ParseRequest_ShouldRejectInvalidExpectedHash(string hash)
    {
        Action act = () => Parse(new { protocol = 1, requestId = Id, target = "/etc/nginx/a", expectedHash = hash });
        act.Should().Throw<NginxContractException>();
    }

    [Fact]
    public void Dispatch_ShouldRejectArbitraryOperationsWithoutReadingInput()
    {
        using var input = new MemoryStream([0xff]);
        using var output = new StringWriter();
        NginxHelperCommand.Run(["nginx-exec", "1"], input, output).Should().Be(1);
        input.Position.Should().Be(0);
        using var result = JsonDocument.Parse(output.ToString());
        result.RootElement.GetProperty("error").GetString().Should().Be("unsupported-operation");
        output.ToString().Should().NotContain("System.");
    }

    [Fact]
    public void Dispatch_ShouldRejectAnOversizedRequestBeforeUsingFilesystem()
    {
        using var input = new MemoryStream(Enumerable.Repeat((byte)' ', 256 * 1024 + 1).ToArray());
        using var output = new StringWriter();
        NginxHelperCommand.Run(["nginx-plan", "1"], input, output).Should().Be(1);
        output.ToString().Should().Contain("size-exceeded");
    }

    [Fact]
    public void Capabilities_ShouldAdvertiseOnlyFixedOperationsWithoutRequiringSudo()
    {
        using var input = new MemoryStream();
        using var output = new StringWriter();
        NginxHelperCommand.Run(["nginx-capabilities"], input, output).Should().Be(0);
        using var result = JsonDocument.Parse(output.ToString());
        result.RootElement.GetProperty("verbs").GetArrayLength().Should().Be(6);
        output.ToString().Should().NotContain("exec");
    }

    private static NginxRequest Parse<T>(T request) => NginxHelperCommand.ParseRequest(JsonSerializer.SerializeToUtf8Bytes(request));
}
