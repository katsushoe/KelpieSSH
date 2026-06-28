using FluentAssertions;
using KelpieSSH.Application.Ssh;
using System.Text;
using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class AllowedCommandProviderTests
{
    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldProvideReadOnlyDiagnosticCommands()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");

        provider.Supports(profile).Should().BeTrue();

        var commands = provider.GetCommands(profile);
        commands.Select(command => command.Name).Should().Contain(
        [
            "get_system_info",
            "get_os_release",
            "target_inventory",
            "get_uptime",
            "get_disk_usage",
            "get_memory_usage",
            "get_process_summary",
            "get_inode_usage",
            "get_mounts",
            "get_network_addresses",
            "get_routes",
            "get_dns_config",
            "cron_list",
            "cron_validate",
            "cron_check_write",
            "cron_write",
            "cron_rollback",
            "cert_inspect",
            "cert_expiry_check",
            "user_list",
            "user_info",
            "group_list",
            "group_info",
            "sudoers_check",
            "user_usage_check",
            "user_check_group_change",
            "user_apply_group_change",
            "user_rollback_group_change",
            "user_check_permission_change",
            "user_apply_permission_change",
            "user_rollback_permission_change",
            "user_file_ownership_check",
            "user_service_usage_check",
            "service_residual_config_check",
            "check_http_local",
            "check_tcp_connect_local",
            "get_listening_ports",
            "get_failed_services",
            "get_journal_recent",
            "service_status",
            "service_is_active",
            "service_is_enabled",
            "list_services",
            "support_report_collect",
            "firewall_status",
            "firewall_check_rule",
            "firewall_apply_rule",
            "backup_plan_check",
            "backup_run",
            "backup_verify",
            "audit_verify",
            "audit_export",
            "tail_log",
        ]);
        commands.Where(command => command.Name is not ("cron_write" or "cron_rollback" or "user_apply_group_change" or "user_rollback_group_change" or "user_apply_permission_change" or "user_rollback_permission_change" or "firewall_apply_rule" or "backup_run"))
            .Should().OnlyContain(command => command.RiskLevel == SshCommandRiskLevel.ReadOnly);
        commands.Where(command => command.Name is "cron_write" or "cron_rollback" or "user_apply_group_change" or "user_rollback_group_change" or "user_apply_permission_change" or "user_rollback_permission_change" or "firewall_apply_rule" or "backup_run")
            .Should().OnlyContain(command => command.RiskLevel == SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void NginxServiceConfigCommandProvider_ShouldProvideReadOnlyDiscoveryCommand()
    {
        var provider = new NginxServiceConfigCommandProvider();
        var profile = CreateProfile("debian", "apt");

        provider.Supports(profile).Should().BeTrue();

        var commands = provider.GetCommands(profile);
        commands.Select(command => command.Name).Should().Contain(
        [
            "service_config_nginx_version",
            "service_config_nginx_test_config",
            "service_config_nginx_read_config",
            "service_config_nginx_check_write_config",
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
        commands.Where(command => command.Name is not ("service_config_nginx_write_config" or "service_config_nginx_rollback_config" or "service_config_nginx_commit_config" or "service_config_nginx_test_config" or "service_config_nginx_disable_default_sites" or "service_config_nginx_rollback_default_sites"))
            .Should().OnlyContain(command => command.RiskLevel == SshCommandRiskLevel.ReadOnly);
    }

    [Fact]
    public void NginxServiceConfigCommandProvider_ShouldUseExistingTimezoneRegexGroupInLogScript()
    {
        var provider = new NginxServiceConfigCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "service_logfile_nginx_read");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["pathBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("/var/log/nginx/access.log")),
            ["allowedPathsBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("/var/log/nginx/access.log")),
            ["maxBytes"] = "262144",
            ["lines"] = "500",
            ["sinceMinutes"] = "10",
        });
        var scriptBase64 = Regex.Match(commandText, "b64decode\\('(?<script>[^']+)'\\)")
            .Groups["script"]
            .Value;
        var script = Encoding.UTF8.GetString(Convert.FromBase64String(scriptBase64));

        script.Should().Contain("tz = m.group(7)");
        script.Should().NotContain("tz = m.group(8)");
    }

    [Fact]
    public void NginxServiceConfigCommandProvider_ShouldAllowCreateAndRollbackCreatedConfigFiles()
    {
        var provider = new NginxServiceConfigCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var commands = provider.GetCommands(profile);
        var baseArguments = new Dictionary<string, string>
        {
            ["pathBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("/etc/nginx/conf.d/default.conf")),
            ["allowedPathsBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("\n")),
            ["allowedDirsBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("/etc/nginx/conf.d\n")),
        };
        var writeArguments = new Dictionary<string, string>(baseArguments, StringComparer.OrdinalIgnoreCase)
        {
            ["contentBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("server {}\n")),
        };

        var writeCommand = commands.Single(command => command.Name == "service_config_nginx_write_config").BuildCommandText(writeArguments);
        var checkCommand = commands.Single(command => command.Name == "service_config_nginx_check_write_config").BuildCommandText(baseArguments);
        var rollbackCommand = commands.Single(command => command.Name == "service_config_nginx_rollback_config").BuildCommandText(baseArguments);

        writeCommand.Should().Contain("KELPIE_CREATED_CONFIG_FILE_BACKUP_V1");
        writeCommand.Should().Contain("exists=os.path.exists(rp)");
        writeCommand.Should().Contain("exists and not os.path.isfile(rp)");
        checkCommand.Should().Contain("exists=os.path.exists(rp)");
        checkCommand.Should().Contain("exists and not os.path.isfile(rp)");
        rollbackCommand.Should().Contain("KELPIE_CREATED_CONFIG_FILE_BACKUP_V1");
        rollbackCommand.Should().Contain("os.remove(p)");
    }

    [Fact]
    public void NginxServiceConfigCommandProvider_ShouldDisableAndRollbackConflictingDefaultSiteLinks()
    {
        var provider = new NginxServiceConfigCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var commands = provider.GetCommands(profile);

        var disableCommand = commands.Single(command => command.Name == "service_config_nginx_disable_default_sites")
            .BuildCommandText(new Dictionary<string, string>());
        var rollbackCommand = commands.Single(command => command.Name == "service_config_nginx_rollback_default_sites")
            .BuildCommandText(new Dictionary<string, string>
            {
                ["disabledPathsBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("/etc/nginx/sites-enabled/default\n")),
            });
        var disableScript = DecodeEmbeddedPythonScript(disableCommand);
        var rollbackScript = DecodeEmbeddedPythonScript(rollbackCommand);

        disableScript.Should().Contain("/etc/nginx/sites-enabled");
        disableScript.Should().Contain("/etc/nginx/.kelpie-disabled-sites");
        disableScript.Should().Contain("default_server");
        disableScript.Should().Contain("os.path.islink(p)");
        disableScript.Should().Contain("os.readlink(p)");
        disableScript.Should().Contain("os.unlink(p)");
        rollbackScript.Should().Contain("base64.b64decode(sys.argv[1])");
        rollbackScript.Should().Contain("os.symlink(target,p)");
        rollbackScript.Should().Contain("os.remove(marker)");
        rollbackCommand.Should().Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("/etc/nginx/sites-enabled/default\n")));
    }

    [Theory]
    [InlineData("check_http_local")]
    [InlineData("check_tcp_connect_local")]
    public void CommonDiagnosticCommandProvider_ShouldRejectOutOfRangeLocalPort(string commandName)
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == commandName);

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["port"] = "70000",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: port");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    public void RhelNginxCommandProvider_ShouldRejectOutOfRangeLocalPort(string port)
    {
        var provider = new RhelNginxCommandProvider();
        var profile = CreateProfile("alma", "dnf");
        var command = provider.GetCommands(profile).Single(command => command.Name == "http_get_local");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["port"] = port,
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: port");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    public void DebianNginxCommandProvider_ShouldRejectOutOfRangeLocalPort(string port)
    {
        var provider = new DebianNginxCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "http_get_local");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["port"] = port,
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: port");
    }

    [Theory]
    [InlineData("1")]
    [InlineData("65535")]
    public void RhelNginxCommandProvider_ShouldAcceptValidLocalPortBoundary(string port)
    {
        var provider = new RhelNginxCommandProvider();
        var profile = CreateProfile("alma", "dnf");
        var command = provider.GetCommands(profile).Single(command => command.Name == "http_get_local");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["port"] = port,
        });

        commandText.Should().Contain($"'{port}'");
    }

    [Fact]
    public void WebPublicFileCommandProvider_ShouldProvidePermissionCommands()
    {
        var provider = new WebPublicFileCommandProvider();
        var profile = CreateProfile("debian", "apt");

        provider.Supports(profile).Should().BeTrue();

        var commands = provider.GetCommands(profile);
        commands.Select(command => command.Name).Should().Contain(
        [
            "web_public_file_list_internal",
            "web_public_file_stat_internal",
            "web_public_file_check_write_internal",
            "web_public_file_read_internal",
            "web_public_file_slice_internal",
            "web_public_file_write_internal",
            "web_public_file_write_with_permissions_internal",
            "web_change_owner_internal",
            "web_change_mode_internal",
        ]);
        commands.Single(command => command.Name == "web_public_file_write_with_permissions_internal")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "web_change_owner_internal")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "web_change_mode_internal")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void WebPublicFileCommandProvider_ShouldRenderSudoPermissionCommands()
    {
        var provider = new WebPublicFileCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "web_change_mode_internal");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["siteRootBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("/var/www/html")),
            ["pathBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("/")),
            ["mode"] = "775",
            ["recursive"] = "0",
        });

        commandText.Should().Be("sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper change-mode 'L3Zhci93d3cvaHRtbA==' 'Lw==' '775' '0'");
    }

    [Fact]
    public void DebianAptCommandProvider_ShouldSupportDebianAptProfile()
    {
        var provider = new DebianAptCommandProvider();
        var profile = CreateProfile("debian", "apt");

        provider.Supports(profile).Should().BeTrue();

        var commands = provider.GetCommands(profile);
        commands.Select(command => command.Name).Should().Contain(
        [
            "pkg_check_updates",
            "pkg_info",
            "pkg_search",
            "pkg_list_installed",
            "pkg_simulate_install",
            "pkg_install",
            "pkg_simulate_remove",
            "pkg_remove",
        ]);
    }

    [Fact]
    public void DebianAptCommandProvider_ShouldSupportUbuntuAptProfile()
    {
        var provider = new DebianAptCommandProvider();
        var profile = CreateProfile("ubuntu", "apt");

        provider.Supports(profile).Should().BeTrue();
    }

    [Theory]
    [InlineData("debian")]
    [InlineData("ubuntu")]
    public void DebianNginxCommandProvider_ShouldSupportDebianFamilyProfile(string osFamily)
    {
        var provider = new DebianNginxCommandProvider();
        var profile = CreateProfile(osFamily, "apt");

        provider.Supports(profile).Should().BeTrue();

        var commands = provider.GetCommands(profile);
        commands.Select(command => command.Name).Should().Contain(
        [
            "service_enable_now",
            "service_reload",
            "service_restart",
            "service_stop",
            "service_disable",
            "http_get_local",
        ]);
        commands.Single(command => command.Name == "service_enable_now")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "service_restart")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void DebianNginxCommandProvider_ShouldRejectRhelProfile()
    {
        var provider = new DebianNginxCommandProvider();
        var profile = CreateProfile("rhel", "dnf");

        provider.Supports(profile).Should().BeFalse();
        provider.GetCommands(profile).Should().BeEmpty();
    }

    [Fact]
    public void OsFamilyAliasResolver_ShouldResolveAlmaAsRhel()
    {
        OsFamilyAliasResolver.Resolve("alma").Should().Be("rhel");
        OsFamilyAliasResolver.Resolve("almalinux").Should().Be("rhel");
    }

    [Fact]
    public void PackageManagerResolver_ShouldUseDefaultFromOsFamily()
    {
        PackageManagerResolver.Resolve("alma", string.Empty).Should().Be("dnf");
        PackageManagerResolver.Resolve("ubuntu", null).Should().Be("apt");
        PackageManagerResolver.Resolve("debian", "custom").Should().Be("custom");
    }

    [Fact]
    public void PackageManagerResolver_ShouldRejectUnknownOsFamilyWhenPackageManagerIsMissing()
    {
        var action = () => PackageManagerResolver.Resolve("unknownos", string.Empty);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH package manager is not configured and cannot be inferred from OS family: unknownos");
    }

    [Fact]
    public void RhelDnfCommandProvider_ShouldSupportAlmaDnfProfile()
    {
        var provider = new RhelDnfCommandProvider();
        var profile = CreateProfile("alma", "dnf");

        provider.Supports(profile).Should().BeTrue();

        var commands = provider.GetCommands(profile);
        commands.Select(command => command.Name).Should().Contain(
        [
            "pkg_check_updates",
            "pkg_info",
            "pkg_search",
            "pkg_list_installed",
            "pkg_simulate_install",
            "pkg_install",
            "pkg_simulate_remove",
            "pkg_remove",
        ]);
    }

    [Fact]
    public void RhelDnfCommandProvider_ShouldRejectNonDnfProfile()
    {
        var provider = new RhelDnfCommandProvider();
        var profile = CreateProfile("alma", "apt");

        provider.Supports(profile).Should().BeFalse();
        provider.GetCommands(profile).Should().BeEmpty();
    }

    [Fact]
    public void RhelDnfCommandProvider_ShouldRenderSudoPackageInstall()
    {
        var provider = new RhelDnfCommandProvider();
        var profile = CreateProfile("alma", "dnf");
        var command = provider.GetCommands(profile).Single(command => command.Name == "pkg_install");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["package"] = "nginx",
        });

        commandText.Should().Be("sudo -n dnf install -y 'nginx'");
    }

    [Fact]
    public void RhelDnfCommandProvider_ShouldRenderLimitedPackageSearch()
    {
        var provider = new RhelDnfCommandProvider();
        var profile = CreateProfile("alma", "dnf");
        var command = provider.GetCommands(profile).Single(command => command.Name == "pkg_search");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["query"] = "nginx",
            ["limit"] = "20",
        });

        commandText.Should().Contain("dnf");
        commandText.Should().Contain("search");
        commandText.Should().Contain("query='nginx'");
        commandText.Should().Contain("limit=int('20')");
    }

    [Fact]
    public void RhelDnfCommandProvider_ShouldRenderLimitedInstalledPackageList()
    {
        var provider = new RhelDnfCommandProvider();
        var profile = CreateProfile("alma", "dnf");
        var command = provider.GetCommands(profile).Single(command => command.Name == "pkg_list_installed");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["filter"] = "nginx",
            ["limit"] = "20",
        });

        commandText.Should().Contain("dnf");
        commandText.Should().Contain("list");
        commandText.Should().Contain("installed");
        commandText.Should().Contain("filter_text='nginx'.lower()");
        commandText.Should().Contain("limit=int('20')");
    }

    [Fact]
    public void RhelNginxCommandProvider_ShouldSupportAlmaProfile()
    {
        var provider = new RhelNginxCommandProvider();
        var profile = CreateProfile("alma", "dnf");

        provider.Supports(profile).Should().BeTrue();

        var commands = provider.GetCommands(profile);
        commands.Select(command => command.Name).Should().Contain(
        [
            "service_enable_now",
            "service_reload",
            "service_restart",
            "service_stop",
            "service_disable",
            "http_get_local",
        ]);
        commands.Select(command => command.Name).Should().NotContain("service_status");
        commands.Select(command => command.Name).Should().NotContain("nginx_test_config");
        commands.Single(command => command.Name == "service_restart")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "service_stop")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands.Single(command => command.Name == "service_disable")
            .RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void DebianAptCommandProvider_ShouldRejectNonAptProfile()
    {
        var provider = new DebianAptCommandProvider();
        var profile = CreateProfile("debian", "dnf");

        provider.Supports(profile).Should().BeFalse();
        provider.GetCommands(profile).Should().BeEmpty();
    }

    [Fact]
    public void DebianAptCommandProvider_ShouldMarkInstallAndRemoveAsConfirmRequired()
    {
        var provider = new DebianAptCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var commands = provider.GetCommands(profile).ToDictionary(command => command.Name);

        commands["pkg_check_updates"].RiskLevel.Should().Be(SshCommandRiskLevel.ReadOnly);
        commands["pkg_simulate_install"].RiskLevel.Should().Be(SshCommandRiskLevel.ReadOnly);
        commands["pkg_install"].RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
        commands["pkg_simulate_remove"].RiskLevel.Should().Be(SshCommandRiskLevel.ReadOnly);
        commands["pkg_remove"].RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Theory]
    [InlineData("pkg_install", "sudo -n env DEBIAN_FRONTEND=noninteractive apt-get install -y 'nginx'")]
    [InlineData("pkg_remove", "sudo -n env DEBIAN_FRONTEND=noninteractive apt-get remove -y 'nginx'")]
    public void DebianAptCommandProvider_ShouldRenderSudoPackageMutationCommands(string commandName, string expected)
    {
        var provider = new DebianAptCommandProvider();
        var profile = CreateProfile("ubuntu", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == commandName);

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["package"] = "nginx",
        });

        commandText.Should().Be(expected);
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void DebianAptCommandProvider_ShouldRenderSafePackageCommand()
    {
        var provider = new DebianAptCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "pkg_simulate_install");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["package"] = "nginx-core",
        });

        commandText.Should().Be("apt-get -s install 'nginx-core'");
    }

    [Fact]
    public void DebianAptCommandProvider_ShouldRejectUnsafePackageName()
    {
        var provider = new DebianAptCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "pkg_install");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["package"] = "nginx*",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: package");
    }

    [Fact]
    public void DebianAptCommandProvider_ShouldRejectUnsafePackageSearchQuery()
    {
        var provider = new DebianAptCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "pkg_search");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["query"] = "nginx*",
            ["limit"] = "20",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: query");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderServiceIsActive()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "service_is_active");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["service"] = "nginx.service",
        });

        commandText.Should().Be("systemctl is-active 'nginx.service'");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderServiceIsEnabled()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "service_is_enabled");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["service"] = "nginx.service",
        });

        commandText.Should().Be("systemctl is-enabled 'nginx.service'");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderTargetInventory()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "target_inventory");

        var commandText = command.BuildCommandText();

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("base64 -d");
        commandText.Should().NotContain("python3 -c");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("run_item helper Python python3 --version");
        script.Should().Contain("run_item helper PHP php --version");
        script.Should().Contain("run_item software Node.js node --version");
        script.Should().Contain("run_item software systemctl systemctl --version");
        script.Should().Contain("run_item software journalctl journalctl --version");
        script.Should().Contain("run_item software findmnt findmnt --version");
        script.Should().Contain("run_item software ss ss --version");
        script.Should().Contain("run_item software ip ip -Version");
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ReadOnly);
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderProcessSummaryWithoutPython()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "get_process_summary");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["sortBy"] = "memory",
            ["limit"] = "20",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("ps -eo pid,ppid,user,comm,%cpu,%mem");
        commandText.Should().Contain("head -n");
        commandText.Should().Contain("'20' 'memory'");
        commandText.Should().NotContain("python3");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderListServicesWithoutPython()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "list_services");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["state"] = "running",
            ["limit"] = "20",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("systemctl list-units");
        commandText.Should().Contain("head -n");
        commandText.Should().Contain("'running' '20'");
        commandText.Should().NotContain("python3");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderCronList()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "cron_list");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["limit"] = "20",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("sh -s -- '20'");
        commandText.Should().NotContain("python3");

        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("/etc/crontab");
        script.Should().Contain("/etc/cron.d/*");
        script.Should().Contain("crontab -l");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderCronValidate()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "cron_validate");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["cronExpression"] = "*/5 * * * *",
            ["runUser"] = "deploy",
            ["command"] = "/usr/local/bin/job --once",
            ["logPath"] = "/var/log/kelpie/job.log",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("sh -s -- '*/5 * * * *' 'deploy' '/usr/local/bin/job --once' '/var/log/kelpie/job.log'");
        commandText.Should().NotContain("python3");

        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("printf 'valid=%s");
        script.Should().Contain("printf 'cronExpression=%s");
        script.Should().Contain("printf 'logPath=%s");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderCronCheckWrite()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "cron_check_write");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["targetType"] = "user",
            ["runUser"] = "deploy",
            ["cronExpression"] = "*/5 * * * *",
            ["command"] = "/usr/local/bin/job --once",
            ["logPath"] = "/var/log/kelpie/job.log",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("sh -s -- 'user' 'deploy' '*/5 * * * *' '/usr/local/bin/job --once' '/var/log/kelpie/job.log'");
        commandText.Should().NotContain("python3");

        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("getent passwd \"$run_user\"");
        script.Should().Contain("printf 'confirmation=cron_write:%s:%s");
        script.Should().Contain("printf 'rollbackSupported=true");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderCronWrite()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "cron_write");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["targetType"] = "user",
            ["runUser"] = "deploy",
            ["cronExpression"] = "*/5 * * * *",
            ["command"] = "/usr/local/bin/job --once",
            ["logPath"] = "/var/log/kelpie/job.log",
        });

        commandText.Should().StartWith("sudo -n python3");
        commandText.Should().Contain("base64.b64decode");
        commandText.Should().Contain("'user' 'deploy' '*/5 * * * *' '/usr/local/bin/job --once' '/var/log/kelpie/job.log'");
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderCronRollback()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "cron_rollback");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["targetType"] = "user",
            ["runUser"] = "deploy",
        });

        commandText.Should().StartWith("sudo -n python3");
        commandText.Should().Contain("base64.b64decode");
        commandText.Should().Contain("'user' 'deploy'");
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRejectUnsafeCronCommandText()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "cron_validate");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["cronExpression"] = "*/5 * * * *",
            ["runUser"] = "deploy",
            ["command"] = "/usr/local/bin/job; id",
            ["logPath"] = "/var/log/kelpie/job.log",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument contains a dangerous fragment: command");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderCertificateInspect()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "cert_inspect");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["path"] = "/etc/letsencrypt/live/example.invalid/fullchain.pem",
        });

        commandText.Should().Be("openssl x509 -in '/etc/letsencrypt/live/example.invalid/fullchain.pem' -noout -issuer -subject -dates -ext subjectAltName");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRejectCertificatePathOutsideApprovedDirectories()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "cert_inspect");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["path"] = "/home/deploy/fullchain.pem",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: path");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderCertificateExpiryCheck()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "cert_expiry_check");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["path"] = "/etc/pki/tls/certs/example.crt",
            ["days"] = "30",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("-- '/etc/pki/tls/certs/example.crt' '30'");
        commandText.Should().NotContain("python3");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("command -v openssl");
        script.Should().Contain("openssl x509 -in \"$path\" -noout -checkend \"$seconds\" -enddate");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserList()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_list");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["limit"] = "50",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("'50'");
        commandText.Should().NotContain("python3");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("getent passwd");
        script.Should().Contain("NR <= limit");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserInfo()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_info");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["user"] = "deploy",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("'deploy'");
        commandText.Should().NotContain("python3");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("getent passwd \"$user\"");
        script.Should().Contain("supplementaryGroups=%s");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderGroupList()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "group_list");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["limit"] = "50",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("'50'");
        commandText.Should().NotContain("python3");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("getent group");
        script.Should().Contain("NR <= limit");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderGroupInfo()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "group_info");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["group"] = "wheel",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("'wheel'");
        commandText.Should().NotContain("python3");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("getent group \"$group\"");
        script.Should().Contain("members=%s");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderSudoersCheck()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "sudoers_check");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["targetType"] = "user",
            ["name"] = "deploy",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("sh -s -- 'user' 'deploy'");
        commandText.Should().NotContain("python3");

        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("printf 'sudoersMatches=%s");
        script.Should().Contain("getent passwd \"$name\"");
        script.Should().Contain("/etc/sudoers");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserCheckGroupChange()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_check_group_change");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["user"] = "deploy",
            ["groups"] = "nginx,wheel",
            ["mode"] = "append",
        });

        commandText.Should().Contain("user='deploy'");
        commandText.Should().Contain("groups='nginx,wheel'");
        commandText.Should().Contain("mode='append'");
        commandText.Should().Contain("confirmation=user_apply_group_change:");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserApplyGroupChange()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_apply_group_change");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["user"] = "deploy",
            ["groups"] = "nginx,wheel",
            ["mode"] = "append",
        });

        commandText.Should().StartWith("sudo -n python3");
        commandText.Should().Contain("base64.b64decode");
        commandText.Should().Contain("'deploy' 'nginx,wheel' 'append'");
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserRollbackGroupChange()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_rollback_group_change");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["user"] = "deploy",
        });

        commandText.Should().StartWith("sudo -n python3");
        commandText.Should().Contain("base64.b64decode");
        commandText.Should().Contain("'deploy'");
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRejectUnsafeUserCheckGroupChange()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_check_group_change");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["user"] = "deploy",
            ["groups"] = "nginx,/root",
            ["mode"] = "append",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: groups");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserCheckPermissionChange()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_check_permission_change");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["user"] = "deploy",
            ["shell"] = "/bin/bash",
            ["login"] = "unchanged",
            ["sudo"] = "present",
        });

        commandText.Should().Contain("user='deploy'");
        commandText.Should().Contain("shell='/bin/bash'");
        commandText.Should().Contain("sudo='present'");
        commandText.Should().Contain("confirmation=user_apply_permission_change:");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserApplyPermissionChange()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_apply_permission_change");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["user"] = "deploy",
            ["shell"] = "/bin/bash",
            ["login"] = "disabled",
            ["sudo"] = "absent",
        });

        commandText.Should().StartWith("sudo -n python3");
        commandText.Should().Contain("base64.b64decode");
        commandText.Should().Contain("'deploy' '/bin/bash' 'disabled' 'absent'");
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserRollbackPermissionChange()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_rollback_permission_change");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["user"] = "deploy",
        });

        commandText.Should().StartWith("sudo -n python3");
        commandText.Should().Contain("base64.b64decode");
        commandText.Should().Contain("'deploy'");
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRejectUnsafeUserApplyPermissionShell()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_apply_permission_change");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["user"] = "deploy",
            ["shell"] = "/tmp/shell",
            ["login"] = "disabled",
            ["sudo"] = "absent",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: shell");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRejectUnsafeSudoersTargetName()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "sudoers_check");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["targetType"] = "user",
            ["name"] = "deploy;id",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument contains a dangerous fragment: name");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserUsageCheck()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_usage_check");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["targetType"] = "user",
            ["name"] = "deploy",
            ["limit"] = "20",
        });

        commandText.Should().Contain("base64.b64decode");
        commandText.Should().Contain("'user' 'deploy' '20'");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserFileOwnershipCheck()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_file_ownership_check");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["targetType"] = "group",
            ["name"] = "www-data",
            ["scanRoot"] = "/var/www",
            ["depth"] = "2",
            ["limit"] = "20",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("sh -s -- 'group' 'www-data' '/var/www' '2' '20'");
        commandText.Should().NotContain("python3");

        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("find \"$root\"");
        script.Should().Contain("stat -c '%u:%g'");
        script.Should().Contain("printf 'entriesScanned=%s");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRejectFileOwnershipPathOutsideApprovedRoots()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_file_ownership_check");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["targetType"] = "user",
            ["name"] = "deploy",
            ["scanRoot"] = "/root",
            ["depth"] = "2",
            ["limit"] = "20",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: scanRoot");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderUserServiceUsageCheck()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "user_service_usage_check");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["targetType"] = "group",
            ["name"] = "www-data",
            ["limit"] = "20",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("sh -s -- 'group' 'www-data' '20'");
        commandText.Should().NotContain("python3");

        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("systemctl list-units");
        script.Should().Contain("systemctl show \"$unit\"");
        script.Should().Contain("printf 'matches=%s");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderServiceResidualConfigCheck()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "service_residual_config_check");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["service"] = "nginx.service",
            ["limit"] = "20",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("-- 'nginx.service' '20'");
        commandText.Should().NotContain("python3");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("base=${base%\".service\"}");
        script.Should().Contain("pathsChecked=%s");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderSupportReportCollect()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "support_report_collect");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["limit"] = "20",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("sh -s -- '20'");
        commandText.Should().NotContain("python3");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("reportVersion=1");
        script.Should().Contain("uname -srm");
        script.Should().Contain("systemctl --failed");
        commandText.Should().NotContain("hostname");
        commandText.Should().NotContain("ip addr");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderFirewallStatus()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "firewall_status");

        var commandText = command.BuildCommandText();

        commandText.Should().StartWith("sh -c");
        commandText.Should().NotContain("python3");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("firewalldAvailable=%s");
        script.Should().Contain("ufwAvailable=%s");
        script.Should().Contain("firewalldServiceCount=%s");
        commandText.Should().NotContain("--list-all");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderFirewallCheckRule()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "firewall_check_rule");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["action"] = "add",
            ["target"] = "port",
            ["value"] = "443/tcp",
            ["zone"] = "public",
            ["permanent"] = "false",
        });

        commandText.Should().Contain("confirmation=firewall_apply_rule:");
        commandText.Should().Contain("value='443/tcp'");
        commandText.Should().Contain("zone='public'");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderFirewallApplyRule()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "firewall_apply_rule");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["action"] = "remove",
            ["target"] = "service",
            ["value"] = "https",
            ["zone"] = "public",
            ["permanent"] = "true",
        });

        commandText.Should().StartWith("sudo -n python3");
        commandText.Should().Contain("'remove' 'service' 'https' 'public' 'true'");
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Theory]
    [InlineData("service_enable_now", "sudo -n systemctl enable --now 'nginx.service'")]
    [InlineData("service_reload", "sudo -n systemctl reload 'nginx.service'")]
    [InlineData("service_restart", "sudo -n systemctl restart 'nginx.service'")]
    [InlineData("service_stop", "sudo -n systemctl stop 'nginx.service'")]
    [InlineData("service_disable", "sudo -n systemctl disable 'nginx.service'")]
    public void DebianNginxCommandProvider_ShouldRenderServiceMaintenanceCommands(string commandName, string expected)
    {
        var provider = new DebianNginxCommandProvider();
        var profile = CreateProfile("ubuntu", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == commandName);

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["service"] = "nginx.service",
        });

        commandText.Should().Be(expected);
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRejectUnsafeFirewallRuleValue()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "firewall_check_rule");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["action"] = "add",
            ["target"] = "port",
            ["value"] = "443/sctp",
            ["zone"] = "public",
            ["permanent"] = "false",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: value");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderBackupPlanCheck()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "backup_plan_check");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["scanRoot"] = "/var/www",
            ["depth"] = "2",
            ["limit"] = "20",
        });

        commandText.Should().Contain("entriesScanned=");
        commandText.Should().Contain("confirmation=backup_run:");
        commandText.Should().Contain("'/var/www' '2' '20'");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderBackupRun()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "backup_run");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["scanRoot"] = "/var/www",
            ["depth"] = "2",
            ["limit"] = "20",
        });

        commandText.Should().StartWith("sudo -n python3");
        commandText.Should().Contain("backupCreated=true");
        commandText.Should().Contain("'/var/www' '2' '20'");
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRejectBackupPlanOutsideApprovedRoots()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "backup_plan_check");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["scanRoot"] = "/root",
            ["depth"] = "2",
            ["limit"] = "20",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: scanRoot");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderBackupVerify()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "backup_verify");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["backupPath"] = "/var/backups/kelpie/site/full.tar.gz",
        });

        commandText.Should().StartWith("sh -c");
        commandText.Should().Contain("sh -s -- '/var/backups/kelpie/site/full.tar.gz'");
        commandText.Should().NotContain("python3");
        var script = DecodeEmbeddedShellScript(commandText);
        script.Should().Contain("tar -tf \"$path\"");
        script.Should().Contain("archiveReadable=%s");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderAuditVerify()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "audit_verify");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["logPath"] = "/var/log/kelpie/audit.log",
            ["limit"] = "50",
        });

        commandText.Should().Contain("chainBreaks=");
        commandText.Should().Contain("path='/var/log/kelpie/audit.log'");
        commandText.Should().Contain("limit=int('50')");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRenderAuditExport()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "audit_export");

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["logPath"] = "/var/log/kelpie/audit.log",
            ["limit"] = "50",
        });

        commandText.Should().Contain("exportVersion=1");
        commandText.Should().Contain("allowed=['timestamp'");
        commandText.Should().NotContain("password");
        commandText.Should().NotContain("PrivateKey");
    }

    [Fact]
    public void CommonDiagnosticCommandProvider_ShouldRejectAuditPathOutsideKelpieLog()
    {
        var provider = new CommonDiagnosticCommandProvider();
        var profile = CreateProfile("debian", "apt");
        var command = provider.GetCommands(profile).Single(command => command.Name == "audit_verify");

        var action = () => command.BuildCommandText(new Dictionary<string, string>
        {
            ["logPath"] = "/var/log/auth.log",
            ["limit"] = "50",
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH command argument format is invalid: logPath");
    }

    [Theory]
    [InlineData("service_restart", "sudo -n systemctl restart 'nginx.service'")]
    [InlineData("service_stop", "sudo -n systemctl stop 'nginx.service'")]
    [InlineData("service_disable", "sudo -n systemctl disable 'nginx.service'")]
    public void RhelNginxCommandProvider_ShouldRenderServiceMaintenanceCommands(string commandName, string expected)
    {
        var provider = new RhelNginxCommandProvider();
        var profile = CreateProfile("alma", "dnf");
        var command = provider.GetCommands(profile).Single(command => command.Name == commandName);

        var commandText = command.BuildCommandText(new Dictionary<string, string>
        {
            ["service"] = "nginx.service",
        });

        commandText.Should().Be(expected);
        command.RiskLevel.Should().Be(SshCommandRiskLevel.ConfirmRequired);
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

    private static string DecodeEmbeddedPythonScript(string commandText)
    {
        var scriptBase64 = Regex.Match(commandText, "b64decode\\('(?<script>[^']+)'\\)")
            .Groups["script"]
            .Value;
        return Encoding.UTF8.GetString(Convert.FromBase64String(scriptBase64));
    }

    private static string DecodeEmbeddedShellScript(string commandText)
    {
        var scriptBase64 = Regex.Match(commandText, "printf %s '(?<script>[^']+)' \\| base64 -d")
            .Groups["script"]
            .Value;
        return Encoding.UTF8.GetString(Convert.FromBase64String(scriptBase64));
    }
}
