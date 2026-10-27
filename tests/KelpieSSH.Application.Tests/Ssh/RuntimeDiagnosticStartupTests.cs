using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class RuntimeDiagnosticStartupTests
{
    [Theory]
    [InlineData("php")]
    [InlineData("node")]
    public void Definition_ShouldOptIntoBoundedIsolatedExecutionWithoutArguments(string runtime)
    {
        var definition = RuntimeDiagnosticStartup.CreateProbeDefinition(runtime);
        definition.Name.Should().Be("runtime_" + runtime + "_startup_probe");
        definition.IgnoreEnvironmentOverrides.Should().BeTrue();
        definition.ExecutionLimits.Should().Be(new SshCommandExecutionLimits(16384, 2048));
        definition.RiskLevel.Should().Be(SshCommandRiskLevel.ReadOnly);
        definition.BuildCommandText().Should().StartWith("/usr/bin/env -i PATH=/usr/bin:/bin HOME=/nonexistent LC_ALL=C /usr/bin/timeout -s KILL 5");
        Action extra = () => definition.BuildCommandText(new Dictionary<string, string> { ["code"] = "arbitrary" });
        extra.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("PHP")]
    [InlineData("node; echo bad")]
    [InlineData("/tmp/node")]
    [InlineData("")]
    public void Definition_ShouldRejectArbitraryRuntimeSelectors(string runtime)
    {
        Action create = () => RuntimeDiagnosticStartup.CreateProbeDefinition(runtime);
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Catalog_ShouldNotExposeStartupProbesBeforeAdapterStage()
    {
        AllowedCommandCatalog.CreateDefault().List().Should().NotContain(x => x.Name.StartsWith("runtime_", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(KelpiePolicyMode.ReadOnly, KelpieExecutionChannel.Mcp)]
    [InlineData(KelpiePolicyMode.ReadOnly, KelpieExecutionChannel.Cli)]
    [InlineData(KelpiePolicyMode.Safe, KelpieExecutionChannel.Cli)]
    [InlineData(KelpiePolicyMode.Safe, KelpieExecutionChannel.Mcp)]
    [InlineData(KelpiePolicyMode.Maintenance, KelpieExecutionChannel.Mcp)]
    [InlineData(KelpiePolicyMode.Maintenance, KelpieExecutionChannel.Cli)]
    [InlineData(KelpiePolicyMode.Expert, KelpieExecutionChannel.Cli)]
    [InlineData(KelpiePolicyMode.Expert, KelpieExecutionChannel.Mcp)]
    public async Task Service_ShouldCarryDefinitionLimitsThroughNormalPolicyPipeline(KelpiePolicyMode mode, KelpieExecutionChannel channel)
    {
        var definition = RuntimeDiagnosticStartup.CreateProbeDefinition("php");
        var runner = new RecordingRunner();
        var service = new SshCommandService(new AllowedCommandCatalog([definition]), KelpiePolicyEvaluator.Default, runner);
        var profile = new SshConnectionProfile { Name = "synthetic", Host = "example.invalid", UserName = "probe", PrivateKeyPath = "synthetic-key", OsFamily = "debian", Mode = mode };
        await service.ExecuteAsync(profile, definition.Name, arguments: null, channel: channel);
        runner.Request!.ExecutionLimits.Should().Be(definition.ExecutionLimits);
        runner.Request.IgnoreEnvironmentOverrides.Should().BeTrue();
    }

    private sealed class RecordingRunner : ISshCommandRunner
    {
        public SshCommandRequest? Request { get; private set; }
        public Task<SshCommandResult> ExecuteAsync(SshCommandRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new SshCommandResult(request.CommandName, request.CommandText, 0, "", "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, false));
        }
    }
}
