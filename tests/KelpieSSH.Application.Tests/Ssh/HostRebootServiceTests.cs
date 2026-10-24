using KelpieMCPServer;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class HostRebootServiceTests : IDisposable
{
    private const string Before = "11111111-1111-1111-1111-111111111111";
    private const string After = "22222222-2222-2222-2222-222222222222";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "kelpie-reboot-test-" + Guid.NewGuid().ToString("N"));
    private readonly Runner _runner = new();
    private readonly Clock _clock = new();
    private FileHostRebootStore Store => new(_directory);
    private HostRebootService Service => new(_runner, Store, _clock);
    private static SshConnectionProfile Profile(HostRebootOptions? checks = null, KelpiePolicyMode mode = KelpiePolicyMode.Maintenance,
        string user = "operator", string? fingerprint = "SHA256:test", string host = "fixture.invalid", string os = "debian") => new()
    {
        Name = "fixture", Host = host, UserName = user, OsFamily = os, Mode = mode, PackageManager = "apt",
        PrivateKeyPath = Path.Combine(Path.GetTempPath(), "kelpie-reboot-fixture.key"),
        HostKeyFingerprintSha256 = fingerprint,
        HostReboot = (checks ?? new() { Enabled = true }) with { Paths = FixturePaths },
    };
    private static HostRebootPaths FixturePaths => new()
    {
        Sudo = "/usr/bin/sudo", Systemctl = "/usr/bin/systemctl", Cat = "/usr/bin/cat", Uname = "/usr/bin/uname",
        Curl = "/usr/bin/curl", BootId = "/proc/sys/kernel/random/boot_id", NullDevice = "/dev/null",
    };
    private async Task<HostRebootOperation> Dispatch(SshConnectionProfile? p = null)
    {
        p ??= Profile();
        var preview = await Service.PreviewAsync(p);
        return await Service.RequestAsync(p, preview.OperationId, preview.Confirmation);
    }

    [Fact]
    public async Task PreviewDoesNotRebootAndShowsCanonicalTargetAndImpact()
    {
        var op = await Service.PreviewAsync(Profile());
        Assert.Equal("Preview", op.State);
        Assert.Equal("fixture.invalid", op.Host);
        Assert.Equal(Before, op.BeforeBootId);
        Assert.Equal("6.1", op.BeforeKernel);
        Assert.Null(op.NextKernel);
        Assert.Contains("all services", op.Impact);
        Assert.Equal(0, _runner.Dispatches);
        Assert.Equivalent(op, Store.Read(op.OperationId));
    }

    [Theory]
    [InlineData(KelpiePolicyMode.ReadOnly, true, "operator", "SHA256:test", "debian")]
    [InlineData(KelpiePolicyMode.Safe, true, "operator", "SHA256:test", "debian")]
    [InlineData(KelpiePolicyMode.Expert, false, "operator", "SHA256:test", "debian")]
    [InlineData(KelpiePolicyMode.Maintenance, true, "root", "SHA256:test", "debian")]
    [InlineData(KelpiePolicyMode.Maintenance, true, "operator", null, "debian")]
    [InlineData(KelpiePolicyMode.Maintenance, true, "operator", "SHA256:test", "windows")]
    public async Task UnauthorizedProfilesCannotEvenPreview(KelpiePolicyMode mode, bool enabled, string user, string? key, string os)
    {
        await Assert.ThrowsAsync<KelpiePolicyError>(() => Service.PreviewAsync(Profile(new() { Enabled = enabled }, mode, user, key, os: os)));
        Assert.Empty(_runner.Commands);
    }

    [Theory]
    [InlineData("service; reboot")]
    [InlineData("--help")]
    [InlineData("service\nreboot")]
    public async Task InvalidUnitCannotReachRunner(string unit)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.PreviewAsync(Profile(new() { Enabled = true, Services = [unit] })));
        Assert.Empty(_runner.Commands);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:password@example.invalid")]
    public async Task UnsafeHttpConfigurationCannotReachRunner(string url)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.PreviewAsync(Profile(new() { Enabled = true, HttpUrls = [url] })));
        Assert.Empty(_runner.Commands);
    }

    [Fact]
    public async Task WrongApprovalAndExpiredPreviewDoNotDispatch()
    {
        var op = await Service.PreviewAsync(Profile());
        Assert.Equal("ApprovalRequired", (await Service.RequestAsync(Profile(), op.OperationId, "yes")).Error);
        _clock.Now += TimeSpan.FromMinutes(11);
        Assert.Equal("PreviewExpired", (await Service.RequestAsync(Profile(), op.OperationId, op.Confirmation)).Error);
        Assert.Equal(0, _runner.Dispatches);
    }

    [Fact]
    public async Task ChangedTargetOrChecksCannotReuseApproval()
    {
        var op = await Service.PreviewAsync(Profile());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.RequestAsync(Profile(host: "another.invalid"), op.OperationId, op.Confirmation));
        Assert.Equal("ProfileChanged", (await Service.RequestAsync(Profile(new() { Enabled = true, ExpectedKernel = "6.2" }), op.OperationId, op.Confirmation)).Error);
        Store.Save(op with { Checks = new() { Enabled = true, Services = ["changed.service"], Paths = FixturePaths } });
        Assert.Equal("ProfileChanged", (await Service.RequestAsync(Profile(), op.OperationId, op.Confirmation)).Error);
        Assert.Equal(0, _runner.Dispatches);
    }

    [Fact]
    public async Task ChangedBootInvalidatesPreview()
    {
        var op = await Service.PreviewAsync(Profile());
        _runner.Boot = After;
        Assert.Equal("PreviewStale", (await Service.RequestAsync(Profile(), op.OperationId, op.Confirmation)).Error);
        Assert.Equal(0, _runner.Dispatches);
    }

    [Fact]
    public async Task RequestIsDurablySingleDispatchAcrossServiceRestartAndDuplicatePreview()
    {
        var first = await Dispatch();
        Assert.Equal("Accepted", first.State);
        Assert.Equivalent(first, await Service.RequestAsync(Profile(), first.OperationId, first.Confirmation));
        var second = await Service.PreviewAsync(Profile());
        Assert.Equal("OperationInProgress", (await Service.RequestAsync(Profile(), second.OperationId, second.Confirmation)).Error);
        Assert.Equal(1, _runner.Dispatches);
        Assert.Equal("'/usr/bin/sudo' -n '/usr/bin/systemctl' reboot", _runner.Commands.Single(c => c.CommandName == "host_reboot").CommandText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostResponseOrTimeoutStaysUnknownAndNeverReplays(bool timedOut)
    {
        _runner.Reboot = timedOut ? "timeout" : "disconnect";
        var op = await Dispatch();
        Assert.Equal("Unknown", op.State);
        Assert.Equal("DispatchUnknown", op.Error);
        await Service.RequestAsync(Profile(), op.OperationId, op.Confirmation);
        Assert.Equal(1, _runner.Dispatches);
        _runner.Boot = After;
        Assert.Equal("Verified", (await Service.InspectAsync(Profile(), op.OperationId)).State);
    }

    [Fact]
    public async Task CrashAfterPersistingIntentCannotRedispatch()
    {
        var preview = await Service.PreviewAsync(Profile());
        var intent = preview with { State = "Dispatching", DispatchedAt = _clock.Now };
        Store.Save(intent);
        Assert.Equivalent(intent, await Service.RequestAsync(Profile(), preview.OperationId, preview.Confirmation));
        Assert.Equal(0, _runner.Dispatches);
    }

    [Fact]
    public async Task ChangedBootKernelAndHealthyChecksAreRequiredForVerified()
    {
        var p = Profile(new() { Enabled = true, ExpectedKernel = "6.2", Services = ["nginx.service"], HttpUrls = ["http://127.0.0.1/health"] });
        var op = await Dispatch(p);
        Assert.Equal("RebootNotObserved", (await Service.InspectAsync(p, op.OperationId)).Error);
        _runner.Boot = After;
        _runner.Kernel = "6.2";
        var result = await Service.InspectAsync(p, op.OperationId, wait: true);
        Assert.Equal("Verified", result.State);
        Assert.True(result.RebootObserved);
        Assert.True(result.Reconnected);
        Assert.Equal(1, _runner.Dispatches);
    }

    [Theory]
    [InlineData("kernel", "KernelMismatch")]
    [InlineData("service", "ServiceStopped")]
    [InlineData("http", "HttpFailed")]
    public async Task VerificationDistinguishesEachFailure(string failure, string error)
    {
        var p = Profile(new() { Enabled = true, ExpectedKernel = "6.1", Services = ["nginx"], HttpUrls = ["http://localhost/"] });
        var op = await Dispatch(p);
        _runner.Boot = After;
        _runner.Kernel = failure == "kernel" ? "wrong" : "6.1";
        _runner.Failure = failure;
        var result = await Service.InspectAsync(p, op.OperationId);
        Assert.Equal(error, result.Error);
        Assert.NotEqual("Verified", result.State);
    }

    [Theory]
    [InlineData("service", "ServiceStopped")]
    [InlineData("http", "HttpFailed")]
    public async Task Inspect_WhenLaterProbeTimesOut_PreservesObservedHealthFailure(string failure, string error)
    {
        var p = Profile(new() { Enabled = true, Services = ["nginx"], HttpUrls = ["http://localhost/"] });
        var op = await Dispatch(p);
        _runner.Boot = After;
        _runner.Failure = failure;
        Assert.Equal(error, (await Service.InspectAsync(p, op.OperationId)).Error);
        _runner.BeforeExecute = request =>
        {
            if (request.CommandName != "host_reboot_snapshot") return;
            _clock.Now += TimeSpan.FromSeconds(121);
            throw new SshCommandConnectionException(SshConnectionFailureKind.Timeout);
        };

        var result = await Service.InspectAsync(p, op.OperationId, wait: true);

        Assert.Equal("Failed", result.State);
        Assert.Equal(error, result.Error);
        Assert.True(result.RebootObserved);
        Assert.Equal(1, _runner.Dispatches);
        Assert.Equivalent(result, Store.Read(op.OperationId));
    }

    [Theory]
    [InlineData("service", "ServiceStopped")]
    [InlineData("http", "HttpFailed")]
    public async Task Inspect_WhenNextHealthProbeTimesOut_PersistsPartialFailure(string failure, string error)
    {
        var p = Profile(new() { Enabled = true, Services = ["nginx"],
            HttpUrls = ["http://localhost/first", "http://localhost/second"] });
        var op = await Dispatch(p);
        _runner.Boot = After;
        _runner.Failure = failure;
        var httpChecks = 0;
        _runner.BeforeExecute = request =>
        {
            if (request.CommandName != "host_reboot_http_check") return;
            if (++httpChecks != (failure == "service" ? 1 : 2)) return;
            _clock.Now += TimeSpan.FromSeconds(121);
            throw new SshCommandConnectionException(SshConnectionFailureKind.Timeout);
        };

        var result = await Service.InspectAsync(p, op.OperationId, wait: true);

        Assert.Equal("Failed", result.State);
        Assert.Equal(error, result.Error);
        Assert.True(result.RebootObserved);
        Assert.Equivalent(result, Store.Read(op.OperationId));
    }

    [Fact]
    public async Task Inspect_WhenHealthRecovers_ClearsPreviouslyObservedFailures()
    {
        var p = Profile(new() { Enabled = true, Services = ["nginx"], HttpUrls = ["http://localhost/"] });
        var op = await Dispatch(p);
        _runner.Boot = After;
        _runner.Failure = "service";
        Assert.Equal("ServiceStopped", (await Service.InspectAsync(p, op.OperationId)).Error);
        _runner.Failure = "";

        var result = await Service.InspectAsync(p, op.OperationId);

        Assert.Equal("Verified", result.State);
        Assert.Null(result.Error);
        Assert.Empty(result.FailedServices!);
        Assert.Empty(result.FailedHttpUrls!);
    }

    [Theory]
    [InlineData(SshConnectionFailureKind.HostKeyMismatch)]
    [InlineData(SshConnectionFailureKind.AuthenticationFailed)]
    public async Task ReconnectSecurityFailureIsTerminalAndDistinct(SshConnectionFailureKind kind)
    {
        var op = await Dispatch();
        _runner.ConnectionFailure = kind;
        var result = await Service.InspectAsync(Profile(), op.OperationId);
        Assert.Equal("Failed", result.State);
        Assert.Equal(kind.ToString(), result.Error);
        Assert.Equal(1, _runner.Dispatches);
    }

    [Fact]
    public async Task PermissionRefusalIsNotReportedAsAccepted()
    {
        _runner.Reboot = "denied";
        var op = await Dispatch();
        Assert.Equal("PermissionDenied", op.Error);
        Assert.Equal("Failed", op.State);
        Assert.Equivalent(op, await Service.InspectAsync(Profile(), op.OperationId));
    }

    [Fact]
    public async Task ExpiredReconnectBudgetCannotBeExtendedByInspection()
    {
        var op = await Dispatch();
        var count = _runner.Commands.Count;
        _clock.Now += TimeSpan.FromSeconds(121);
        var result = await Service.InspectAsync(Profile(), op.OperationId, wait: true);
        Assert.Equal("Unknown", result.State);
        Assert.Equal("ReconnectTimeout", result.Error);
        Assert.Equal(op.Deadline, result.Deadline);
        Assert.Equal(count, _runner.Commands.Count);
    }

    [Fact]
    public async Task CancelledInspectionDoesNotResendOrEraseJournal()
    {
        var op = await Dispatch();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.InspectAsync(Profile(), op.OperationId, true, cancellation.Token));
        Assert.Equivalent(op, Store.Read(op.OperationId));
        Assert.Equal(1, _runner.Dispatches);
    }

    [Fact]
    public async Task McpUsesSameOperationAndApprovalRules()
    {
        var catalog = new SshConnectionProfileCatalog([Profile()]);
        var op = await KelpieTools.PreviewHostRebootAsync(Service, catalog, "fixture");
        Assert.Equal("ApprovalRequired", (await KelpieTools.RequestHostRebootAsync(Service, catalog, "fixture", op.OperationId, "no")).Error);
        await KelpieTools.RequestHostRebootAsync(Service, catalog, "fixture", op.OperationId, op.Confirmation);
        _runner.Boot = After;
        Assert.Equal("Verified", (await KelpieTools.InspectHostRebootAsync(Service, catalog, "fixture", op.OperationId)).State);
    }

    [Fact]
    public void StoreRejectsTraversalAndSerializesAcrossInstances()
    {
        Assert.Throws<ArgumentException>(() => Store.Read("../outside"));
        using var guard = Store.Acquire(new string('A', 64));
        Assert.Throws<InvalidOperationException>(() => Store.Acquire(new string('A', 64)));
    }

    [Fact]
    public void EnablingOrChangingRebootPolicyRequiresTrustExpansionApproval()
    {
        var disabled = SshProfileAuthorizationSnapshot.FromProfile(Profile(new()));
        var enabled = SshProfileAuthorizationSnapshot.FromProfile(Profile());
        var changed = SshProfileAuthorizationSnapshot.FromProfile(Profile(new() { Enabled = true, Services = ["nginx"] }));
        Assert.Equal(SshProfileAuthorizationChangeKind.PrivilegeExpansion, SshProfileAuthorizationEvaluator.Compare(disabled, enabled).Kind);
        Assert.Equal(SshProfileAuthorizationChangeKind.PrivilegeExpansion, SshProfileAuthorizationEvaluator.Compare(enabled, changed).Kind);
        Assert.Equal(SshProfileAuthorizationChangeKind.PrivilegeReduction, SshProfileAuthorizationEvaluator.Compare(enabled, disabled).Kind);
        Assert.Null(disabled.HostRebootPolicy);
    }

    [Fact]
    public async Task ConcurrentRequestSeesDurableIntentAndCannotDispatch()
    {
        var op = await Service.PreviewAsync(Profile());
        var gate = new TaskCompletionSource<SshCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _runner.DispatchGate = gate;
        var first = Service.RequestAsync(Profile(), op.OperationId, op.Confirmation);
        Assert.Equal("Dispatching", Store.Read(op.OperationId).State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.RequestAsync(Profile(), op.OperationId, op.Confirmation));
        gate.SetResult(new("host_reboot", "", 0, "", "", _clock.Now, _clock.Now, false));
        Assert.Equal("Accepted", (await first).State);
        Assert.Equal(1, _runner.Dispatches);
    }

    [Fact]
    public async Task PostDispatchHostKeyFailureDoesNotUnblockAnotherReboot()
    {
        var op = await Dispatch();
        _runner.ConnectionFailure = SshConnectionFailureKind.HostKeyMismatch;
        Assert.Equal("HostKeyMismatch", (await Service.InspectAsync(Profile(), op.OperationId)).Error);
        _runner.ConnectionFailure = null;
        var next = await Service.PreviewAsync(Profile());
        Assert.Equal("OperationInProgress", (await Service.RequestAsync(Profile(), next.OperationId, next.Confirmation)).Error);
        Assert.Equal(1, _runner.Dispatches);
    }

    [Fact]
    public async Task ApprovalRefusalIsAuditedWithoutErasingPreview()
    {
        var op = await Service.PreviewAsync(Profile());
        await Service.RequestAsync(Profile(), op.OperationId, "refused");
        Assert.Contains("ApprovalRequired", File.ReadAllText(Path.Combine(_directory, op.OperationId + ".json.audit.jsonl")));
        Assert.Equivalent(op, Store.Read(op.OperationId));
    }

    [Fact]
    public async Task CapabilitiesAdvertiseAllDedicatedToolsAndGenericCommandCannotReboot()
    {
        var p = Profile();
        var catalog = new SshConnectionProfileCatalog([p]);
        var commands = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), _runner);
        var caps = await KelpieTools.GetSshCapabilitiesAsync(commands, catalog, "fixture");
        Assert.Equal(3, caps.Tools.Count(t => t.ToolName.StartsWith("ssh_host_reboot_", StringComparison.Ordinal) && t.Available));
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.ExecuteAsync(p, "host_reboot"));
        Assert.Equal(0, _runner.Dispatches);
    }

    [Fact]
    public void NewFailureKindPreservesExistingNumericValues()
    {
        Assert.Equal(0, (int)SshConnectionFailureKind.ConnectionFailed);
        Assert.Equal(1, (int)SshConnectionFailureKind.HostUnreachable);
        Assert.Equal(2, (int)SshConnectionFailureKind.Timeout);
        Assert.Equal(3, (int)SshConnectionFailureKind.AuthenticationFailed);
        Assert.Equal(4, (int)SshConnectionFailureKind.HostKeyMismatch);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{}")]
    public async Task CorruptJournalCannotDispatchAndProducesHandledError(string contents)
    {
        var op = await Service.PreviewAsync(Profile());
        File.WriteAllText(Path.Combine(_directory, op.OperationId + ".json"), contents);
        await Assert.ThrowsAsync<InvalidDataException>(() => Service.RequestAsync(Profile(), op.OperationId, op.Confirmation));
        Assert.Equal(0, _runner.Dispatches);
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Runner : ISshCommandRunner
    {
        public string Boot = Before, Kernel = "6.1", Reboot = "ok", Failure = "";
        public SshConnectionFailureKind? ConnectionFailure;
        public TaskCompletionSource<SshCommandResult>? DispatchGate;
        public Action<SshCommandRequest>? BeforeExecute;
        public List<SshCommandRequest> Commands { get; } = [];
        public int Dispatches => Commands.Count(c => c.CommandName == "host_reboot");
        public Task<SshCommandResult> ExecuteAsync(SshCommandRequest request, CancellationToken cancellationToken = default)
        {
            Commands.Add(request);
            BeforeExecute?.Invoke(request);
            if (request.CommandName == "host_reboot" && DispatchGate is not null) return DispatchGate.Task;
            if (request.CommandName == "host_reboot" && Reboot == "disconnect")
                throw new SshCommandConnectionException(SshConnectionFailureKind.ConnectionFailed);
            if (request.CommandName != "host_reboot" && ConnectionFailure is { } kind)
                throw new SshCommandConnectionException(kind);
            var exit = request.CommandName == "host_reboot" && Reboot == "denied" ? 1
                : request.CommandName == "host_reboot_service_check" && Failure == "service" ? 3 : 0;
            var output = request.CommandName == "host_reboot_snapshot" ? Boot + "\n" + Kernel + "\n"
                : request.CommandName == "host_reboot_http_check" ? Failure == "http" ? "503" : "200" : "";
            return Task.FromResult(new SshCommandResult(request.CommandName, request.CommandText, exit, output, Reboot == "denied" ? "sudo: a password is required" : "",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, request.CommandName == "host_reboot" && Reboot == "timeout"));
        }
    }
}
