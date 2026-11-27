using System.Text.Json;
using FluentAssertions;
using KelpieMCPServer;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class RuntimeDiagnosticAcceptanceTests
{
    [Theory, Trait("D3", "PND-10")]
    [InlineData("php", 1, "unsupported")]
    [InlineData("node", 9, "unsupported")]
    [InlineData("php", 9, "invalid-response")]
    [InlineData("node", 1, "invalid-response")]
    public async Task StartupRejection_ShouldBeSpecificToRuntime(string runtime, int exit, string expected)
    {
        var runner = new Runner { Exit = exit };
        var result = await new RuntimeDiagnosticService(new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner))
            .GetInfoAsync(Profile(), runtime);
        result.Status.Should().Be(expected); result.Features.Json.Should().BeNull();
        runner.Calls.Should().Be(1);
    }

    [Fact, Trait("D3", "PND-15")]
    public async Task InvalidUnicode_ShouldBeRejectedBeforeAuthentication()
    {
        var request = new SshCommandRequest(Profile(), "runtime_php_info", "/bin/false", TimeSpan.FromSeconds(1),
            new Dictionary<string, string>(), StandardInput: "\uD800")
        { ExecutionLimits = new SshCommandExecutionLimits(16384, 2048, 4096) };
        Func<Task> run = async () => { _ = await new KelpieSSH.Infrastructure.Ssh.SshNetCommandRunner().ExecuteAsync(request); };
        await run.Should().ThrowAsync<System.Text.EncoderFallbackException>();
    }

    [Fact, Trait("D3", "PND-12")]
    public async Task RevokedCatalog_ShouldNotReuseWarmAuthority()
    {
        var runner = new Runner(); var catalog = new MutableCatalog();
        var commands = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        (await KelpieTools.GetRuntimeInfoAsync(commands, catalog, "synthetic", "php")).Status.Should().Be("ok");
        catalog.Revoked = true;
        Func<Task> revoked = async () => { _ = await KelpieTools.GetRuntimeInfoAsync(commands, catalog, "synthetic", "php"); };
        await revoked.Should().ThrowAsync<InvalidOperationException>();
        runner.Calls.Should().Be(1);
    }

    [Fact, Trait("D3", "PND-13")]
    public async Task FreshCalls_ShouldObserveFailuresAndClockChangesWithoutCachingAbsence()
    {
        var runner = new Runner(); var clock = new Clock();
        var service = new RuntimeDiagnosticService(new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner), clock);
        var first = await service.GetInfoAsync(Profile(), "php");
        runner.Exit = 66; clock.Now = clock.Now.AddSeconds(61);
        var second = await service.GetInfoAsync(Profile(), "php", refresh: true);
        runner.Exit = 0; var third = await service.GetInfoAsync(Profile(), "php");
        first.Status.Should().Be("ok"); second.Status.Should().Be("unsupported"); third.Status.Should().Be("ok");
        runner.Calls.Should().Be(3); third.CheckedAt.Should().Be(clock.Now); third.Cached.Should().BeFalse();
        new[] { first.RequestId, second.RequestId, third.RequestId }.Distinct().Should().HaveCount(3);
    }

    [Fact, Trait("D3", "PND-20")]
    public async Task PostDispatchConnectionLoss_ShouldRemainUnknownWithoutRetry()
    {
        var runner = new Runner { LoseConnection = true };
        var service = new RuntimeDiagnosticService(new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner));
        var result = await service.GetInfoAsync(Profile(), "node");
        result.Status.Should().Be("connection-failed"); runner.Calls.Should().Be(1);
        result.Features.Should().Be(new RuntimeDiagnosticFeatures(null, null));
        JsonSerializer.Serialize(result).Should().NotContain("synthetic-sensitive");
    }

    [Theory, Trait("D3", "PND-16")]
    [InlineData("operation")][InlineData("startup")][InlineData("feature-extra")]
    [InlineData("feature-duplicate")][InlineData("protocol-type")][InlineData("version-null")]
    public async Task InvalidWireFields_ShouldNeverBecomeSuccessfulObservations(string fault)
    {
        var runner = new Runner { Fault = fault };
        var service = new RuntimeDiagnosticService(new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner));
        var result = await service.GetInfoAsync(Profile(), "php");
        result.Status.Should().Be("invalid-response"); result.Version.Should().BeNull();
        result.Features.Should().Be(new RuntimeDiagnosticFeatures(null, null));
    }

    private static SshConnectionProfile Profile() => new() { Name = "synthetic", Host = "example.invalid", UserName = "reader", PrivateKeyPath = "synthetic-key", HostKeyFingerprintSha256 = "synthetic-pin", OsFamily = "debian", PackageManager = "apt" };
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = new(2026, 10, 27, 0, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class MutableCatalog : ISshConnectionProfileCatalog
    {
        public bool Revoked;
        public bool TryGet(string name, out SshConnectionProfile profile) { profile = Profile(); return !Revoked; }
        public IReadOnlyCollection<SshConnectionProfile> List() => Revoked ? [] : [Profile()];
    }
    private sealed class Runner : ISshCommandRunner
    {
        public int Calls, Exit; public bool LoseConnection; public string Fault = "";
        public Task<SshCommandResult> ExecuteAsync(SshCommandRequest request, CancellationToken cancellationToken = default)
        {
            Calls++; if (LoseConnection) throw new IOException("synthetic-sensitive");
            using var input = JsonDocument.Parse(request.StandardInput!);
            var output = JsonSerializer.Serialize(new { protocol = 1, operation = "runtime.info", requestId = input.RootElement.GetProperty("requestId").GetString(), runtime = input.RootElement.GetProperty("runtime").GetString(), startupMode = "isolated", version = "8.2.35", features = new { json = true, sha256 = true } });
            output = Fault switch
            {
                "operation" => output.Replace("runtime.info", "runtime.other"),
                "startup" => output.Replace("isolated", "application"),
                "feature-extra" => output.Replace("\"json\":true", "\"extra\":true,\"json\":true"),
                "feature-duplicate" => output.Replace("\"json\":true", "\"json\":true,\"json\":true"),
                "protocol-type" => output.Replace("\"protocol\":1", "\"protocol\":\"1\""),
                "version-null" => output.Replace("\"8.2.35\"", "null"),
                _ => output,
            };
            return Task.FromResult(new SshCommandResult(request.CommandName, request.CommandText, Exit, output, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false));
        }
    }
}
