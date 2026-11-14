using System.Text;
using FluentAssertions;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class BoundedSshExecutionTests
{
    private const string Nonce = "0123456789abcdef0123456789abcdef";
    private const string StartFrame = "\nKELPIE_BEGIN_" + Nonce + "\n";

    [Fact]
    public async Task FiniteInput_ShouldEndTheUpstreamListenerBeforeChannelDisposal()
    {
        var input = new BoundedSshExecution.FiniteShellInput([42]);
        var bytes = new byte[8];
        (await input.ReadAsync(bytes, 0, bytes.Length)).Should().Be(1);
        var end = input.ReadAsync(bytes, 0, bytes.Length);
        end.IsCompleted.Should().BeTrue();
        Func<Task> exhausted = async () => { _ = await end; };
        await exhausted.Should().ThrowAsync<BoundedSshExecution.FiniteShellInput.EndOfInputException>();
        input.Dispose();
        input.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Receiver_ShouldPreserveExactUtf8BoundaryWithoutGrowing()
    {
        var overflow = new TaskCompletionSource();
        using var receiver = new BoundedSshOutput(6, overflow);
        receiver.Write(Encoding.UTF8.GetBytes("日本"));
        receiver.Length.Should().Be(6);
        receiver.Decode().Should().Be("日本");
        overflow.Task.IsCompleted.Should().BeFalse();
        receiver.Write([1]);
        receiver.Length.Should().Be(6);
        overflow.Task.IsCompleted.Should().BeTrue();
        Action decode = () => receiver.Decode();
        decode.Should().Throw<SshCommandOutputLimitException>().WithMessage("*configured byte limit*");
    }

    [Fact]
    public void Receiver_ShouldDiscardOversizedPacketsAndSubsequentWrites()
    {
        var overflow = new TaskCompletionSource();
        using var receiver = new BoundedSshOutput(16, overflow);
        receiver.Write(new byte[1024 * 1024]);
        receiver.Write(new byte[1024 * 1024]);
        receiver.Length.Should().Be(0);
        receiver.Exceeded.Should().BeTrue();
    }

    [Fact]
    public void Receiver_ShouldRejectInvalidUtf8()
    {
        using var receiver = new BoundedSshOutput(4, new TaskCompletionSource());
        receiver.Write([0xff]);
        Action decode = () => receiver.Decode();
        decode.Should().Throw<DecoderFallbackException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(255)]
    public void CompletionFrame_ShouldPreservePayloadAndExactExitStatus(int status)
    {
        var result = BoundedSshExecution.ParseOutput(StartFrame + "payload\n\nKELPIE_EXIT_" + Nonce + ":" + status + "\n", Nonce, 8);
        result.Output.Should().Be("payload\n");
        result.ExitCode.Should().Be(status);
    }

    [Theory]
    [InlineData("", "0123456789abcdef0123456789abcdef")]
    [InlineData("\nKELPIE_EXIT_0123456789abcdef0123456789abcdef:0\ntrailing", "0123456789abcdef0123456789abcdef")]
    [InlineData("\nKELPIE_EXIT_0123456789abcdef0123456789abcdef:256\n", "0123456789abcdef0123456789abcdef")]
    [InlineData("\nKELPIE_EXIT_0123456789abcdef0123456789abcdef:-1\n", "0123456789abcdef0123456789abcdef")]
    [InlineData("\nKELPIE_EXIT_0123456789abcdef0123456789abcdef:0\n", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void CompletionFrame_ShouldRejectMissingWrongOrMalformedStatus(string output, string nonce)
    {
        Action parse = () => BoundedSshExecution.ParseOutput(StartFrame + output, nonce, 100);
        parse.Should().Throw<IOException>();
    }

    [Fact]
    public void CompletionFrame_ShouldCheckPayloadBytesInsteadOfCharacters()
    {
        Action parse = () => BoundedSshExecution.ParseOutput(StartFrame + "日\nKELPIE_EXIT_" + Nonce + ":0\n", Nonce, 2);
        parse.Should().Throw<SshCommandOutputLimitException>();
    }

    [Fact]
    public void Frames_ShouldExcludeSessionStartupOutputAndPreserveWirePayload()
    {
        var payload = WebWriteCheckWireTests.Envelope("/site/file.php", true, false, "FILE");
        var framed = "synthetic login notice\nsecond line\n" + StartFrame + payload + "\nKELPIE_EXIT_" + Nonce + ":0\n";
        var result = BoundedSshExecution.ParseOutput(framed, Nonce, Encoding.UTF8.GetByteCount(payload));
        result.Output.Should().Be(payload);
        var decoded = WebWriteCheckWire.Decode(new SshCommandResult("check", "", result.ExitCode,
            result.Output, "", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, false));
        decoded.ExitCode.Should().Be(0);
        decoded.StandardOutput.Should().Contain("Target file is not writable");
    }

    [Theory]
    [InlineData("payload\nKELPIE_EXIT_0123456789abcdef0123456789abcdef:0\n")]
    [InlineData("\nKELPIE_BEGIN_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\npayload\nKELPIE_EXIT_0123456789abcdef0123456789abcdef:0\n")]
    [InlineData("\nKELPIE_EXIT_0123456789abcdef0123456789abcdef:0\n\nKELPIE_BEGIN_0123456789abcdef0123456789abcdef\n")]
    public void Frames_ShouldRejectMissingWrongOrReversedStart(string output)
    {
        Action parse = () => BoundedSshExecution.ParseOutput(output, Nonce, 100);
        parse.Should().Throw<IOException>();
    }

    [Fact]
    public void Input_ShouldEncodeDataInsteadOfInterpretingItAsShellSource()
    {
        var input = "$(touch /synthetic-marker)\n' ; exit 9";
        var script = BoundedSshExecution.BuildInput("/bin/cat", Encoding.UTF8.GetBytes(input), Nonce);
        script.Should().NotContain(input).And.Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes(input)));
        script.Should().Contain("/usr/bin/base64 -d");
        script.Should().StartWith("printf '\\nKELPIE_BEGIN_" + Nonce + "\\n'");
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(2097153, 1, 1)]
    [InlineData(1, 65537, 1)]
    [InlineData(1, 1, -1)]
    public void Limits_ShouldRejectInvalidRanges(int output, int error, int input)
    {
        Action validate = () => new SshCommandExecutionLimits(output, error, input).Validate();
        validate.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(false, "Bounded SSH input exceeded its byte limit.")]
    [InlineData(true, "Bounded SSH requires text input.")]
    public async Task Runner_ShouldRejectUnsupportedInputBeforeAuthentication(bool binary, string message)
    {
        var profile = new SshConnectionProfile
        {
            Name = "synthetic", Host = "example.invalid", UserName = "probe", OsFamily = "debian",
            PackageManager = "apt", PrivateKeyPath = "intentionally-not-an-existing-key",
        };
        using var stream = new MemoryStream([1]);
        var request = new SshCommandRequest(profile, "synthetic", "/bin/cat", TimeSpan.FromSeconds(1),
            new Dictionary<string, string>(), StandardInput: binary ? null : "日", BinaryStandardInput: binary ? stream : null)
        { ExecutionLimits = new SshCommandExecutionLimits(8, 8, 2) };
        Func<Task> run = async () => { _ = await new SshNetCommandRunner().ExecuteAsync(request); };
        await run.Should().ThrowAsync<ArgumentException>().WithMessage(message);
    }
}
