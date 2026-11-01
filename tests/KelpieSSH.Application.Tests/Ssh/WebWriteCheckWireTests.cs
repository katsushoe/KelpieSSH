using System.Text;
using System.Text.Json;
using FluentAssertions;
using KelpieSSH.Application.Ssh;
namespace KelpieSSH.Application.Tests.Ssh;
public sealed class WebWriteCheckWireTests
{
    internal static string Envelope(string path, bool exists, bool writable, string reason = "NONE") =>
        "KELPIE_WRITE_CHECK_1\n" + Convert.ToBase64String(Encoding.UTF8.GetBytes(path)) + "\n" + (exists ? "1" : "0") + "\n" + (writable ? "1" : "0") + "\n" + reason + "\n";
    internal static string FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r=doc.RootElement;
        return Envelope(r.GetProperty("resolvedPath").GetString()!,r.GetProperty("exists").GetBoolean(),r.GetProperty("canWrite").GetBoolean(),r.GetProperty("canWrite").GetBoolean()?"NONE":"FILE");
    }
    private static SshCommandResult Result(string output, int status=0) => new("check","",status,output,"",DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false);
    [Theory]
    [InlineData(true,true,"NONE",null)]
    [InlineData(false,true,"NONE",null)]
    [InlineData(true,false,"TYPE","Target path is not a regular file.")]
    [InlineData(true,false,"FILE","Target file is not writable by the SSH user.")]
    [InlineData(false,false,"PARENT","Parent directory is not writable by the SSH user.")]
    [InlineData(false,false,"MISSING","Parent directory does not exist and createDirectories is disabled.")]
    public void Decode_MapsAdvisoryState(bool exists,bool writable,string code,string? reason)
    {
        var result=WebWriteCheckWire.Decode(Result(Envelope("/site/日本語\n\"file",exists,writable,code)));
        result.ExitCode.Should().Be(0);
        using var json=JsonDocument.Parse(result.StandardOutput);
        json.RootElement.GetProperty("resolvedPath").GetString().Should().Be("/site/日本語\n\"file");
        json.RootElement.GetProperty("exists").GetBoolean().Should().Be(exists);
        json.RootElement.GetProperty("canWrite").GetBoolean().Should().Be(writable);
        json.RootElement.GetProperty("reason").GetString().Should().Be(reason);
    }
    [Theory]
    [InlineData("garbage")]
    [InlineData("KELPIE_WRITE_CHECK_1\n!\n0\n1\nNONE\n")]
    [InlineData("KELPIE_WRITE_CHECK_1\nL3g=\n2\n1\nNONE\n")]
    [InlineData("KELPIE_WRITE_CHECK_1\nL3g=\n0\n2\nNONE\n")]
    [InlineData("KELPIE_WRITE_CHECK_1\nL3g=\n0\n1\nFILE\n")]
    [InlineData("KELPIE_WRITE_CHECK_1\nL3g=\n0\n0\nFILE\n")]
    [InlineData("KELPIE_WRITE_CHECK_1\nL3g=\n1\n0\nPARENT\n")]
    [InlineData("KELPIE_WRITE_CHECK_1\nL3g=\n0\n0\nUNKNOWN\n")]
    public void Decode_RejectsMalformedOrInconsistentResponse(string value)
    {
        var result=WebWriteCheckWire.Decode(Result(value));
        result.ExitCode.Should().NotBe(0);result.StandardOutput.Should().BeEmpty();
    }
    [Theory]
    [InlineData("relative")]
    [InlineData("/nul\0path")]
    [InlineData("")]
    public void Decode_RejectsInvalidPath(string path) => WebWriteCheckWire.Decode(Result(Envelope(path,false,true))).ExitCode.Should().NotBe(0);
    [Fact]
    public void Decode_RejectsOversizedPathAndEnvelope()
    {
        WebWriteCheckWire.Decode(Result(Envelope("/"+new string('a',12288),false,true))).ExitCode.Should().NotBe(0);
        WebWriteCheckWire.Decode(Result(new string('a',32769))).ExitCode.Should().NotBe(0);
    }
    [Fact]
    public void Decode_PreservesExecutionFailure() => WebWriteCheckWire.Decode(Result("",2)).ExitCode.Should().Be(2);
}
