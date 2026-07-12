using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class AllowedCommandCatalogTests
{
    [Fact]
    public void CreateDefault_ShouldIncludeReadOnlyDiagnosticCommands()
    {
        var catalog = AllowedCommandCatalog.CreateDefault();

        var commands = catalog.List();

        commands.Select(command => command.Name).Should().Contain(
        [
            "get_system_info",
            "get_disk_usage",
            "get_memory_usage",
            "get_listening_ports",
            "get_failed_services",
            "cron_write",
            "cron_rollback",
            "user_apply_group_change",
            "user_rollback_group_change",
            "user_apply_permission_change",
            "user_rollback_permission_change",
            "firewall_check_rule",
            "firewall_apply_rule",
            "backup_run",
            "audit_verify",
            "audit_export",
            "tail_log",
            "service_config_nginx_version",
            "service_config_nginx_test_config",
            "service_config_nginx_read_config",
            "service_config_nginx_write_config",
            "service_config_nginx_rollback_config",
            "service_config_nginx_commit_config",
            "service_config_nginx_disable_default_sites",
            "service_config_nginx_rollback_default_sites",
            "service_logfile_nginx_read",
        ]);
        commands.Single(command => command.Name == "service_config_nginx_write_config")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "service_config_nginx_rollback_config")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "service_config_nginx_commit_config")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "service_config_nginx_test_config")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "service_config_nginx_disable_default_sites")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "service_config_nginx_rollback_default_sites")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Where(command => command.Name is not (
                "cron_write"
                or "cron_rollback"
                or "user_apply_group_change"
                or "user_rollback_group_change"
                or "user_apply_permission_change"
                or "user_rollback_permission_change"
                or "firewall_apply_rule"
                or "backup_run"
                or "service_config_nginx_write_config"
                or "service_config_nginx_rollback_config"
                or "service_config_nginx_commit_config"
                or "service_config_nginx_test_config"
                or "service_config_nginx_disable_default_sites"
                or "service_config_nginx_rollback_default_sites"))
            .Should().OnlyContain(command => command.RiskLevel == SshCommandRiskLevel.ReadOnly);
        commands.Where(command => command.Name is "cron_write" or "cron_rollback" or "user_apply_group_change" or "user_rollback_group_change" or "user_apply_permission_change" or "user_rollback_permission_change" or "firewall_apply_rule" or "backup_run")
            .Should().OnlyContain(command => command.RiskLevel == SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CreateForProfile_ShouldUseMatchingCommandProcessingProviders()
    {
        var profile = CreateProfile("alma", "dnf");
        var catalog = AllowedCommandCatalog.CreateForProfile(
            profile,
            CommandProcessingProviderCatalog.CreateDefault());

        catalog.TryGet("pkg_simulate_install", out var command).Should().BeTrue();

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["package"] = "nginx",
        });
        commandText.Should().Be("sudo -n dnf install -y --setopt=tsflags=test 'nginx'");
    }

    [Fact]
    public void CreateForProfile_ShouldUseDebianPackageAndServiceProviders()
    {
        var profile = CreateProfile("ubuntu", "apt");
        var catalog = AllowedCommandCatalog.CreateForProfile(
            profile,
            CommandProcessingProviderCatalog.CreateDefault());

        catalog.TryGet("pkg_install", out var packageInstall).Should().BeTrue();
        catalog.TryGet("service_enable_now", out var serviceEnableNow).Should().BeTrue();
        catalog.TryGet("service_restart", out var serviceRestart).Should().BeTrue();
        catalog.TryGet("service_disable", out var serviceDisable).Should().BeTrue();

        packageInstall.BuildCommandText(new Dictionary<string, string>
        {
            ["package"] = "nginx",
        }).Should().Be("sudo -n env DEBIAN_FRONTEND=noninteractive apt-get install -y 'nginx'");
        serviceEnableNow.BuildCommandText(new Dictionary<string, string>
        {
            ["service"] = "nginx.service",
        }).Should().Be("sudo -n systemctl enable --now 'nginx.service'");
        serviceRestart.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        serviceDisable.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CreateForProfile_ShouldExcludeUnsupportedPackageProvider()
    {
        var profile = CreateProfile("debian", "dnf");
        var catalog = AllowedCommandCatalog.CreateForProfile(
            profile,
            CommandProcessingProviderCatalog.CreateDefault());

        catalog.TryGet("get_system_info", out _).Should().BeTrue();
        catalog.TryGet("pkg_simulate_install", out _).Should().BeFalse();
    }

    [Fact]
    public void Constructor_ShouldKeepExplicitRiskLevel()
    {
        var catalog = new AllowedCommandCatalog(
        [
            new AllowedCommandDefinition(
                "update_packages",
                "apt-get update",
                TimeSpan.FromSeconds(30),
                RiskLevel: SshCommandRiskLevel.ConfirmRequired),
        ]);

        catalog.TryGet("update_packages", out var command).Should().BeTrue();
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void TailLog_ShouldRenderSafeJournalctlCommand()
    {
        var catalog = AllowedCommandCatalog.CreateDefault();
        catalog.TryGet("tail_log", out var command).Should().BeTrue();

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["service"] = "nginx.service",
            ["lines"] = "100",
        });

        commandText.Should().Be("journalctl -u 'nginx.service' -n '100' --no-pager");
    }

    [Fact]
    public void TailLog_ShouldRejectUnsafeServiceName()
    {
        var catalog = AllowedCommandCatalog.CreateDefault();
        catalog.TryGet("tail_log", out var command).Should().BeTrue();

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["service"] = "nginx.service; rm -rf /",
            ["lines"] = "100",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument contains a dangerous fragment: service");
    }

    [Fact]
    public void TailLog_ShouldRejectUnsafeLineCount()
    {
        var catalog = AllowedCommandCatalog.CreateDefault();
        catalog.TryGet("tail_log", out var command).Should().BeTrue();

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["service"] = "nginx.service",
            ["lines"] = "100000",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: lines");
    }

    [Fact]
    public void TryGet_ShouldRejectDangerousCommandName()
    {
        var catalog = AllowedCommandCatalog.CreateDefault();

        var result = catalog.TryGet("get_system_info; rm -rf /", out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void Constructor_ShouldRejectDeniedExecutableName()
    {
        var action = () => new AllowedCommandCatalog(
        [
            new AllowedCommandDefinition("bad", "rm -rf /", TimeSpan.FromSeconds(1)),
        ]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command executable is denied: rm");
    }

    [Fact]
    public void Constructor_ShouldRejectUndefinedTemplateArgument()
    {
        var action = () => new AllowedCommandCatalog(
        [
            new AllowedCommandDefinition(
                "bad",
                "journalctl -u {service} --no-pager",
                TimeSpan.FromSeconds(1)),
        ]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command template references an undefined argument: service");
    }

    [Fact]
    public void Constructor_ShouldRejectUnusedArgumentDefinition()
    {
        var action = () => new AllowedCommandCatalog(
        [
            new AllowedCommandDefinition(
                "bad",
                "journalctl --no-pager",
                TimeSpan.FromSeconds(1),
                [new AllowedCommandParameterDefinition("service")]),
        ]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument is not used by template: service");
    }

    private static SshConnectionProfile CreateProfile(string osFamily, string packageManager)
    {
        return new SshConnectionProfile
        {
            Name = "test",
            Host = "example.invalid",
            UserName = "deploy",
            PrivateKeyPath = "id_ed25519",
            OsFamily = osFamily,
            PackageManager = packageManager,
            Capabilities = PolicySet.Empty,
        };
    }
}
