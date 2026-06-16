using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshCommandServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldRejectRootUser()
    {
        var service = CreateProviderBackedService(new FakeSshCommandRunner());
        var profile = CreateProfile("root");

        var action = async () => await service.ExecuteAsync(profile, "get_system_info");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Direct root SSH login is not allowed.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseCommandProcessingProviderForOsFamily()
    {
        var runner = new FakeSshCommandRunner();
        var service = new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
        var profile = CreateProfile("deploy", "alma", "dnf", KelpiePolicyMode.Expert);

        await service.ExecuteAsync(
            profile,
            "pkg_simulate_install",
            new Dictionary<string, string>
            {
                ["package"] = "nginx",
            });

        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.CommandText.Should().Be("sudo -n dnf install --assumeno 'nginx'");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRunManagedRemoteOperation()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var operation = new SshRemoteOperation(
            new SshRemoteEndpoint("example.invalid"),
            new SshRemoteCredential(
                "deploy",
                "private_key",
                PrivateKeyPath: "id_ed25519"),
            new SshRemotePolicy(
                "safe",
                allowedRoots:
                [
                    new SshRemoteAllowedRoot("/var/www/example", ["read", "list", "write", "cd"]),
                ],
                specialPaths:
                [
                    new SshRemoteSpecialPath("**/.env", "deny"),
                ]),
            new SshRemoteOperationSpec(
                "managed",
                "service_status",
                new Dictionary<string, string>
                {
                    ["service"] = "nginx",
                }),
            new SshRemoteOperationOptions(30, "op-example"),
            new SshRemoteTarget("debian", "apt"));

        await service.ExecuteAsync(operation);

        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.Profile.Name.Should().Be("op-example");
        runner.LastRequest.Profile.AllowedRootRules.Single().Access.Should()
            .Be(AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.Write | AllowedRootAccess.CD);
        runner.LastRequest.Profile.SpecialPaths.Single().Action.Should().Be(SpecialPathAction.Deny);
        runner.LastRequest.CommandText.Should().Be("systemctl status 'nginx' --no-pager");
        runner.LastRequest.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRunOperationCreatedFromProfile()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profile = CreateProfile(
            "deploy",
            allowedRootRules:
            [
                new AllowedRootRule("/var/www/example", AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.CD),
            ]);
        var operation = SshRemoteOperation.FromProfile(
            profile,
            "managed",
            "get_system_info",
            correlationId: "profile-adapter-test");

        await service.ExecuteAsync(operation);

        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.Profile.Name.Should().Be("profile-adapter-test");
        runner.LastRequest.Profile.Host.Should().Be(profile.Host);
        runner.LastRequest.Profile.AllowedRootRules.Single().Access.Should()
            .Be(AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.CD);
        runner.LastRequest.CommandText.Should().Be("uname -a");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectUnknownCommand()
    {
        var service = CreateProviderBackedService(new FakeSshCommandRunner());
        var profile = CreateProfile("deploy");

        var action = async () => await service.ExecuteAsync(profile, "cat_shadow");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SSH command is not allowed: cat_shadow");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassAllowedCommandToRunner()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profile = CreateProfile("deploy");

        var result = await service.ExecuteAsync(profile, "get_system_info");

        result.CommandName.Should().Be("get_system_info");
        result.CommandText.Should().Be("uname -a");
        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.Profile.Should().BeSameAs(profile);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassValidatedArgumentsToRunner()
    {
        var runner = new FakeSshCommandRunner();
        var catalog = new AllowedCommandCatalog(
        [
            new AllowedCommandDefinition(
                "tail_service_log",
                "journalctl -u {service} -n {lines} --no-pager",
                TimeSpan.FromSeconds(10),
                [
                    new AllowedCommandParameterDefinition("service", Pattern: "^[a-zA-Z0-9_.@-]+$"),
                    new AllowedCommandParameterDefinition("lines", Pattern: "^[0-9]{1,4}$"),
                ]),
        ]);
        var service = new SshCommandService(catalog, runner);
        var profile = CreateProfile("deploy");

        var result = await service.ExecuteAsync(
            profile,
            "tail_service_log",
            new Dictionary<string, string>
            {
                ["service"] = "nginx.service",
                ["lines"] = "50",
            });

        result.CommandText.Should().Be("journalctl -u 'nginx.service' -n '50' --no-pager");
        runner.LastRequest!.Arguments["service"].Should().Be("nginx.service");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectUnknownArgument()
    {
        var catalog = new AllowedCommandCatalog(
        [
            new AllowedCommandDefinition(
                "tail_service_log",
                "journalctl -u {service} --no-pager",
                TimeSpan.FromSeconds(10),
                [new AllowedCommandParameterDefinition("service")]),
        ]);
        var service = new SshCommandService(catalog, new FakeSshCommandRunner());
        var profile = CreateProfile("deploy");

        var action = async () => await service.ExecuteAsync(
            profile,
            "tail_service_log",
            new Dictionary<string, string>
            {
                ["service"] = "nginx.service",
                ["unexpected"] = "value",
            });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SSH command argument is not allowed: unexpected");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectDangerousArgument()
    {
        var catalog = new AllowedCommandCatalog(
        [
            new AllowedCommandDefinition(
                "tail_service_log",
                "journalctl -u {service} --no-pager",
                TimeSpan.FromSeconds(10),
                [new AllowedCommandParameterDefinition("service")]),
        ]);
        var service = new SshCommandService(catalog, new FakeSshCommandRunner());
        var profile = CreateProfile("deploy");

        var action = async () => await service.ExecuteAsync(
            profile,
            "tail_service_log",
            new Dictionary<string, string>
            {
                ["service"] = "nginx.service; rm -rf /",
            });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SSH command argument contains a dangerous fragment: service");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldPassReadOnlyCommandToRunner()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profile = CreateProfile("deploy");

        var result = await service.ExecuteRawAsync(profile, "uname -a");

        result.CommandName.Should().Be("raw_shell");
        result.CommandText.Should().Be("uname -a");
        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.CommandText.Should().Be("uname -a");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldRejectShutdownCommand()
    {
        var service = CreateProviderBackedService(new FakeSshCommandRunner());
        var profile = CreateProfile("deploy");

        var action = async () => await service.ExecuteRawAsync(profile, "shutdown now");

        await action.Should().ThrowAsync<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: command is forbidden: shutdown");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldAllowExitCommand()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profile = CreateProfile("deploy");

        await service.ExecuteRawAsync(profile, "exit");

        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.CommandText.Should().Be("exit");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldAllowCdWhenAllowedRootsIsEmpty()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profile = CreateProfile("deploy");

        await service.ExecuteRawAsync(profile, "cd /var/www");

        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.CommandText.Should().Be("cd /var/www");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldRejectCdOutsideAllowedRoots()
    {
        var service = CreateProviderBackedService(new FakeSshCommandRunner());
        var profile = CreateProfile("deploy", allowedRoots: ["/var/www"]);

        var action = async () => await service.ExecuteRawAsync(profile, "cd /tmp");

        await action.Should().ThrowAsync<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: cd target is outside AllowedRoots: /tmp");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldRejectCdWhenAllowedRootDoesNotAllowCd()
    {
        var service = CreateProviderBackedService(new FakeSshCommandRunner());
        var profile = CreateProfile(
            "deploy",
            allowedRootRules:
            [
                new AllowedRootRule("/var/www", AllowedRootAccess.Read),
            ]);

        var action = async () => await service.ExecuteRawAsync(profile, "cd /var/www");

        await action.Should().ThrowAsync<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: cd target is outside AllowedRoots: /var/www");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldAllowRelativeCdWhenAllowedRootsIsGlobal()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profile = CreateProfile("deploy", allowedRoots: ["*"]);

        await service.ExecuteRawAsync(profile, "cd var");

        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.CommandText.Should().Be("cd var");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldRejectRmOutsideWritableAllowedRoots()
    {
        var service = CreateProviderBackedService(new FakeSshCommandRunner());
        var profile = CreateProfile(
            "deploy",
            mode: KelpiePolicyMode.Expert,
            allowedRootRules:
            [
                new AllowedRootRule("/var/www", AllowedRootAccess.Read),
            ]);

        var action = async () => await service.ExecuteRawAsync(profile, "rm /var/www/old.log");

        await action.Should().ThrowAsync<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: rm target is outside writable AllowedRoots: /var/www/old.log");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldAllowRmInsideWritableAllowedRoots()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profile = CreateProfile(
            "deploy",
            mode: KelpiePolicyMode.Expert,
            allowedRootRules:
            [
                new AllowedRootRule("/var/www", AllowedRootAccess.Write),
            ]);

        await service.ExecuteRawAsync(profile, "rm /var/www/old.log");

        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.CommandText.Should().Be("rm /var/www/old.log");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldRejectRmDeniedBySpecialPaths()
    {
        var service = CreateProviderBackedService(new FakeSshCommandRunner());
        var profile = CreateProfile(
            "deploy",
            mode: KelpiePolicyMode.Expert,
            allowedRootRules:
            [
                new AllowedRootRule("/var/www", AllowedRootAccess.Write),
            ],
            specialPaths:
            [
                new SpecialPathRule("**/.env", SpecialPathAction.Deny),
            ]);

        var action = async () => await service.ExecuteRawAsync(profile, "rm /var/www/.env");

        await action.Should().ThrowAsync<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: rm target is denied by SpecialPaths: /var/www/.env");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldRequireInstallPackagePolicy()
    {
        var service = CreateProviderBackedService(new FakeSshCommandRunner());
        var profile = CreateProfile("deploy");

        var action = async () => await service.ExecuteRawAsync(profile, "apt install nginx");

        await action.Should().ThrowAsync<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: AllowInstallPackage is required for command: apt");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldAllowInstallPackageInMaintenanceMode()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profile = CreateProfile("deploy", mode: KelpiePolicyMode.Maintenance);

        await service.ExecuteRawAsync(profile, "apt install nginx");

        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.CommandText.Should().Be("apt install nginx");
    }

    [Fact]
    public async Task ExecuteRawAsync_ShouldRejectShellControlOperators()
    {
        var service = CreateProviderBackedService(new FakeSshCommandRunner());
        var profile = CreateProfile("deploy");

        var action = async () => await service.ExecuteRawAsync(profile, "uname -a; id");

        await action.Should().ThrowAsync<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: shell control operators are not allowed in interactive command.");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAllowNginxConfigTestForWebAdminRoleInSafeMode()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profile = CreateProfile(
            "deploy",
            mode: KelpiePolicyMode.Safe,
            roles: [KelpieRoleNames.WebAdmin]);

        await service.ExecuteAsync(profile, "service_config_nginx_test_config", channel: KelpieExecutionChannel.Mcp);

        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.CommandText.Should().Be("sudo -n nginx -t");
    }

    private static SshConnectionProfile CreateProfile(
        string userName,
        string osFamily = "debian",
        string packageManager = "apt",
        KelpiePolicyMode mode = KelpiePolicyMode.Safe,
        IReadOnlyCollection<string>? allowedRoots = null,
        IReadOnlyCollection<AllowedRootRule>? allowedRootRules = null,
        IReadOnlyCollection<SpecialPathRule>? specialPaths = null,
        IReadOnlyCollection<string>? roles = null)
    {
        return new SshConnectionProfile
        {
            Name = "test",
            Host = "example.invalid",
            UserName = userName,
            PrivateKeyPath = "id_ed25519",
            OsFamily = osFamily,
            PackageManager = packageManager,
            Mode = mode,
            Capabilities = PolicySet.Empty,
            Roles = roles ?? [],
            AllowedRoots = allowedRoots ?? [],
            AllowedRootRules = allowedRootRules ?? [],
            SpecialPaths = specialPaths ?? [],
        };
    }

    private static SshCommandService CreateProviderBackedService(ISshCommandRunner runner)
    {
        return new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
    }

    private sealed class FakeSshCommandRunner : ISshCommandRunner
    {
        public SshCommandRequest? LastRequest { get; private set; }

        public Task<SshCommandResult> ExecuteAsync(
            SshCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new SshCommandResult(
                request.CommandName,
                request.CommandText,
                0,
                "ok",
                string.Empty,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                TimedOut: false));
        }
    }
}
