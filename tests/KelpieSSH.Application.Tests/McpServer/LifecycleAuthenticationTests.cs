using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using KelpieMCPServer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Moyai.ProviderAuthentication;

namespace KelpieSSH.Application.Tests.McpServer;

public sealed class LifecycleAuthenticationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deployment_WhenRestartingBetweenStages_RestoresTransferAndCompletes(bool rollback)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        await File.WriteAllTextAsync(fixture.ArtifactPath, "artifact");
        var hash = Convert.ToHexString(SHA256.HashData("artifact"u8));
        var context = ServerDeploymentToolTests.CreateDeploymentContext(hash.ToLowerInvariant());
        async Task Stage(string tool, object args, Func<Task<ServerDeployResult>> action)
        {
            var result = await fixture.Filter.ExecuteAsync(tool, Args(args), "Bearer " + fixture.Sign(tool),
                "operation-1", async () => LifecycleToolFilter.Result(await action()));
            result.IsError.Should().NotBe(true);
            result.StructuredContent!.Value.GetProperty("success").GetBoolean().Should().BeTrue();
            await fixture.InitializeAsync();
        }
        await Stage("deploy_prepare", new { deploymentId = "flow", targetName = "test", destination = "/release/app.bin" },
            () => Task.FromResult(KelpieTools.PrepareDeployment(context.Catalog, fixture.Transfers, fixture.Deployments,
                "flow", "test", "/release/app.bin", "target-1")));
        await Stage("deploy_upload", new { deploymentId = "flow", artifactPath = fixture.ArtifactPath, sha256 = hash },
            () => KelpieTools.UploadDeploymentAsync(fixture.Transfers, fixture.Deployments, "flow", fixture.ArtifactPath, hash));
        await Stage("deploy_activate", new { deploymentId = "flow" },
            () => KelpieTools.ActivateDeploymentAsync(context.Service, context.Catalog, context.Provider,
                fixture.Transfers, fixture.Deployments, "flow"));
        await Stage("deploy_verify", new { deploymentId = "flow" },
            () => KelpieTools.VerifyDeploymentAsync(context.Service, context.Catalog, context.Provider, fixture.Deployments, "flow"));
        if (rollback)
            await Stage("deploy_rollback", new { deploymentId = "flow" },
                () => KelpieTools.RollbackDeploymentAsync(context.Service, context.Catalog, context.Provider,
                    fixture.Transfers, fixture.Deployments, "flow"));
        await Stage("deploy_cleanup", new { deploymentId = "flow" },
            () => KelpieTools.CleanupDeploymentAsync(context.Service, context.Catalog, context.Provider,
                fixture.Transfers, fixture.Deployments, "flow"));
        (await fixture.Journal.FindAsync("flow"))!.Deployment!.State.Should().Be(ServerDeploymentState.Cleaned);
    }

    [Theory]
    [InlineData("TEST")]
    [InlineData("target-1")]
    public async Task HttpTransport_WhenBootstrapAndProtectedCalls_UsesRequestHeaderBoundary(string targetName)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        await fixture.Journal.BeginAsync("d1", fixture.Options.Targets.Single(), "deploy_prepare", "hash");
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(fixture.Options);
        builder.Services.AddSingleton(fixture.Validator);
        builder.Services.AddSingleton(fixture.Journal);
        builder.Services.AddSingleton(fixture.Deployments);
        builder.Services.AddSingleton(fixture.Transfers);
        builder.Services.AddSingleton<LifecycleToolFilter>();
        builder.Services.AddMcpServer().WithHttpTransport(o => o.Stateless = true)
            .WithRequestFilters(filters => filters.AddCallToolFilter(next => (request, ct) =>
                request.Services!.GetRequiredService<LifecycleToolFilter>().InvokeAsync(request, next, ct)))
            .WithTools([
                McpServerTool.Create((Func<ProviderVersionResult>)KelpieTools.GetProviderVersion),
                McpServerTool.Create((Func<ProviderCapabilitiesResult>)KelpieTools.GetProviderCapabilities),
                McpServerTool.Create((Func<string, LifecycleDeploymentStatus>)KelpieTools.GetDeploymentStatus),
                McpServerTool.Create((Func<string, string, string, string?, ServerDeployResult>)
                    ((deploymentId, targetName, destination, targetId) => KelpieTools.PrepareDeployment(
                        ServerDeploymentToolTests.CreateDeploymentContext("unused").Catalog,
                        fixture.Transfers, fixture.Deployments, deploymentId, targetName, destination, targetId)),
                    new McpServerToolCreateOptions { Name = "deploy_prepare" }),
            ]);
        await using var app = builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
            async Task<string> Post(object payload, string? assertion = null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
                { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
                if (assertion is not null)
                {
                    request.Headers.Authorization = new("Bearer", assertion);
                    request.Headers.Add("X-Moyai-Operation-Id", "operation-1");
                }
                using var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            var initialized = await Post(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new
            { protocolVersion = "2025-03-26", capabilities = new { }, clientInfo = new { name = "isolated-test", version = "1" } } });
            initialized.Should().Contain("protocolVersion");
            (await Post(new { jsonrpc = "2.0", id = 2, method = "tools/list", @params = new { } })).Should().Contain("deploy_status");
            var bootstrap = await Post(new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new
            { name = "provider_capabilities", arguments = new { } } });
            bootstrap.Should().Contain("kelpiessh");
            var call = new { jsonrpc = "2.0", id = 4, method = "tools/call", @params = new
            { name = "deploy_status", arguments = new { deploymentId = "d1" } } };
            (await Post(call)).Should().Contain("auth_assertion_missing");
            var token = fixture.Sign("deploy_status");
            var accepted = await Post(call, token);
            accepted.Should().Contain("result-unknown").And.NotContain(token);
            var prepare = new { jsonrpc = "2.0", id = 5, method = "tools/call", @params = new
            { name = "deploy_prepare", arguments = new { deploymentId = "http-prepare", targetName, destination = "/release/app" } } };
            var prepared = await Post(prepare, fixture.Sign("deploy_prepare"));
            prepared.Should().Contain("target-1").And.NotContain("deployment_stage_failed").And.NotContain("deployment_result_unknown");
            (await fixture.Journal.FindAsync("http-prepare"))!.Deployment!.TargetName.Should().Be("test");
        }
        finally { await app.StopAsync(); }
    }

    [Theory]
    [InlineData("aud", "githubie", "auth_audience_mismatch")]
    [InlineData("aud", "buckettie", "auth_audience_mismatch")]
    [InlineData("aud", "server", "auth_audience_mismatch")]
    [InlineData("iss", "moyai:other", "auth_assertion_invalid")]
    [InlineData("project", "10000000-0000-0000-0000-000000000002", "auth_project_mismatch")]
    [InlineData("resource", "other-target", "auth_project_mismatch")]
    [InlineData("resource_kind", "repository", "auth_project_mismatch")]
    [InlineData("operation_id", "other-operation", "auth_project_mismatch")]
    [InlineData("protocol_version", "1", "auth_protocol_unsupported")]
    [InlineData("scope", "deploy.activate", "auth_scope_denied")]
    [InlineData("expired", "true", "auth_assertion_expired")]
    [InlineData("unknown-key", "true", "auth_key_unknown")]
    [InlineData("tampered", "true", "auth_assertion_invalid")]
    [InlineData("repository", "forbidden-mixed-context", "auth_assertion_invalid")]
    public async Task Authentication_WhenContextInvalid_DoesNotInvokeTool(string field, string value, string code)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var calls = 0;
        var result = await fixture.Filter.ExecuteAsync("target_status", Args(new { targetName = "test" }),
            "Bearer " + fixture.Sign("target_status", field, value), "operation-1",
            () => { calls++; return Task.FromResult(LifecycleToolFilter.Result(new { ok = true })); });
        result.IsError.Should().BeTrue();
        result.Content.OfType<TextContentBlock>().Single().Text.Should().Be(code);
        calls.Should().Be(0);
    }

    [Theory]
    [InlineData("target_status")]
    [InlineData("deploy_prepare")]
    [InlineData("deploy_upload")]
    [InlineData("deploy_activate")]
    [InlineData("deploy_verify")]
    [InlineData("deploy_rollback")]
    [InlineData("deploy_cleanup")]
    [InlineData("deploy_status")]
    public async Task Validator_WhenMinimalToolScopeProvided_AcceptsOnlyThatScope(string tool)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var result = await fixture.Validator.ValidateAsync(fixture.Sign(tool), fixture.Context(tool));
        result.Context.Tool.Should().Be(tool);
        result.Context.Scopes.Should().Equal(LifecycleContract.ToolScopes[tool]);
    }

    [Fact]
    public async Task Replay_WhenValidatorRecreated_RejectsUsedAssertion()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var assertion = fixture.Sign("target_status");
        await fixture.Validator.ValidateAsync(assertion, fixture.Context("target_status"));
        await fixture.InitializeAsync();
        var action = () => fixture.Validator.ValidateAsync(assertion, fixture.Context("target_status"));
        (await action.Should().ThrowAsync<ProviderAuthenticationException>()).Which.Code.Should().Be("auth_replay_detected");
    }

    [Fact]
    public async Task TrustBundle_WhenKeyRevoked_AppliesWithoutRestart()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        fixture.WriteTrust(SigningKeyState.Revoked);
        var action = () => fixture.Validator.ValidateAsync(fixture.Sign("target_status"), fixture.Context("target_status"));
        (await action.Should().ThrowAsync<ProviderAuthenticationException>()).Which.Code.Should().Be("auth_key_revoked");
    }

    [Fact]
    public async Task Prepare_WhenRestarted_ReturnsCachedResultAndRejectsDifferentInput()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var args = Args(new { deploymentId = "d1", targetName = "test", destination = "/release/app" });
        await fixture.Filter.ExecuteAsync("deploy_prepare", args, "Bearer " + fixture.Sign("deploy_prepare"), "operation-1", () =>
        {
            var transfer = fixture.Transfers.Create("test", "default");
            var deployment = fixture.Deployments.Prepare("d1", "test", "target-1", "default", transfer.Id);
            return Task.FromResult(LifecycleToolFilter.Result(new ServerDeployResult(true, deployment)));
        });
        await fixture.InitializeAsync();
        var calls = 0;
        Task<CallToolResult> UnexpectedCall() { calls++; return Task.FromResult(LifecycleToolFilter.Failure("unexpected")); }
        var cached = await fixture.Filter.ExecuteAsync("deploy_prepare", args, "Bearer " + fixture.Sign("deploy_prepare"),
            "operation-1", UnexpectedCall);
        cached.IsError.Should().NotBe(true);
        var snapshot = await fixture.Journal.FindAsync("d1");
        snapshot!.Deployment!.State.Should().Be(ServerDeploymentState.Prepared);
        snapshot.Target.ProjectId.Should().Be(fixture.Project);
        args["destination"] = JsonSerializer.SerializeToElement("/different");
        var conflict = await fixture.Filter.ExecuteAsync("deploy_prepare", args, "Bearer " + fixture.Sign("deploy_prepare"),
            "operation-1", UnexpectedCall);
        conflict.Content.OfType<TextContentBlock>().Single().Text.Should().Be("idempotency-conflict");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task Stage_WhenProcessStopsAfterReservation_StatusIsUnknownAndNoStageReplays()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        await fixture.Journal.BeginAsync("d1", fixture.Options.Targets.Single(), "deploy_prepare", "hash");
        await fixture.InitializeAsync();
        var calls = 0;
        Task<CallToolResult> UnexpectedCall() { calls++; return Task.FromResult(LifecycleToolFilter.Failure("unexpected")); }
        var status = await fixture.Filter.ExecuteAsync("deploy_status", Args(new { deploymentId = "d1" }),
            "Bearer " + fixture.Sign("deploy_status"), "operation-1", UnexpectedCall);
        status.StructuredContent!.Value.GetProperty("resultUnknown").GetBoolean().Should().BeTrue();
        var upload = await fixture.Filter.ExecuteAsync("deploy_upload", Args(new { deploymentId = "d1" }),
            "Bearer " + fixture.Sign("deploy_upload"), "operation-1", UnexpectedCall);
        upload.Content.OfType<TextContentBlock>().Single().Text.Should().Be("deployment_result_unknown");
        calls.Should().Be(0);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("target-1")]
    public async Task Target_WhenCallerSuppliesAnotherId_RejectsWithoutInvocation(string targetName)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var result = await fixture.Filter.ExecuteAsync("target_status", Args(new { targetName, targetId = "forged" }),
            "Bearer " + fixture.Sign("target_status"), "operation-1", () => throw new Exception("must not run"));
        result.Content.OfType<TextContentBlock>().Single().Text.Should().Be("auth_project_mismatch");
    }

    [Theory]
    [InlineData("target_status")]
    [InlineData("deploy_prepare")]
    public async Task Target_WhenSelectedByRegisteredId_InvokesCanonicalProfile(string tool)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var arguments = tool == "target_status"
            ? Args(new { targetName = "target-1", targetId = "target-1" })
            : Args(new { deploymentId = "id-alias", targetName = "target-1", targetId = "target-1", destination = "/release/app" });
        var calls = 0;
        await fixture.Filter.ExecuteAsync(tool, arguments, "Bearer " + fixture.Sign(tool), "operation-1", () =>
        {
            calls++;
            arguments["targetName"].GetString().Should().Be("test");
            arguments["targetId"].GetString().Should().Be("target-1");
            return Task.FromResult(LifecycleToolFilter.Result(new { ok = true }));
        });
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Target_WhenIdCollidesWithAnotherProfile_RejectsWithoutInvocation()
    {
        using var fixture = new Fixture();
        fixture.Options.Targets.Add(new(Guid.NewGuid(), "target-2", "target-1"));
        await fixture.InitializeAsync();
        var result = await fixture.Filter.ExecuteAsync("target_status", Args(new { targetName = "target-1", targetId = "target-1" }),
            "Bearer " + fixture.Sign("target_status"), "operation-1", () => throw new Exception("must not run"));
        result.Content.OfType<TextContentBlock>().Single().Text.Should().Be("auth_project_mismatch");
    }

    [Fact]
    public async Task Request_WhenUnconfiguredOrMissingBearer_FailsClosed()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var unconfigured = new LifecycleToolFilter(new HttpContextAccessor(), fixture.Deployments,
            fixture.Transfers, NullLogger<LifecycleToolFilter>.Instance);
        var result = await unconfigured.ExecuteAsync("target_status", Args(new { targetName = "test" }), null, null,
            () => throw new Exception("must not run"));
        result.IsError.Should().BeTrue();
        result = await fixture.Filter.ExecuteAsync("target_status", Args(new { targetName = "test" }), null, "operation-1",
            () => throw new Exception("must not run"));
        result.Content.OfType<TextContentBlock>().Single().Text.Should().Be("auth_assertion_missing");
    }

    [Fact]
    public async Task Stage_WhenDelegateThrows_DoesNotExposeSecretAndKeepsPendingJournal()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var result = await fixture.Filter.ExecuteAsync("deploy_prepare",
            Args(new { deploymentId = "d1", targetName = "test" }), "Bearer " + fixture.Sign("deploy_prepare"),
            "operation-1", () => throw new InvalidOperationException("private-key-material-marker"));
        JsonSerializer.Serialize(result).Should().NotContain("private-key-material-marker");
        (await fixture.Journal.FindAsync("d1"))!.ResultUnknown.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(180, true)]
    public async Task Authentication_WhenReplayReservationCrossesExpiry_DoesNotDispatchExpiredTool(int elapsedSeconds, bool expired)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var clock = new ReservationClock();
        var replay = new AdvancingReplayCache(clock, elapsedSeconds);
        var validator = new Es256AssertionValidator(new FileAssertionTrustStore(fixture.Options.TrustBundlePath),
            replay, new(fixture.Options.Issuer), LifecycleContract.Capability, clock);
        var filter = new LifecycleToolFilter(new HttpContextAccessor(), fixture.Deployments, fixture.Transfers,
            NullLogger<LifecycleToolFilter>.Instance, fixture.Options, validator, fixture.Journal);
        var calls = 0;
        var result = await filter.ExecuteAsync("target_status", Args(new { targetName = "test" }),
            "Bearer " + fixture.Sign("target_status"), "operation-1", () =>
            {
                calls++;
                return Task.FromResult(LifecycleToolFilter.Result(new { ok = true }));
            });
        replay.Calls.Should().Be(1);
        calls.Should().Be(expired ? 0 : 1);
        if (expired)
        {
            result.IsError.Should().BeTrue();
            result.Content.OfType<TextContentBlock>().Single().Text.Should().Be("auth_assertion_expired");
        }
        else result.IsError.Should().NotBe(true);
    }

    [Theory]
    [InlineData("target_status", "valid")]
    [InlineData("target_status", "expired")]
    [InlineData("target_status", "revoked")]
    [InlineData("deploy_prepare", "valid")]
    [InlineData("deploy_prepare", "expired")]
    [InlineData("deploy_prepare", "revoked")]
    [InlineData("deploy_status", "valid")]
    [InlineData("deploy_status", "expired")]
    [InlineData("deploy_status", "revoked")]
    [InlineData("cached", "valid")]
    [InlineData("cached", "expired")]
    [InlineData("cached", "revoked")]
    public async Task Execution_WhenAuthorizationChangesAfterValidation_RechecksWithoutReplay(string path, string change)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var clock = new ReservationClock();
        var replay = new AdvancingReplayCache(clock, 0);
        var inner = new Es256AssertionValidator(new FileAssertionTrustStore(fixture.Options.TrustBundlePath),
            replay, new(fixture.Options.Issuer), LifecycleContract.Capability, clock);
        var validator = new WaitingValidator(inner, () =>
        {
            if (change == "expired") clock.Now = clock.Now.AddMinutes(3);
            if (change == "revoked") fixture.WriteTrust(SigningKeyState.Revoked);
        });
        var filter = new LifecycleToolFilter(new HttpContextAccessor(), fixture.Deployments, fixture.Transfers,
            NullLogger<LifecycleToolFilter>.Instance, fixture.Options, validator, fixture.Journal);
        var tool = path == "cached" ? "deploy_prepare" : path;
        var args = tool == "deploy_status" ? Args(new { deploymentId = "execution-test" })
            : Args(new { deploymentId = "execution-test", targetName = "test", destination = "/release/app" });
        if (tool == "target_status") args = Args(new { targetName = "test" });
        var calls = 0;
        Task<CallToolResult> Invoke()
        {
            calls++;
            if (tool == "target_status") return Task.FromResult(LifecycleToolFilter.Result(new { ok = true }));
            var transfer = fixture.Transfers.Create("test", "default");
            var deployment = fixture.Deployments.Prepare("execution-test", "test", "target-1", "default", transfer.Id);
            return Task.FromResult(LifecycleToolFilter.Result(new ServerDeployResult(true, deployment)));
        }
        if (path == "cached")
            await fixture.Filter.ExecuteAsync(tool, args, "Bearer " + fixture.Sign(tool), "operation-1", Invoke);
        if (tool == "deploy_status")
            await fixture.Journal.BeginAsync("execution-test", fixture.Options.Targets.Single(), "deploy_prepare", "hash");
        calls = 0;
        var result = await filter.ExecuteAsync(tool, args, "Bearer " + fixture.Sign(tool), "operation-1", Invoke);
        replay.Calls.Should().Be(1);
        validator.ExecutionChecks.Should().Be(1);
        calls.Should().Be(change == "valid" && path is "target_status" or "deploy_prepare" ? 1 : 0);
        if (change == "valid") result.IsError.Should().NotBe(true);
        else
        {
            result.IsError.Should().BeTrue();
            result.Content.OfType<TextContentBlock>().Single().Text.Should().Be(
                change == "expired" ? "auth_assertion_expired" : "auth_key_revoked");
            if (path == "deploy_prepare")
            {
                (await fixture.Journal.FindAsync("execution-test"))!.ResultUnknown.Should().BeFalse();
                fixture.WriteTrust(SigningKeyState.Active);
                var retry = await fixture.Filter.ExecuteAsync(tool, args, "Bearer " + fixture.Sign(tool), "operation-1", Invoke);
                retry.IsError.Should().NotBe(true);
                calls.Should().Be(1);
            }
        }
    }

    private sealed class WaitingValidator(Es256AssertionValidator inner, Action afterValidation)
        : IAssertionValidator, IAssertionExecutionValidator
    {
        public int ExecutionChecks { get; private set; }
        public async Task<AssertionPrincipal> ValidateAsync(string assertion, AssertionContext expected, CancellationToken cancellationToken = default)
        {
            var principal = await inner.ValidateAsync(assertion, expected, cancellationToken);
            afterValidation();
            return principal;
        }
        public Task EnsureCurrentAsync(AssertionPrincipal principal, CancellationToken cancellationToken = default)
        {
            ExecutionChecks++;
            return inner.EnsureCurrentAsync(principal, cancellationToken);
        }
    }

    [Theory]
    [InlineData("activate-failed", "activate-failed")]
    [InlineData("secret-provider-code", "unclassified-stage-failure")]
    [InlineData(null, "unclassified-stage-failure")]
    public async Task StageDiagnostic_WhenStageFails_IsSafeAndSurvivesRestart(string? code, string expected)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        await SeedDiagnosticDeploymentAsync(fixture);
        var calls = 0;
        var before = DateTimeOffset.UtcNow;
        var result = await fixture.Filter.ExecuteAsync("deploy_activate", Args(new { deploymentId = "diagnostic-1" }),
            "Bearer " + fixture.Sign("deploy_activate"), "operation-1", () =>
            {
                calls++;
                var current = fixture.Deployments.Get("diagnostic-1");
                var failed = fixture.Deployments.Update(current,
                    current with { Error = new(code!, "secret-provider-message", true) });
                return Task.FromResult(LifecycleToolFilter.Result(new ServerDeployResult(false, failed)));
            });
        var diagnostic = result.StructuredContent!.Value.GetProperty("diagnostic");
        diagnostic.GetProperty("errorCode").GetString().Should().Be(expected);
        diagnostic.GetProperty("deploymentId").GetString().Should().Be("diagnostic-1");
        diagnostic.GetProperty("operationId").GetString().Should().Be("operation-1");
        diagnostic.GetProperty("tool").GetString().Should().Be("deploy_activate");
        diagnostic.GetProperty("startedAtUtc").GetDateTimeOffset().Should().BeOnOrAfter(before);
        diagnostic.GetProperty("completedAtUtc").GetDateTimeOffset().Should()
            .BeOnOrAfter(diagnostic.GetProperty("startedAtUtc").GetDateTimeOffset());
        var json = result.StructuredContent.Value.GetRawText();
        json.Should().Contain("deployment_stage_failed").And.NotContain("secret-provider");
        var snapshot = await fixture.Journal.FindAsync("diagnostic-1");
        snapshot!.Deployment!.Error!.Code.Should().Be("deployment_stage_failed");
        snapshot.Deployment.Error.Message.Should().NotContain("secret-provider");
        await fixture.InitializeAsync();
        var cached = await fixture.Filter.ExecuteAsync("deploy_activate", Args(new { deploymentId = "diagnostic-1" }),
            "Bearer " + fixture.Sign("deploy_activate"), "operation-1", () => throw new Exception("must not replay"));
        System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(json),
            System.Text.Json.Nodes.JsonNode.Parse(cached.StructuredContent!.Value.GetRawText())).Should().BeTrue();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task StageDiagnostic_WhenStageSucceeds_HasTimingAndNoErrorClassification()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        var result = await SeedDiagnosticDeploymentAsync(fixture);
        var data = result.StructuredContent!.Value;
        data.GetProperty("success").GetBoolean().Should().BeTrue();
        data.GetProperty("diagnostic").GetProperty("errorCode").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("diagnostic").GetProperty("tool").GetString().Should().Be("deploy_prepare");
    }

    [Fact]
    public async Task StageDiagnostic_WhenDispatchThrows_LeavesUnknownReservationAndHidesMessage()
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync();
        await SeedDiagnosticDeploymentAsync(fixture);
        var result = await fixture.Filter.ExecuteAsync("deploy_activate", Args(new { deploymentId = "diagnostic-1" }),
            "Bearer " + fixture.Sign("deploy_activate"), "operation-1", () => throw new IOException("secret-provider-message"));
        result.IsError.Should().BeTrue();
        var json = result.StructuredContent!.Value.GetRawText();
        json.Should().Contain("unexpected-stage-failure").And.Contain("operation-1").And.NotContain("secret-provider");
        (await fixture.Journal.FindAsync("diagnostic-1"))!.ResultUnknown.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Logging_WhenProviderThrows_DoesNotAlterAuthorizationOrDispatch(bool authorized)
    {
        using var fixture = new Fixture();
        await fixture.InitializeAsync(new ThrowingLifecycleLogger());
        var calls = 0;
        var result = await fixture.Filter.ExecuteAsync("target_status", Args(new { targetName = "test" }),
            authorized ? "Bearer " + fixture.Sign("target_status") : null, "operation-1", () =>
            {
                calls++;
                return Task.FromResult(LifecycleToolFilter.Result(new { ok = true }));
            });
        calls.Should().Be(authorized ? 1 : 0);
        if (authorized) result.IsError.Should().NotBe(true);
        else result.Content.OfType<TextContentBlock>().Single().Text.Should().Be("auth_assertion_missing");
        JsonSerializer.Serialize(result).Should().NotContain("secret-logger-message");
    }

    private static Task<CallToolResult> SeedDiagnosticDeploymentAsync(Fixture fixture) =>
        fixture.Filter.ExecuteAsync("deploy_prepare", Args(new { deploymentId = "diagnostic-1", targetName = "test", destination = "/release/app" }),
            "Bearer " + fixture.Sign("deploy_prepare"), "operation-1", () =>
            {
                var transfer = fixture.Transfers.Create("test", "default");
                var deployment = fixture.Deployments.Prepare("diagnostic-1", "test", "target-1", "default", transfer.Id);
                deployment = fixture.Deployments.Update(deployment, deployment with { State = ServerDeploymentState.Uploaded });
                return Task.FromResult(LifecycleToolFilter.Result(new ServerDeployResult(true, deployment)));
            });

    private sealed class ThrowingLifecycleLogger : ILogger<LifecycleToolFilter>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new UnauthorizedAccessException("secret-logger-message");
    }

    private sealed class ReservationClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class AdvancingReplayCache(ReservationClock clock, int elapsedSeconds) : IAssertionReplayCache
    {
        public int Calls { get; private set; }
        public Task<bool> TryUseAsync(string issuer, string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
        {
            Calls++;
            clock.Now = clock.Now.AddSeconds(elapsedSeconds);
            return Task.FromResult(true);
        }
    }

    private static Dictionary<string, JsonElement> Args(object value) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(value))!;

    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "kelpie-lifecycle-test-" + Guid.NewGuid().ToString("N"));
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public Guid Project { get; } = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public string ArtifactPath => Path.Combine(_directory, "artifact.bin");
        public LifecycleOptions Options { get; }
        public ServerDeploymentStore Deployments { get; private set; } = new();
        public WebBulkTransferStore Transfers { get; private set; } = new();
        public LifecycleDeploymentStore Journal { get; private set; } = null!;
        public IAssertionValidator Validator { get; private set; } = null!;
        public LifecycleToolFilter Filter { get; private set; } = null!;

        public Fixture()
        {
            Directory.CreateDirectory(_directory);
            Options = new()
            {
                Issuer = "moyai:isolated-test", TrustBundlePath = Path.Combine(_directory, "trust.json"),
                ReplayDatabasePath = Path.Combine(_directory, "replay.db"),
                DeploymentDatabasePath = Path.Combine(_directory, "deploy.db"),
                Targets = [new(Project, "target-1", "test")],
            };
            WriteTrust(SigningKeyState.Active);
        }

        public async Task InitializeAsync(ILogger<LifecycleToolFilter>? logger = null)
        {
            Options.Validate();
            var replay = new SqliteAssertionReplayCache(new(Options.ReplayDatabasePath));
            await replay.InitializeAsync();
            Journal = new(Options.DeploymentDatabasePath);
            await Journal.InitializeAsync();
            Deployments = new();
            Transfers = new();
            Validator = new Es256AssertionValidator(new FileAssertionTrustStore(Options.TrustBundlePath), replay,
                new(Options.Issuer), LifecycleContract.Capability);
            Filter = new(new HttpContextAccessor(), Deployments, Transfers, logger ?? NullLogger<LifecycleToolFilter>.Instance,
                Options, Validator, Journal);
        }

        public void WriteTrust(SigningKeyState state)
        {
            var q = _key.ExportParameters(false).Q;
            var key = new AssertionTrustKey(Options.Issuer, "test-key", "ES256", Base64(q.X!), Base64(q.Y!),
                DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), state);
            File.WriteAllText(Options.TrustBundlePath, JsonSerializer.Serialize(new[] { key }, AssertionJson.Options));
        }

        public AssertionContext Context(string tool) => AssertionContext.ForResource("kelpiessh", Project,
            "kelpie_target", "target-1", tool, LifecycleContract.ToolScopes[tool], "operation-1");

        public string Sign(string tool, string? changed = null, string? value = null)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = new Dictionary<string, object>
            {
                ["iss"] = Options.Issuer, ["sub"] = Options.Issuer, ["aud"] = "kelpiessh", ["prv"] = "kelpiessh",
                ["project"] = Project.ToString("D"), ["protocol_version"] = "2", ["resource_kind"] = "kelpie_target",
                ["resource"] = "target-1", ["operation_id"] = "operation-1", ["scope"] = LifecycleContract.ToolScopes[tool],
                ["jti"] = Guid.NewGuid().ToString("N"), ["iat"] = now, ["nbf"] = now, ["exp"] = now + 120,
            };
            if (changed == "expired") { payload["iat"] = now - 500; payload["nbf"] = now - 500; payload["exp"] = now - 380; }
            else if (changed == "scope") payload["scope"] = new[] { value! };
            else if (changed is not null and not ("unknown-key" or "tampered")) payload[changed] = value!;
            var header = new { alg = "ES256", typ = "JWT", kid = changed == "unknown-key" ? "other" : "test-key" };
            var input = Base64(JsonSerializer.SerializeToUtf8Bytes(header)) + "." + Base64(JsonSerializer.SerializeToUtf8Bytes(payload));
            var signature = _key.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (changed == "tampered") signature[0] ^= 1;
            return input + "." + Base64(signature);
        }

        private static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        public void Dispose() { _key.Dispose(); Directory.Delete(_directory, true); }
    }
}
