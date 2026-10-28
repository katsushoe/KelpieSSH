using System.Text;
using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class NginxConfigReadWireTests
{
    [Theory]
    [InlineData("")]
    [InlineData("server { listen 80; }\n")]
    [InlineData("日本語の設定\n")]
    public void Decode_ShouldReturnExactUtf8Text(string content)
    {
        var result = NginxConfigReadWire.Decode(Response(Encoding.UTF8.GetBytes(content)), 65536);
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Be(content);
        result.StandardError.Should().BeEmpty();
    }

    [Fact]
    public void Decode_ShouldMarkTruncationWithoutReturningExtraByte()
    {
        var result = NginxConfigReadWire.Decode(Response(Encoding.UTF8.GetBytes("abcdef")), 5);
        result.StandardOutput.Should().Be("abcde");
        result.StandardError.Should().Contain("KELPIE_TRUNCATED=1");
    }

    [Theory]
    [InlineData("AA==", 4)]
    [InlineData("/w==", 4)]
    [InlineData("5pel", 1)]
    [InlineData("Zm9vYmFy", 2)]
    [InlineData("not-base64!", 10)]
    [InlineData("Y Q==", 10)]
    public void Decode_ShouldRejectBinaryInvalidUtf8OversizeOrMalformedWire(string payload, int limit)
    {
        var result = NginxConfigReadWire.Decode(Result(payload), limit);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
    }

    [Fact]
    public void Decode_ShouldPreserveFailureWithoutParsingRemoteOutput()
    {
        var failure = Result("untrusted") with { ExitCode = 2, StandardError = "ERROR: unavailable" };
        NginxConfigReadWire.Decode(failure, 65536).Should().Be(failure);
    }

    [Fact]
    public void Decode_ShouldDiscardOutputAfterTimeout()
    {
        var result = NginxConfigReadWire.Decode(Result("untrusted") with { TimedOut = true }, 65536);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
    }

    [Fact]
    public void Decode_ShouldAcceptLargestPreviouslyValidInternalByteLimit()
    {
        var bytes = Enumerable.Repeat((byte)'a', 1000000).ToArray();
        var result = NginxConfigReadWire.Decode(Response(bytes), 999999);
        result.ExitCode.Should().Be(0);
        result.StandardOutput.Length.Should().Be(999999);
        result.StandardError.Should().Contain("KELPIE_TRUNCATED=1");
    }

    [Fact]
    public void Definition_ShouldUseBoundedIsolatedTransport()
    {
        var profile = new SshConnectionProfile { Name = "synthetic", Host = "example.invalid", UserName = "reader", OsFamily = "debian", PackageManager = "apt" };
        var definition = new NginxServiceConfigCommandProvider().GetCommands(profile)
            .Single(c => c.Name == "service_config_nginx_read_config");
        definition.CommandTemplate.Should().NotContain("python");
        definition.IgnoreEnvironmentOverrides.Should().BeTrue();
        definition.ExecutionLimits!.StandardOutputBytes.Should().BeGreaterThan(1333336);
        definition.ExecutionLimits.StandardErrorBytes.Should().Be(2048);
        definition.ExecutionLimits.Validate();
    }

    [Fact]
    public async Task Execute_ShouldRejectMalformedPathParameterBeforeDispatch()
    {
        var runner = new FailureRunner(new IOException("not reached"));
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var arguments = new Dictionary<string, string>(Arguments()) { ["pathBase64"] = "not base64!" };
        Func<Task> call = async () => { _ = await NginxConfigReadWire.ExecuteAsync(service, Profile("reader"), "service_config_nginx_read_config", arguments, KelpieExecutionChannel.Mcp, default); };
        await call.Should().ThrowAsync<Exception>();
        runner.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Execute_ShouldRejectRootBeforeDispatch()
    {
        var runner = new FailureRunner(new IOException("not reached"));
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        Func<Task> call = async () => { _ = await NginxConfigReadWire.ExecuteAsync(service, Profile("root"), "service_config_nginx_read_config", Arguments(), KelpieExecutionChannel.Mcp, default); };
        await call.Should().ThrowAsync<InvalidOperationException>();
        runner.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(KelpiePolicyMode.ReadOnly)]
    [InlineData(KelpiePolicyMode.Safe)]
    [InlineData(KelpiePolicyMode.Maintenance)]
    [InlineData(KelpiePolicyMode.Expert)]
    public async Task Execute_ShouldPermitReadInEachMode(KelpiePolicyMode mode)
    {
        var runner = new ResultRunner();
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var result = await NginxConfigReadWire.ExecuteAsync(service, Profile("reader", mode), "service_config_nginx_read_config", Arguments(), KelpieExecutionChannel.Mcp, default);
        result.ExitCode.Should().Be(0);
        runner.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_ShouldNotRetryCancellationOrConnectionLoss(bool cancelled)
    {
        var runner = new FailureRunner(cancelled ? new OperationCanceledException() : new IOException("synthetic connection loss"));
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        Func<Task> call = async () => { _ = await NginxConfigReadWire.ExecuteAsync(service, Profile("reader"), "service_config_nginx_read_config", Arguments(), KelpieExecutionChannel.Mcp, default); };
        if (cancelled) await call.Should().ThrowAsync<OperationCanceledException>();
        else await call.Should().ThrowAsync<IOException>();
        runner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Execute_ShouldNotRetryAfterTimeout()
    {
        var runner = new ResultRunner(timedOut: true);
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var result = await NginxConfigReadWire.ExecuteAsync(service, Profile("reader"), "service_config_nginx_read_config", Arguments(), KelpieExecutionChannel.Mcp, default);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
        runner.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_ShouldSanitizeTransportLimitAndEncodingFailures(bool invalidEncoding)
    {
        var runner = new FailureRunner(invalidEncoding ? new DecoderFallbackException() : new SshCommandOutputLimitException());
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var result = await NginxConfigReadWire.ExecuteAsync(service, Profile("reader"), "service_config_nginx_read_config", Arguments(), KelpieExecutionChannel.Mcp, default);
        result.ExitCode.Should().NotBe(0);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().StartWith("ERROR:");
        runner.Calls.Should().Be(1);
    }

    private static SshConnectionProfile Profile(string user, KelpiePolicyMode mode = KelpiePolicyMode.ReadOnly) => new()
    { Name = "synthetic", Host = "example.invalid", UserName = user, OsFamily = "debian", PackageManager = "apt", Mode = mode };

    private static IReadOnlyDictionary<string, string> Arguments() => new Dictionary<string, string>
    { ["pathBase64"] = "L3N5bnRoZXRpYw==", ["allowedPathsBase64"] = "L3N5bnRoZXRpYw==", ["allowedDirsBase64"] = "Cg==", ["maxBytes"] = "65536" };

    private sealed class FailureRunner(Exception failure) : ISshCommandRunner
    {
        public int Calls { get; private set; }
        public Task<SshCommandResult> ExecuteAsync(SshCommandRequest request, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromException<SshCommandResult>(failure); }
    }

    private sealed class ResultRunner(bool timedOut = false) : ISshCommandRunner
    {
        public int Calls { get; private set; }
        public Task<SshCommandResult> ExecuteAsync(SshCommandRequest request, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(Result("") with { TimedOut = timedOut }); }
    }

    private static SshCommandResult Response(byte[] bytes) => Result(Convert.ToBase64String(bytes));
    private static SshCommandResult Result(string output) => new("synthetic", "fixed", 0, output, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false);
}
