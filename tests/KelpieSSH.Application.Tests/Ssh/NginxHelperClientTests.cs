using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class NginxHelperClientTests
{
    [Fact]
    public async Task Apply_ShouldReconcileAnAmbiguousReplyWithoutRepeatingMutation()
    {
        var runner = new Runner((request, operation) => operation == "apply"
            ? Result(request, "not-json", timedOut: true)
            : Reply(request, "applied", changed: true));
        var (service, profile, request) = Context(runner);
        var reply = await NginxHelperClient.CallAsync(service, profile, "apply", request, default);
        reply.Ok.Should().BeFalse();
        reply.State.Should().Be("applied");
        reply.ConfigChanged.Should().BeTrue();
        reply.Error.Should().Contain("no mutation was retried");
        runner.Operations.Should().Equal("apply", "status");
    }

    [Fact]
    public async Task Apply_ShouldRemainUnknownIfReconciliationAlsoFails()
    {
        var runner = new Runner((request, _) => Result(request, "PRIVATE_CONFIG_BODY", timedOut: true));
        var (service, profile, request) = Context(runner);
        var reply = await NginxHelperClient.CallAsync(service, profile, "apply", request, default);
        reply.State.Should().Be("unknown");
        reply.Error.Should().NotContain("PRIVATE_CONFIG_BODY");
        runner.Operations.Should().Equal("apply", "status");
    }

    [Fact]
    public async Task Commit_ShouldAcceptVerifiedCompletionAfterLostReply()
    {
        var runner = new Runner((request, operation) => operation == "commit"
            ? Result(request, "", timedOut: true) : Reply(request, "committed", changed: true));
        var (service, profile, request) = Context(runner);
        var reply = await NginxHelperClient.CallAsync(service, profile, "commit", request, default);
        reply.Ok.Should().BeTrue();
        reply.State.Should().Be("committed");
        runner.Operations.Should().Equal("commit", "status");
    }

    [Fact]
    public async Task Rollback_ShouldNeverReportSuccessWhenStatusIsUnknown()
    {
        var runner = new Runner((request, _) => Result(request, "{", timedOut: false));
        var (service, profile, request) = Context(runner);
        var reply = await NginxHelperClient.CallAsync(service, profile, "rollback", request, default);
        reply.Ok.Should().BeFalse();
        reply.State.Should().Be("unknown");
        runner.Operations.Should().Equal("rollback", "status");
    }

    [Theory]
    [InlineData("protocol", "2")]
    [InlineData("requestId", "\"different\"")]
    [InlineData("transactionId", "null")]
    [InlineData("intentDigest", "null")]
    [InlineData("state", "null")]
    [InlineData("error", "null")]
    [InlineData("linksChanged", "65")]
    [InlineData("bytesWritten", "65537")]
    public async Task Plan_ShouldRejectMalformedResponses(string property, string value)
    {
        var runner = new Runner((request, _) =>
        {
            var reply = JsonNode.Parse(Reply(request, "planned").StandardOutput)!.AsObject();
            reply[property] = JsonNode.Parse(value);
            return Result(request, reply.ToJsonString());
        });
        var (service, profile, request) = Context(runner);
        var reply = await NginxHelperClient.CallAsync(service, profile, "plan", request with { TransactionId = "" }, default);
        reply.Ok.Should().BeFalse();
        reply.State.Should().Be("unknown");
        runner.Operations.Should().Equal("plan");
    }

    [Fact]
    public async Task Plan_ShouldSendContentInStdinAndBindTheExpectedPreState()
    {
        SshCommandRequest? observed = null;
        var runner = new Runner((request, _) => { observed = request; return Reply(request, "planned"); });
        var (service, profile, _) = Context(runner);
        var request = NginxHelperClient.Create("/etc/nginx/example.conf", "synthetic-content", "old", true, false);
        await NginxHelperClient.CallAsync(service, profile, "plan", request, default);
        observed!.CommandText.Should().NotContain("synthetic-content");
        observed.CommandText.Should().Contain("kelpie-web-permission-helper nginx-plan 1");
        using var input = JsonDocument.Parse(observed.StandardInput!);
        input.RootElement.GetProperty("expectedHash").GetString().Should().Be(NginxHelperClient.Hash("old"));
        input.RootElement.GetProperty("expectedExists").GetBoolean().Should().BeTrue();
        Encoding.UTF8.GetString(Convert.FromBase64String(input.RootElement.GetProperty("contentBase64").GetString()!)).Should().Be("synthetic-content");
    }

    [Fact]
    public async Task Apply_ShouldRespectReadOnlyModeBeforeDispatch()
    {
        var runner = new Runner((request, _) => Reply(request, "applied"));
        var (service, profile, request) = Context(runner, KelpiePolicyMode.ReadOnly);
        Func<Task> act = () => NginxHelperClient.CallAsync(service, profile, "apply", request, default);
        await act.Should().ThrowAsync<KelpiePolicyError>();
        runner.Operations.Should().BeEmpty();
    }

    private static (SshCommandService, SshConnectionProfile, NginxHelperRequest) Context(Runner runner, KelpiePolicyMode mode = KelpiePolicyMode.Expert)
    {
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var profile = new SshConnectionProfile
        {
            Name = "nginx-test", Host = "example.invalid", UserName = "deploy", PrivateKeyPath = "test-key",
            OsFamily = "debian", Mode = mode, Capabilities = PolicySet.Empty,
        };
        return (service, profile, new NginxHelperRequest
        {
            RequestId = Guid.NewGuid().ToString("N"), Target = "/etc/nginx/example.conf",
            TransactionId = "0123456789abcdef0123456789abcdef", IntentDigest = new string('a', 64),
        });
    }

    private static SshCommandResult Reply(SshCommandRequest request, string state, bool changed = false)
    {
        using var input = JsonDocument.Parse(request.StandardInput!);
        return Result(request, JsonSerializer.Serialize(new
        {
            protocol = 1, requestId = input.RootElement.GetProperty("requestId").GetString(),
            transactionId = "0123456789abcdef0123456789abcdef", ok = true, state,
            configChanged = changed, linksChanged = 0, bytesWritten = changed ? 4 : 0,
            intentDigest = new string('a', 64), originalHash = new string('b', 64), currentHash = new string('c', 64),
            recoverable = state == "applied", error = "",
        }));
    }

    private static SshCommandResult Result(SshCommandRequest request, string stdout, bool timedOut = false) =>
        new(request.CommandName, request.CommandText, timedOut ? 1 : 0, stdout, "", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, timedOut);

    private sealed class Runner(Func<SshCommandRequest, string, SshCommandResult> reply) : ISshCommandRunner
    {
        public List<string> Operations { get; } = [];
        public Task<SshCommandResult> ExecuteAsync(SshCommandRequest request, CancellationToken cancellationToken = default)
        {
            var operation = request.CommandName["service_config_nginx_helper_".Length..];
            Operations.Add(operation);
            return Task.FromResult(reply(request, operation));
        }
    }
}
