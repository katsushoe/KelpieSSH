using System.Text.Json;
using FluentAssertions;
using KelpieMCPServer;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class RuntimeDiagnosticServiceTests
{
    public static IEnumerable<object[]> Modes() => Enum.GetValues<KelpiePolicyMode>().SelectMany(mode =>
        Enum.GetValues<KelpieExecutionChannel>().SelectMany(channel => new[] { "php", "node" }.Select(runtime => new object[] { mode, channel, runtime })));

    [Theory, MemberData(nameof(Modes))]
    public async Task Info_ShouldUseCurrentPolicyBoundedLauncherAndMeasuredFeatures(KelpiePolicyMode mode, KelpieExecutionChannel channel, string runtime)
    {
        var runner = new Runner();
        var service = Service(runner);
        var first = await service.GetInfoAsync(Profile(mode), runtime, channel: channel);
        runner.Sha256 = false;
        var second = await service.GetInfoAsync(Profile(mode), runtime, refresh: true, channel: channel);
        first.Status.Should().Be("ok"); first.Features.Sha256.Should().BeTrue();
        second.Features.Sha256.Should().BeFalse(); second.Cached.Should().BeFalse();
        second.RequestId.Should().NotBe(first.RequestId); runner.Calls.Should().Be(2);
        first.CheckedAt.Offset.Should().Be(TimeSpan.Zero);
        runner.Last!.ExecutionLimits.Should().Be(new SshCommandExecutionLimits(16384, 2048));
        runner.Last.IgnoreEnvironmentOverrides.Should().BeTrue(); runner.Last.StandardInput!.Length.Should().BeLessThan(4096);
    }

    [Theory]
    [InlineData("unknown")][InlineData("PHP")][InlineData("php;touch marker")]
    public async Task InvalidSelector_ShouldNeverReachRunner(string runtime)
    {
        var runner = new Runner();
        (await Service(runner).GetInfoAsync(Profile(), runtime)).Status.Should().Be("invalid-input");
        runner.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("root", "synthetic-pin")][InlineData("reader", null)]
    public async Task UntrustedProfile_ShouldNotProbe(string user, string? pin)
    {
        var runner = new Runner();
        (await Service(runner).GetInfoAsync(Profile(user: user, pin: pin), "php")).Status.Should().Be("denied");
        runner.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ForbiddenCatalog_ShouldDenyBeforeRunner()
    {
        var runner = new Runner();
        var command = RuntimeDiagnosticStartup.CreateInfoDefinition("php") with { RiskLevel = SshCommandRiskLevel.Forbidden };
        var service = new RuntimeDiagnosticService(new SshCommandService(new AllowedCommandCatalog([command]), runner));
        (await service.GetInfoAsync(Profile(), "php", channel: KelpieExecutionChannel.Mcp)).Status.Should().Be("denied");
        runner.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(127, "runtime-startup-unavailable", "unavailable")]
    [InlineData(127, "sensitive-path", "unsupported")]
    [InlineData(65, "runtime-startup-untrusted", "denied")]
    [InlineData(65, "runtime-startup-changed", "denied")]
    [InlineData(66, "", "unsupported")]
    [InlineData(137, "", "timeout")]
    [InlineData(2, "sensitive-value", "invalid-response")]
    [InlineData(0, "warning-secret", "invalid-response")]
    public async Task Failure_ShouldClassifyWithoutReturningRemoteDetails(int code, string error, string expected)
    {
        var result = await Service(new Runner { Code = code, Error = error }).GetInfoAsync(Profile(), "php");
        result.Status.Should().Be(expected); result.Version.Should().BeNull();
        result.Features.Should().Be(new RuntimeDiagnosticFeatures(null, null)); result.StartupMode.Should().BeNull();
        JsonSerializer.Serialize(result).Should().NotContain("sensitive").And.NotContain("secret");
    }

    [Theory]
    [InlineData("duplicate")][InlineData("unknown")][InlineData("runtime")][InlineData("id")]
    [InlineData("protocol")][InlineData("version")][InlineData("feature")][InlineData("null")]
    [InlineData("deep")][InlineData("large")][InlineData("trailing")][InlineData("remote-time")]
    public async Task MalformedResponse_ShouldFailClosed(string fault)
    {
        var result = await Service(new Runner { Fault = fault }).GetInfoAsync(Profile(), "php");
        result.Status.Should().Be("invalid-response"); result.Features.Json.Should().BeNull();
    }

    [Fact]
    public async Task Cancellation_ShouldReturnCancelledWithoutDispatch()
    {
        var runner = new Runner(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        (await Service(runner).GetInfoAsync(Profile(), "php", cancellationToken: cancel.Token)).Status.Should().Be("cancelled");
        runner.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Mcp_ShouldResolveCurrentProfileAndReturnSameTypedContract()
    {
        var runner = new Runner();
        var catalog = new SshConnectionProfileCatalog([Profile()]);
        var result = await KelpieTools.GetRuntimeInfoAsync(Commands(runner), catalog, "synthetic", "node");
        result.Runtime.Should().Be("node"); result.Version.Should().Be("22.1.2");
        result.Status.Should().Be("ok");
    }

    private static RuntimeDiagnosticService Service(Runner runner) => new(Commands(runner));
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task Capability_ShouldReflectCurrentPinWithoutClaimingInstallation(bool pinned)
    {
        var runner = new Runner();
        var profile = Profile(pin: pinned ? "synthetic-pin" : null);
        var result = await KelpieTools.GetSshCapabilitiesAsync(Commands(runner), new SshConnectionProfileCatalog([profile]), profile.Name);
        result.Tools.Single(tool => tool.ToolName == "ssh_runtime_info").Available.Should().Be(pinned);
        runner.Last!.CommandName.Should().Be("get_os_release");
    }

    private static SshCommandService Commands(Runner runner) => new(CommandProcessingProviderCatalog.CreateDefault(), runner);
    private static SshConnectionProfile Profile(KelpiePolicyMode mode = KelpiePolicyMode.ReadOnly, string user = "reader", string? pin = "synthetic-pin") => new()
    { Name = "synthetic", Host = "example.invalid", UserName = user, HostKeyFingerprintSha256 = pin,
      PrivateKeyPath = "synthetic-key", OsFamily = "debian", PackageManager = "apt", Mode = mode };

    private sealed class Runner : ISshCommandRunner
    {
        public int Calls; public bool Sha256 = true; public int Code; public string Error = ""; public string Fault = "";
        public SshCommandRequest? Last;
        public Task<SshCommandResult> ExecuteAsync(SshCommandRequest request, CancellationToken cancellationToken = default)
        {
            Calls++; Last = request;
            if (request.CommandName == "get_os_release") return Task.FromResult(new SshCommandResult(request.CommandName, request.CommandText, 0, "ID=debian", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false));
            using var input = JsonDocument.Parse(request.StandardInput!);
            var id = input.RootElement.GetProperty("requestId").GetString();
            var runtime = input.RootElement.GetProperty("runtime").GetString();
            var output = JsonSerializer.Serialize(new { protocol = 1, operation = "runtime.info", requestId = id, runtime,
                startupMode = "isolated", version = runtime == "php" ? "8.2.3" : "22.1.2", features = new { json = true, sha256 = Sha256 } });
            output = Fault switch
            {
                "duplicate" => output.Insert(1, "\"protocol\":1,"),
                "unknown" => output.Insert(1, "\"extra\":1,"),
                "runtime" => output.Replace("\"php\"", "\"node\""),
                "id" => output.Replace(id!, new string('0', 32)),
                "protocol" => output.Replace("\"protocol\":1", "\"protocol\":2"),
                "version" => output.Replace("8.2.3", "8.2.3-secret"),
                "feature" => output.Replace("\"json\":true", "\"json\":false"),
                "null" => output.Replace("\"sha256\":true", "\"sha256\":null"),
                "deep" => new string('[', 12) + "0" + new string(']', 12),
                "large" => new string(' ', 17000) + output,
                "trailing" => output + "garbage",
                "remote-time" => output.Insert(1, "\"checkedAt\":\"2020-01-01\","),
                _ => output,
            };
            return Task.FromResult(new SshCommandResult(request.CommandName, request.CommandText, Code, output, Error, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false));
        }
    }
}
