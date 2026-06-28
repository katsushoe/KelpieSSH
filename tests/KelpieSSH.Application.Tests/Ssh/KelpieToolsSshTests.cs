using FluentAssertions;
using KelpieMCPServer;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class KelpieToolsSshTests
{
    [Fact]
    public void ReloadProfiles_ShouldReloadProfileCatalog()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("deploy"));
        var profiles = new ReloadingSshConnectionProfileCatalog(directory);
        File.WriteAllText(Path.Combine(directory, "vps02.json"), CreateProfileJson("ops"));

        var result = KelpieTools.ReloadProfiles(profiles);

        result.Success.Should().BeTrue();
        result.ProfileCount.Should().Be(2);
        result.ProfileNames.Should().Equal("vps01", "vps02");
        profiles.TryGet("vps02", out var profile).Should().BeTrue();
        profile.UserName.Should().Be("ops");
    }

    [Fact]
    public async Task ReloadProfiles_ShouldExposeUpdatedEnvironmentPolicyToEnvironmentTools()
    {
        var directory = CreateTempDirectory();
        var profilePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(profilePath, CreateProfileJson("deploy"));
        var profiles = new ReloadingSshConnectionProfileCatalog(directory);
        File.WriteAllText(profilePath, CreateProfileJsonWithEnvironmentPolicy("deploy"));
        var runner = new FakeSshCommandRunner(
        [
            new FakeSshCommandOutput($"APP_ENV{Environment.NewLine}SECRET_TOKEN{Environment.NewLine}", string.Empty),
            new FakeSshCommandOutput("production\n", string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);

        var reloadResult = KelpieTools.ReloadProfiles(profiles);
        var keysResult = await KelpieTools.GetEnvironmentKeysAsync(service, profiles, "vps01");
        var setResult = await KelpieTools.SetEnvironmentValueAsync(
            service,
            profiles,
            "vps01",
            "APP_ENV",
            "production",
            "uname -a");

        reloadResult.Success.Should().BeTrue();
        reloadResult.ProfileNames.Should().Equal("vps01");
        keysResult.StandardOutput.Should().Be($"APP_ENV{Environment.NewLine}");
        setResult.CommandText.Should().Be("env APP_ENV=(hidden) uname -a");
        setResult.CommandText.Should().NotContain("production");
        runner.Requests.Should().HaveCount(2);
        runner.Requests[0].Profile.EnvironmentValues.Should().Contain(rule =>
            rule.Key == "SECRET_TOKEN" && rule.IsHidden);
        runner.Requests[1].CommandText.Should().Be("if [ -f ~/.kelpie/.env ]; then . ~/.kelpie/.env; fi; env APP_ENV='production' uname -a");
    }

    [Fact]
    public async Task CloseSshConnectionAsync_ShouldReturnNotFoundForMissingHandle()
    {
        var manager = new SshTerminalSessionManager(
            new SshConnectionProfileCatalog([]),
            new InMemorySshPasswordSessionStore());

        var result = await KelpieTools.CloseSshConnectionAsync(manager, "term-missing");

        result.Handle.Should().Be("term-missing");
        result.Closed.Should().BeFalse();
        result.Error.Should().Be("session-not-found");
    }

    [Fact]
    public async Task LogoutSshProfile_ShouldClearPasswordSession()
    {
        var profile = CreatePasswordProfile("vps01", "kelpie:vps01");
        var profiles = new SshConnectionProfileCatalog([profile]);
        var store = new InMemorySshPasswordSessionStore();
        store.SetPasswordSession("vps01", "kelpie:vps01", "secret");

        var result = KelpieTools.LogoutSshProfile(profiles, store, "vps01");

        result.ProfileName.Should().Be("vps01");
        result.LoggedOut.Should().BeTrue();
        result.Error.Should().BeEmpty();
        var password = await store.GetPasswordAsync("kelpie:vps01");
        password.Should().BeNull();
    }

    [Fact]
    public void LogoutSshProfile_ShouldReportMissingPasswordSecret()
    {
        var profile = CreateProfile("vps01");
        var profiles = new SshConnectionProfileCatalog([profile]);
        var store = new InMemorySshPasswordSessionStore();

        var result = KelpieTools.LogoutSshProfile(profiles, store, "vps01");

        result.ProfileName.Should().Be("vps01");
        result.LoggedOut.Should().BeFalse();
        result.Error.Should().Be("SSH password secret name is not configured.");
    }

    [Fact]
    public async Task GetSshSystemInfoAsync_ShouldUseNamedProfile()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshSystemInfoAsync(service, profiles, "vps01");

        result.ProfileName.Should().Be("vps01");
        result.CommandName.Should().Be("get_system_info");
        result.CommandText.Should().Be("uname -a");
        result.StandardOutput.Should().Contain("\u001b[31mERROR\u001b[0m");
        result.Stdout.Should().Contain("\u001b[31mERROR\u001b[0m");
        result.StdoutPlain.Should().Contain("ERROR");
        result.Stderr.Should().Contain("warning");
        result.StderrPlain.Should().Contain("warning");
        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.Profile.Should().BeEquivalentTo(profile);
    }

    [Fact]
    public async Task RunRemoteOperationAsync_ShouldRunWithoutProfileCatalog()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var operation = new SshRemoteOperation(
            new SshRemoteEndpoint("example.invalid"),
            new SshRemoteCredential(
                "deploy",
                "private_key",
                PrivateKeyPath: "id_ed25519"),
            new SshRemotePolicy("safe"),
            new SshRemoteOperationSpec(
                "managed",
                "service_status",
                new Dictionary<string, string>
                {
                    ["service"] = "nginx",
                }),
            new SshRemoteOperationOptions(30, "op-example"),
            new SshRemoteTarget("debian", "apt"));

        var result = await KelpieTools.RunRemoteOperationAsync(service, operation);

        result.CorrelationId.Should().Be("op-example");
        result.CommandName.Should().Be("service_status");
        result.CommandText.Should().Be("systemctl status 'nginx' --no-pager");
        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.Profile.Name.Should().Be("op-example");
    }

    [Fact]
    public async Task RunAllowedSshCommandAsync_ShouldUseNamedProfile()
    {
        var profile = CreateProfile("vps02");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunAllowedSshCommandAsync(
            service,
            profiles,
            "get_disk_usage",
            "vps02",
            arguments: null);

        result.ProfileName.Should().Be("vps02");
        result.CommandName.Should().Be("get_disk_usage");
        result.CommandText.Should().Be("df -h");
        result.Ok.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.ErrorInfo.Should().BeNull();
        result.Meta.SchemaVersion.Should().Be("1");
        result.Meta.ProfileName.Should().Be("vps02");
    }

    [Theory]
    [InlineData("cron_write")]
    [InlineData("cron_rollback")]
    [InlineData("user_apply_group_change")]
    [InlineData("user_rollback_group_change")]
    [InlineData("user_apply_permission_change")]
    [InlineData("user_rollback_permission_change")]
    [InlineData("firewall_apply_rule")]
    [InlineData("backup_run")]
    public async Task RunAllowedSshCommandAsync_ShouldRejectDedicatedConfirmationCommand(string commandName)
    {
        var profile = CreateProfile("vps02", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunAllowedSshCommandAsync(
            service,
            profiles,
            commandName,
            "vps02",
            arguments: null);

        result.CommandName.Should().Be(commandName);
        result.ExitCode.Should().Be(-1);
        result.Ok.Should().BeFalse();
        result.Error.Should().Be("Confirmation-required maintenance commands must be called through their dedicated MCP tools.");
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be("KELPIE_POLICY_COMMAND_DENIED");
        result.ErrorInfo.Category.Should().Be("PolicyDenied");
        result.Data.Should().BeNull();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task GetSshOsReleaseAsync_ShouldRunProviderCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshOsReleaseAsync(service, profiles, "vps01");

        result.CommandName.Should().Be("get_os_release");
        result.CommandText.Should().Be("cat /etc/os-release");
        runner.LastRequest!.CommandName.Should().Be("get_os_release");
    }

    [Fact]
    public async Task GetSshCapabilitiesAsync_ShouldReturnProfileCommandAndToolCapabilities()
    {
        var profile = CreateProfile("vps01", osFamily: "alma", packageManager: "dnf");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshCapabilitiesAsync(service, profiles, "vps01");

        result.ProfileName.Should().Be("vps01");
        result.OsFamily.Should().Be("alma");
        result.PackageManager.Should().Be("dnf");
        result.ProbeSucceeded.Should().BeTrue();
        result.ProbeCommandName.Should().Be("get_os_release");
        result.Commands.Should().Contain(command => command.CommandName == "pkg_search");
        result.Commands.Should().Contain(command => command.CommandName == "target_inventory");
        result.Tools.Should().Contain(tool => tool.ToolName == "get_target_inventory" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_pkg_search" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_service_is_enabled" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_cron_list" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_cron_check_write" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_cron_write" && tool.Available && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_cron_rollback" && tool.Available && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_cert_inspect" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_list" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_info" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_group_list" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_group_info" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_sudoers_check" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_usage_check" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_check_group_change" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_apply_group_change" && tool.Available && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_rollback_group_change" && tool.Available && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_check_permission_change" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_apply_permission_change" && tool.Available && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_rollback_permission_change" && tool.Available && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_file_ownership_check" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_user_service_usage_check" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_service_residual_config_check" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_support_report_collect" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_firewall_status" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_firewall_check_rule" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_firewall_apply_rule" && tool.Available && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_backup_plan_check" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_backup_run" && tool.Available && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_backup_verify" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_audit_verify" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_audit_export" && tool.Available);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_service_restart" && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_service_stop" && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_service_disable" && tool.RequiresConfirmation);
        result.Tools.Should().Contain(tool => tool.ToolName == "ssh_pkg_install" && tool.RequiresConfirmation);
        runner.LastRequest!.CommandName.Should().Be("get_os_release");
    }

    [Fact]
    public async Task GetSshCapabilitiesAsync_ShouldReturnEnvironmentToolCapabilities()
    {
        var profile = CreateProfile(
            "vps01",
            capabilities: PolicySet.FromNames(
            [
                KelpiePolicyNames.AllowPeekEnvironmentKeys,
                KelpiePolicyNames.AllowPeekEnvironmentValues,
            ]));
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshCapabilitiesAsync(service, profiles, "vps01");

        result.Tools.Should().Contain(tool =>
            tool.ToolName == "get_environment_keys"
            && tool.Available
            && tool.RiskLevel == nameof(SshCommandRiskLevel.ReadOnly));
        result.Tools.Should().Contain(tool =>
            tool.ToolName == "peek_environment_value"
            && tool.Available
            && tool.RiskLevel == nameof(SshCommandRiskLevel.ReadOnly));
        result.Tools.Should().Contain(tool =>
            tool.ToolName == "set_environment_value"
            && !tool.Available
            && tool.UnavailableReason == "AllowSetEnvironmentValues is not enabled for this profile.");
    }

    [Fact]
    public async Task GetEnvironmentKeysAsync_ShouldUseNamedProfile()
    {
        var profile = CreateProfile(
            "vps01",
            capabilities: PolicySet.FromNames([KelpiePolicyNames.AllowPeekEnvironmentKeys]),
            environmentValues:
            [
                new EnvironmentValueRule("MY_SECRET_KEY", EnvironmentValueAccess.Hidden),
            ]);
        var runner = new FakeSshCommandRunner("PATH\nMY_SECRET_KEY\nLANG\n", string.Empty);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetEnvironmentKeysAsync(service, profiles, "vps01");

        result.ProfileName.Should().Be("vps01");
        result.CommandName.Should().Be("get_environment_keys");
        result.StandardOutput.Should().Be($"PATH{Environment.NewLine}LANG{Environment.NewLine}");
        runner.LastRequest!.Profile.Should().BeSameAs(profile);
    }

    [Fact]
    public async Task PeekEnvironmentValueAsync_ShouldUseNamedProfile()
    {
        var profile = CreateProfile(
            "vps01",
            capabilities: PolicySet.FromNames([KelpiePolicyNames.AllowPeekEnvironmentValues]),
            environmentValues:
            [
                new EnvironmentValueRule("APP_ENV", EnvironmentValueAccess.PeekCommon),
            ]);
        var runner = new FakeSshCommandRunner("production\n", string.Empty);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.PeekEnvironmentValueAsync(service, profiles, "vps01", "APP_ENV");

        result.CommandName.Should().Be("peek_environment_value");
        result.CommandText.Should().Be("printenv 'APP_ENV'");
        result.StandardOutput.Should().Be("production\n");
    }

    [Fact]
    public async Task PeekEnvironmentValueAsync_ShouldReturnPolicyErrorResult()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.PeekEnvironmentValueAsync(service, profiles, "vps01", "APP_ENV");

        result.CommandName.Should().Be("peek_environment_value");
        result.Ok.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("KelpiePolicyError: AllowPeekEnvironmentValues is required.");
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be("KELPIE_POLICY_COMMAND_DENIED");
        result.ErrorInfo.Category.Should().Be("PolicyDenied");
        result.Data.Should().BeNull();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task SetEnvironmentValueAsync_ShouldMaskValueInMcpResult()
    {
        var profile = CreateProfile(
            "vps01",
            capabilities: PolicySet.FromNames([KelpiePolicyNames.AllowSetEnvironmentValues]),
            environmentValues:
            [
                new EnvironmentValueRule("APP_ENV", EnvironmentValueAccess.SetCommon),
            ]);
        var runner = new FakeSshCommandRunner("production\n", string.Empty);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.SetEnvironmentValueAsync(
            service,
            profiles,
            "vps01",
            "APP_ENV",
            "production",
            "uname -a");

        result.CommandName.Should().Be("set_environment_value");
        result.CommandText.Should().Be("env APP_ENV=(hidden) uname -a");
        result.CommandText.Should().NotContain("production");
        runner.LastRequest!.CommandText.Should().Be("if [ -f ~/.kelpie/.env ]; then . ~/.kelpie/.env; fi; env APP_ENV='production' uname -a");
    }

    [Fact]
    public async Task GetTargetInventoryAsync_ShouldReturnStructuredInventory()
    {
        var profile = CreateProfile("vps02", osFamily: "ubuntu", packageManager: "apt");
        var runner = new FakeSshCommandRunner(
            "OS\tUbuntu\t24.04\tubuntu\n" +
            "ITEM\thelper\tPython\tpython3\t0\tPython 3.12.3\n" +
            "ITEM\thelper\tPHP\tphp\t127\tcommand not found\n" +
            "ITEM\tsoftware\tnginx\tnginx\t0\tnginx version: nginx/1.24.0\n" +
            "ITEM\tsoftware\tsystemctl\tsystemctl\t0\tsystemd 255 (255.4-1ubuntu8)\n",
            string.Empty);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetTargetInventoryAsync(service, profiles, "vps02");

        result.Profile.Should().Be("vps02");
        result.Os.Family.Should().Be("ubuntu");
        result.Os.Name.Should().Be("Ubuntu");
        result.Os.Version.Should().Be("24.04");
        result.Os.PackageManager.Should().Be("apt");
        result.Helpers.Should().ContainEquivalentOf(new KelpieTools.TargetInventoryItem(
            "Python",
            "python3",
            "Available",
            "3.12.3",
            "Python 3.12.3",
            0));
        result.Helpers.Should().Contain(item =>
            item.Name == "PHP" &&
            item.Status == "Not Available" &&
            item.Version == string.Empty &&
            item.ExitCode == 127);
        result.Software.Should().Contain(item =>
            item.Name == "nginx" &&
            item.Status == "Available" &&
            item.Version == "1.24.0");
        result.Software.Should().Contain(item =>
            item.Name == "systemctl" &&
            item.Version == "255");
        runner.LastRequest!.CommandName.Should().Be("target_inventory");
    }

    [Fact]
    public async Task GetTargetInventoryAsync_ShouldFailWhenOsProbeFails()
    {
        var profile = CreateProfile("vps02");
        var runner = new FakeSshCommandRunner(
            [
                new FakeSshCommandOutput("ERROR\tos-release not readable\n", string.Empty, 2),
            ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var action = () => KelpieTools.GetTargetInventoryAsync(service, profiles, "vps02");

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Target inventory probe failed: ERROR*");
        runner.LastRequest!.CommandName.Should().Be("target_inventory");
    }

    [Fact]
    public async Task GetSshUptimeAsync_ShouldRunProviderCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshUptimeAsync(service, profiles, "vps01");

        result.CommandName.Should().Be("get_uptime");
        result.CommandText.Should().Be("uptime");
        runner.LastRequest!.CommandName.Should().Be("get_uptime");
    }

    [Fact]
    public async Task GetSshProcessSummaryAsync_ShouldPassSortAndLimitArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshProcessSummaryAsync(
            service,
            profiles,
            "vps01",
            "memory",
            "15");

        result.CommandName.Should().Be("get_process_summary");
        result.CommandText.Should().Contain("ps");
        result.CommandText.Should().Contain("sort_by=\"$2\"");
        result.CommandText.Should().Contain("'15' 'memory'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["sortBy"].Should().Be("memory");
        runner.LastRequest.Arguments["limit"].Should().Be("15");
    }

    [Fact]
    public async Task GetSshInodeUsageAsync_ShouldRunProviderCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshInodeUsageAsync(service, profiles, "vps01");

        result.CommandName.Should().Be("get_inode_usage");
        result.CommandText.Should().Be("df -ih");
        runner.LastRequest!.CommandName.Should().Be("get_inode_usage");
    }

    [Fact]
    public async Task GetSshMountsAsync_ShouldRunProviderCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshMountsAsync(service, profiles, "vps01");

        result.CommandName.Should().Be("get_mounts");
        result.CommandText.Should().Be("findmnt -rno TARGET,SOURCE,FSTYPE,OPTIONS");
        runner.LastRequest!.CommandName.Should().Be("get_mounts");
    }

    [Fact]
    public async Task GetSshNetworkAddressesAsync_ShouldRunProviderCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshNetworkAddressesAsync(service, profiles, "vps01");

        result.CommandName.Should().Be("get_network_addresses");
        result.CommandText.Should().Be("ip addr show");
        runner.LastRequest!.CommandName.Should().Be("get_network_addresses");
    }

    [Fact]
    public async Task GetSshRoutesAsync_ShouldRunProviderCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshRoutesAsync(service, profiles, "vps01");

        result.CommandName.Should().Be("get_routes");
        result.CommandText.Should().Be("ip route show");
        runner.LastRequest!.CommandName.Should().Be("get_routes");
    }

    [Fact]
    public async Task GetSshDnsConfigAsync_ShouldRunProviderCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshDnsConfigAsync(service, profiles, "vps01");

        result.CommandName.Should().Be("get_dns_config");
        result.CommandText.Should().Be("cat /etc/resolv.conf");
        runner.LastRequest!.CommandName.Should().Be("get_dns_config");
    }

    [Fact]
    public async Task CheckSshHttpLocalAsync_ShouldPassPortArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshHttpLocalAsync(service, profiles, "vps01", "8080");

        result.CommandName.Should().Be("check_http_local");
        result.CommandText.Should().Contain("urllib.request");
        result.CommandText.Should().Contain("port=int('8080')");
        runner.LastRequest!.Arguments["port"].Should().Be("8080");
    }

    [Fact]
    public async Task CheckSshTcpConnectLocalAsync_ShouldPassPortArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshTcpConnectLocalAsync(service, profiles, "vps01", "22");

        result.CommandName.Should().Be("check_tcp_connect_local");
        result.CommandText.Should().Contain("socket.create_connection");
        result.CommandText.Should().Contain("port=int('22')");
        runner.LastRequest!.Arguments["port"].Should().Be("22");
    }

    [Fact]
    public async Task ListSshCronAsync_ShouldPassLimitArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ListSshCronAsync(
            service,
            profiles,
            "vps01",
            "20");

        result.CommandName.Should().Be("cron_list");
        result.CommandText.Should().Contain("sh -s -- '20'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task ValidateSshCronAsync_ShouldPassSafeArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ValidateSshCronAsync(
            service,
            profiles,
            "vps01",
            "*/5 * * * *",
            "deploy",
            "/usr/local/bin/job --once",
            "/var/log/kelpie/job.log");

        result.CommandName.Should().Be("cron_validate");
        result.CommandText.Should().Contain("sh -s -- '*/5 * * * *' 'deploy' '/usr/local/bin/job --once' '/var/log/kelpie/job.log'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["cronExpression"].Should().Be("*/5 * * * *");
        runner.LastRequest.Arguments["runUser"].Should().Be("deploy");
        runner.LastRequest.Arguments["command"].Should().Be("/usr/local/bin/job --once");
        runner.LastRequest.Arguments["logPath"].Should().Be("/var/log/kelpie/job.log");
    }

    [Fact]
    public async Task InspectSshCertificateAsync_ShouldPassCertificatePath()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.InspectSshCertificateAsync(
            service,
            profiles,
            "vps01",
            "/etc/letsencrypt/live/example.invalid/fullchain.pem");

        result.CommandName.Should().Be("cert_inspect");
        result.CommandText.Should().Contain("openssl x509");
        runner.LastRequest!.Arguments["path"].Should().Be("/etc/letsencrypt/live/example.invalid/fullchain.pem");
    }

    [Fact]
    public async Task CheckSshCertificateExpiryAsync_ShouldPassPathAndDays()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshCertificateExpiryAsync(
            service,
            profiles,
            "vps01",
            "/etc/pki/tls/certs/example.crt",
            "14");

        result.CommandName.Should().Be("cert_expiry_check");
        result.CommandText.Should().Contain("-- '/etc/pki/tls/certs/example.crt' '14'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["path"].Should().Be("/etc/pki/tls/certs/example.crt");
        runner.LastRequest.Arguments["days"].Should().Be("14");
    }

    [Fact]
    public async Task ListSshUsersAsync_ShouldPassLimitArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ListSshUsersAsync(
            service,
            profiles,
            "vps01",
            "50");

        result.CommandName.Should().Be("user_list");
        result.CommandText.Should().Contain("sh -s -- '50'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["limit"].Should().Be("50");
    }

    [Fact]
    public async Task GetSshUserInfoAsync_ShouldPassUserArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshUserInfoAsync(
            service,
            profiles,
            "vps01",
            "deploy");

        result.CommandName.Should().Be("user_info");
        result.CommandText.Should().Contain("sh -s -- 'deploy'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["user"].Should().Be("deploy");
    }

    [Fact]
    public async Task ListSshGroupsAsync_ShouldPassLimitArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ListSshGroupsAsync(
            service,
            profiles,
            "vps01",
            "50");

        result.CommandName.Should().Be("group_list");
        result.CommandText.Should().Contain("sh -s -- '50'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["limit"].Should().Be("50");
    }

    [Fact]
    public async Task GetSshGroupInfoAsync_ShouldPassGroupArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshGroupInfoAsync(
            service,
            profiles,
            "vps01",
            "wheel");

        result.CommandName.Should().Be("group_info");
        result.CommandText.Should().Contain("sh -s -- 'wheel'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["group"].Should().Be("wheel");
    }

    [Fact]
    public async Task CheckSshSudoersAsync_ShouldPassPrincipalArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshSudoersAsync(
            service,
            profiles,
            "vps01",
            "user",
            "deploy");

        result.CommandName.Should().Be("sudoers_check");
        result.CommandText.Should().Contain("kind='user'");
        result.CommandText.Should().Contain("name='deploy'");
        runner.LastRequest!.Arguments["targetType"].Should().Be("user");
        runner.LastRequest.Arguments["name"].Should().Be("deploy");
    }

    [Fact]
    public async Task CheckSshUserUsageAsync_ShouldPassPrincipalAndLimitArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshUserUsageAsync(
            service,
            profiles,
            "vps01",
            "user",
            "deploy",
            "20");

        result.CommandName.Should().Be("user_usage_check");
        runner.LastRequest!.Arguments["targetType"].Should().Be("user");
        runner.LastRequest.Arguments["name"].Should().Be("deploy");
        runner.LastRequest.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task CheckSshUserFileOwnershipAsync_ShouldPassScanArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshUserFileOwnershipAsync(
            service,
            profiles,
            "vps01",
            "group",
            "www-data",
            "/var/www",
            "2",
            "20");

        result.CommandName.Should().Be("user_file_ownership_check");
        runner.LastRequest!.Arguments["targetType"].Should().Be("group");
        runner.LastRequest.Arguments["name"].Should().Be("www-data");
        runner.LastRequest.Arguments["scanRoot"].Should().Be("/var/www");
        runner.LastRequest.Arguments["depth"].Should().Be("2");
        runner.LastRequest.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task CheckSshUserServiceUsageAsync_ShouldPassPrincipalAndLimitArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshUserServiceUsageAsync(
            service,
            profiles,
            "vps01",
            "group",
            "www-data",
            "20");

        result.CommandName.Should().Be("user_service_usage_check");
        runner.LastRequest!.Arguments["targetType"].Should().Be("group");
        runner.LastRequest.Arguments["name"].Should().Be("www-data");
        runner.LastRequest.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task CheckSshServiceResidualConfigAsync_ShouldPassServiceAndLimitArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshServiceResidualConfigAsync(
            service,
            profiles,
            "vps01",
            "nginx.service",
            "20");

        result.CommandName.Should().Be("service_residual_config_check");
        result.CommandText.Should().Contain("sh -s -- 'nginx.service' '20'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["service"].Should().Be("nginx.service");
        runner.LastRequest.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task GetServiceConfigPathsAsync_ShouldUseRegisteredProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner(
        [
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.GetServiceConfigPathsAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01");

        result.ServiceKey.Should().Be("nginx");
        result.MainConfig.Should().Be("/etc/nginx/nginx.conf");
        result.ConfigFiles.Should().Equal("/etc/nginx/nginx.conf");
        result.IncludePatterns.Should().Equal("/etc/nginx/conf.d/*.conf");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task GetServiceConfigPathsAsync_ShouldReturnErrorForUnsupportedServiceKey()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.GetServiceConfigPathsAsync(
            service,
            profiles,
            appProviders,
            "postgresql",
            "vps01");

        result.Error.Should().Be("Unsupported serviceKey: postgresql");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ReadServiceConfigFileAsync_ShouldUseRegisteredProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "user nginx;\n",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.ReadServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01");

        result.ServiceKey.Should().Be("nginx");
        result.Path.Should().Be("/etc/nginx/nginx.conf");
        result.Content.Should().Be("user nginx;\n");
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task CanReadServiceConfigFileAsync_ShouldUseRegisteredProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "u",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.CanReadServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01");

        result.ServiceKey.Should().Be("nginx");
        result.Path.Should().Be("/etc/nginx/nginx.conf");
        result.CanRead.Should().BeTrue();
        result.CanWrite.Should().BeFalse();
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_read_config");
        runner.LastRequest.Arguments["maxBytes"].Should().Be("1");
    }

    [Fact]
    public async Task WriteServiceConfigFileAsync_ShouldUseRegisteredProvider()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        const string updatedContent = """
            server {
                server_name localhost;
            }

            """;
        var expectedWrittenContent = updatedContent.ReplaceLineEndings("\n");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: """
                    server {
                        server_name old.example.invalid;
                    }

                    """,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: updatedContent.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.WriteServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "/etc/nginx/conf.d/kelpie-test.conf",
            "server.server_name",
            "replace",
            "service_config_file_write:nginx:/etc/nginx/conf.d/kelpie-test.conf:replace:server.server_name",
            "localhost");

        result.ServiceKey.Should().Be("nginx");
        result.Path.Should().Be("/etc/nginx/conf.d/kelpie-test.conf");
        result.BytesWritten.Should().Be(System.Text.Encoding.UTF8.GetByteCount(expectedWrittenContent));
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_write_config");
        var writtenContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(runner.LastRequest.Arguments["contentBase64"]));
        writtenContent.Should().Be(expectedWrittenContent);
    }

    [Fact]
    public async Task CanWriteServiceConfigFileAsync_ShouldUseRegisteredProviderWithoutWriting()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: """
                    server {
                        server_name old.example.invalid;
                    }

                    """,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "1",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.CanWriteServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "/etc/nginx/conf.d/kelpie-test.conf",
            "server.server_name",
            "replace",
            "localhost");

        result.ServiceKey.Should().Be("nginx");
        result.Path.Should().Be("/etc/nginx/conf.d/kelpie-test.conf");
        result.CanRead.Should().BeTrue();
        result.CanWrite.Should().BeTrue();
        result.RequiresConfirmation.Should().BeTrue();
        result.Confirmation.Should().Be("service_config_file_write:nginx:/etc/nginx/conf.d/kelpie-test.conf:replace:server.server_name");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_check_write_config");
        runner.LastRequest.Arguments.Should().NotContainKey("contentBase64");
    }

    [Fact]
    public async Task CanWriteServiceConfigFileAsync_ShouldReturnMatcherReasonWithoutWriteCheck()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: """
                    server {
                        server_name old.example.invalid;
                    }

                    """,
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.CanWriteServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "/etc/nginx/conf.d/kelpie-test.conf",
            "server.server_name[2]",
            "replace",
            "localhost");

        result.CanRead.Should().BeTrue();
        result.CanWrite.Should().BeFalse();
        result.Reason.Should().Be("TargetKey index did not match any editable Nginx directive.");
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_read_config");
    }

    [Fact]
    public async Task WriteServiceConfigFileAsync_ShouldRequireConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.WriteServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "/etc/nginx/conf.d/kelpie-test.conf",
            "server.server_name",
            "replace",
            "wrong",
            "localhost");

        result.Error.Should().Be("Confirmation is required: service_config_file_write:nginx:/etc/nginx/conf.d/kelpie-test.conf:replace:server.server_name");
        result.BytesWritten.Should().Be(0);
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteServiceConfigFileAsync_ShouldReturnErrorForUnsupportedServiceKey()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.WriteServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "postgresql",
            "vps01",
            "/etc/postgresql/postgresql.conf",
            "server.server_name",
            "replace",
            "service_config_file_write:postgresql:/etc/postgresql/postgresql.conf:replace:server.server_name",
            "localhost");

        result.Error.Should().Be("Unsupported serviceKey: postgresql");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task RollbackServiceConfigFileAsync_ShouldRequireConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.RollbackServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "/etc/nginx/conf.d/kelpie-test.conf",
            "wrong");

        result.Error.Should().Be("Confirmation is required: service_config_file_rollback:nginx:/etc/nginx/conf.d/kelpie-test.conf");
        result.Changed.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task RollbackServiceConfigFileAsync_ShouldUseRegisteredProvider()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "128",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.RollbackServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "/etc/nginx/conf.d/kelpie-test.conf",
            "service_config_file_rollback:nginx:/etc/nginx/conf.d/kelpie-test.conf");

        result.Changed.Should().BeTrue();
        result.Error.Should().BeNull();
        result.BackupPath.Should().Be("/etc/nginx/conf.d/kelpie-test.conf.kelpiebakup");
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_rollback_config");
    }

    [Fact]
    public async Task CommitServiceConfigFileAsync_ShouldUseRegisteredProvider()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "1",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.CommitServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "/etc/nginx/conf.d/kelpie-test.conf",
            "service_config_file_commit:nginx:/etc/nginx/conf.d/kelpie-test.conf");

        result.Changed.Should().BeTrue();
        result.Error.Should().BeNull();
        result.BackupPath.Should().Be("/etc/nginx/conf.d/kelpie-test.conf.kelpiebakup");
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_commit_config");
    }

    [Fact]
    public async Task TestServiceConfigFileAsync_ShouldUseRegisteredProvider()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "nginx: configuration file /etc/nginx/nginx.conf test is successful\n"),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.TestServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "service_config_test:nginx");

        result.ServiceKey.Should().Be("nginx");
        result.DisplayName.Should().Be("Nginx");
        result.TestCommand.Should().Be("nginx -t");
        result.ExitCode.Should().Be(0);
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("service_config_nginx_test_config");
    }

    [Fact]
    public async Task TestServiceConfigFileAsync_ShouldRequireConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.TestServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "wrong");

        result.Error.Should().Be("Confirmation is required: service_config_test:nginx");
        result.ExitCode.Should().Be(-1);
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task TestServiceConfigFileAsync_ShouldReturnErrorForUnsupportedServiceKey()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.TestServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "postgresql",
            "vps01",
            "service_config_test:postgresql");

        result.Error.Should().Be("Unsupported serviceKey: postgresql");
        result.ExitCode.Should().Be(-1);
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task EnableNginxPhpAsync_ShouldRequireConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.EnableNginxPhpAsync(
            service,
            profiles,
            appProviders,
            "vps01",
            "/run/php/php8.3-fpm.sock",
            "wrong");

        result.Error.Should().Be("Confirmation is required: ssh_service_config_nginx_enable_php:default:/run/php/php8.3-fpm.sock:.php");
        result.Changed.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task EnableNginxPhpAsync_ShouldUseRegisteredProvider()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        const string originalContent = """
            server {
                listen 80;
                root /var/www/html;
                index index.html index.htm;
            }

            """;
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: originalContent,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "256",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "nginx: configuration file /etc/nginx/nginx.conf test is successful\n"),
            new FakeSshCommandOutput(
                StandardOutput: string.Empty,
                StandardError: "configure arguments: --conf-path=/etc/nginx/nginx.conf"),
            new FakeSshCommandOutput(
                StandardOutput: "include /etc/nginx/conf.d/*.conf;\n",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: "1",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.EnableNginxPhpAsync(
            service,
            profiles,
            appProviders,
            "vps01",
            "/run/php/php8.3-fpm.sock",
            "ssh_service_config_nginx_enable_php:default:/run/php/php8.3-fpm.sock:.php");

        result.Error.Should().BeNull();
        result.ServiceKey.Should().Be("nginx");
        result.SiteKey.Should().Be("default");
        result.Path.Should().Be("/etc/nginx/conf.d/default.conf");
        result.Changed.Should().BeTrue();
        result.Tested.Should().BeTrue();
        result.Committed.Should().BeTrue();
        runner.Requests.Select(request => request.CommandName).Should().ContainInOrder(
            "service_config_nginx_write_config",
            "service_config_nginx_test_config",
            "service_config_nginx_commit_config");
    }

    [Fact]
    public async Task ReadServiceConfigFileAsync_ShouldReturnErrorForUnsupportedServiceKey()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.ReadServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "postgresql",
            "vps01");

        result.Error.Should().Be("Unsupported serviceKey: postgresql");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task CanReadServiceConfigFileAsync_ShouldReturnErrorForUnsupportedServiceKey()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.CanReadServiceConfigFileAsync(
            service,
            profiles,
            appProviders,
            "postgresql",
            "vps01");

        result.Error.Should().Be("Unsupported serviceKey: postgresql");
        result.CanRead.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ReadServiceLogfileAsync_ShouldUseRegisteredProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: "127.0.0.1 - - [14/Jun/2026:10:00:00 +0000] \"GET / HTTP/1.1\" 200 12\n",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.ReadServiceLogfileAsync(
            service,
            profiles,
            appProviders,
            "nginx",
            "vps01",
            "access",
            sinceMinutes: 10,
            lines: 500);

        result.ServiceKey.Should().Be("nginx");
        result.LogKey.Should().Be("access");
        result.Path.Should().Be("/var/log/nginx/access.log");
        result.Content.Should().Contain("GET / HTTP/1.1");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("service_logfile_nginx_read");
    }

    [Fact]
    public async Task ReadServiceLogfileAsync_ShouldReturnErrorForUnsupportedServiceKey()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([]);
        var appProviders = ServiceConfigPathsProviderCatalog.CreateDefault();

        var result = await KelpieTools.ReadServiceLogfileAsync(
            service,
            profiles,
            appProviders,
            "postgresql",
            "vps01",
            "access");

        result.Error.Should().Be("Unsupported serviceKey: postgresql");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ReadWebFileAsync_ShouldUseProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","exists":true,"contentBase64":"PGgxPk9LPC9oMT4=","size":11,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.ReadWebFileAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/my_dir/sample.html");

        result.SiteKey.Should().Be("default");
        result.ResolvedPath.Should().Be("/var/www/html/my_dir/sample.html");
        result.ContentBase64.Should().Be("PGgxPk9LPC9oMT4=");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_read_internal");
    }

    [Fact]
    public async Task ReadWebFileHeadAsync_ShouldUseProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/app.txt","exists":true,"contentBase64":"b25lCg==","size":4,"sourceSize":20,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.ReadWebFileHeadAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/app.txt",
            maxBytes: 128,
            maxLines: 1);

        result.ContentBase64.Should().Be("b25lCg==");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_slice_internal");
        runner.LastRequest.Arguments["mode"].Should().Be("head");
    }

    [Fact]
    public async Task ReadWebFileTailAsync_ShouldUseProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/app.txt","exists":true,"contentBase64":"dHdvCg==","size":4,"sourceSize":20,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.ReadWebFileTailAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/app.txt",
            maxBytes: 128,
            maxLines: 1);

        result.ContentBase64.Should().Be("dHdvCg==");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_slice_internal");
        runner.LastRequest.Arguments["mode"].Should().Be("tail");
    }

    [Fact]
    public async Task ListWebFilesAsync_ShouldUseProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html","exists":true,"entries":[{"name":"index.html","path":"/index.html","resolvedPath":"/var/www/html/index.html","type":"file","size":128,"mode":"644","owner":"nginx","group":"nginx","lastModified":"2026-06-15T00:00:00Z","depth":0,"isSymlink":false}],"truncated":false}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.ListWebFilesAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/",
            maxDepth: 0,
            limit: 100);

        result.SiteKey.Should().Be("default");
        result.Entries.Should().ContainSingle().Which.Path.Should().Be("/index.html");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_list_internal");
    }

    [Fact]
    public async Task SearchWebFileTextAsync_ShouldUseProvider()
    {
        var profile = CreateProfile("vps01");
        var contentBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("needle\n"));
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html","exists":true,"entries":[{"name":"index.html","path":"/index.html","resolvedPath":"/var/www/html/index.html","type":"file","size":7,"mode":"644","owner":"nginx","group":"nginx","lastModified":null,"depth":0,"isSymlink":false}],"truncated":false}""",
                StandardError: string.Empty),
            new FakeSshCommandOutput(
                StandardOutput: $$"""{"resolvedPath":"/var/www/html/index.html","exists":true,"contentBase64":"{{contentBase64}}","size":7,"lastModified":"2026-06-14T00:00:00Z"}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.SearchWebFileTextAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "needle");

        result.Matches.Should().ContainSingle().Which.Path.Should().Be("/index.html");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_read_internal");
    }

    [Fact]
    public async Task StatWebFileAsync_ShouldUseProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/index.html","exists":true,"type":"file","size":128,"mode":"644","owner":"nginx","group":"nginx","lastModified":"2026-06-15T00:00:00Z","isSymlink":false}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.StatWebFileAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/index.html");

        result.Exists.Should().BeTrue();
        result.Type.Should().Be("file");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_stat_internal");
    }

    [Fact]
    public async Task CheckWriteWebFileAsync_ShouldUseProviderWithoutWriting()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","exists":false,"canWrite":true,"reason":null}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.CheckWriteWebFileAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/my_dir/sample.html",
            contentType: "text/html");

        result.CanWrite.Should().BeTrue();
        result.Confirmation.Should().Be("web_file_write:default:/my_dir/sample.html");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_check_write_internal");
    }

    [Fact]
    public async Task WriteWebFileAsync_ShouldRequireConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.WriteWebFileAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/my_dir/sample.html",
            "YQ==",
            "wrong",
            encoding: "utf-8",
            contentType: "text/html");

        result.Error.Should().Be("Confirmation is required: web_file_write:default:/my_dir/sample.html");
        result.Written.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteWebFileAsync_ShouldWriteConfirmedFile()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var contentBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<h1>OK</h1>"));
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","written":true,"created":false,"overwritten":true,"size":11}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.WriteWebFileAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/my_dir/sample.html",
            contentBase64,
            "web_file_write:default:/my_dir/sample.html",
            encoding: "utf-8",
            contentType: "text/html");

        result.Written.Should().BeTrue();
        result.Overwritten.Should().BeTrue();
        result.ContentType.Should().Be("text/html");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_write_internal");
    }

    [Fact]
    public async Task WriteWebFileAsync_ShouldRequirePermissionConfirmationWhenOwnerGroupModeAreSpecified()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.WriteWebFileAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/my_dir/sample.html",
            "YQ==",
            "web_file_write:default:/my_dir/sample.html",
            encoding: "utf-8",
            contentType: "text/html",
            owner: "www-data:www-data",
            mode: "775");

        result.Error.Should().Be("Confirmation is required: web_file_write:default:/my_dir/sample.html:www-data:www-data:775");
        result.Written.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteWebFileAsync_ShouldWriteConfirmedPermissionedFile()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var contentBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<h1>OK</h1>"));
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir/sample.html","written":true,"created":true,"overwritten":false,"size":11,"owner":"www-data","group":"www-data","mode":"775"}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.WriteWebFileAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/my_dir/sample.html",
            contentBase64,
            "web_file_write:default:/my_dir/sample.html:www-data:www-data:775",
            encoding: "utf-8",
            contentType: "text/html",
            owner: "www-data:www-data",
            mode: "775");

        result.Written.Should().BeTrue();
        result.Owner.Should().Be("www-data");
        result.Group.Should().Be("www-data");
        result.Mode.Should().Be("775");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_write_with_permissions_internal");
    }

    [Fact]
    public async Task ChangeWebPublicOwnerRecursiveAsync_ShouldRequireConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.ChangeWebPublicOwnerRecursiveAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/my_dir",
            "deploy",
            "www-data",
            "web_change_owner:default:/my_dir:deploy:www-data");

        result.Error.Should().Be("Confirmation is required: web_change_owner_recursive:default:/my_dir:deploy:www-data");
        result.Changed.Should().BeFalse();
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ChangeWebPublicModeRecursiveAsync_ShouldPassRecursiveFlag()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir","changed":true,"owner":"","group":"","mode":"775"}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.ChangeWebPublicModeRecursiveAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/my_dir",
            "775",
            "web_change_mode_recursive:default:/my_dir:775");

        result.Changed.Should().BeTrue();
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_change_mode_internal");
        runner.LastRequest.Arguments["recursive"].Should().Be("1");
    }

    [Fact]
    public async Task SearchWebFileNamesAsync_ShouldUseProvider()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html","exists":true,"entries":[{"name":"index.html","path":"/index.html","resolvedPath":"/var/www/html/index.html","type":"file","size":128,"mode":"644","owner":"nginx","group":"nginx","lastModified":null,"depth":0,"isSymlink":false},{"name":"app.css","path":"/app.css","resolvedPath":"/var/www/html/app.css","type":"file","size":64,"mode":"644","owner":"nginx","group":"nginx","lastModified":null,"depth":0,"isSymlink":false}],"truncated":false}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.SearchWebFileNamesAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "*.html",
            "/",
            maxDepth: 2,
            limit: 50);

        result.Entries.Should().ContainSingle().Which.Name.Should().Be("index.html");
        runner.LastRequest!.CommandName.Should().Be("web_public_file_list_internal");
        runner.LastRequest.Arguments["maxDepth"].Should().Be("2");
        runner.LastRequest.Arguments["limit"].Should().Be("50");
    }

    [Fact]
    public async Task CheckWebFilePermissionsAsync_ShouldReturnConfirmationTokens()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput(
                StandardOutput: """{"resolvedPath":"/var/www/html/my_dir","exists":true,"type":"directory","size":128,"mode":"755","owner":"nginx","group":"nginx","lastModified":null,"isSymlink":false}""",
                StandardError: string.Empty),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var webProvider = new WebPublicFileProvider();

        var result = await KelpieTools.CheckWebFilePermissionsAsync(
            service,
            profiles,
            webProvider,
            "vps01",
            "default",
            "/my_dir",
            owner: "deploy",
            group: "www-data",
            mode: "775",
            recursive: true);

        result.CanChangeOwner.Should().BeTrue();
        result.CanChangeMode.Should().BeTrue();
        result.OwnerConfirmation.Should().Be("web_change_owner_recursive:default:/my_dir:deploy:www-data");
        result.ModeConfirmation.Should().Be("web_change_mode_recursive:default:/my_dir:775");
        result.Error.Should().BeNull();
        runner.LastRequest!.CommandName.Should().Be("web_public_file_stat_internal");
    }

    [Fact]
    public async Task RunAllowedSshCommandAsync_ShouldRejectInternalServiceConfigCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunAllowedSshCommandAsync(
            service,
            profiles,
            "service_config_nginx_read_config",
            "vps01");

        result.Error.Should().Be("Service config commands must be called through service_config_* tools.");
        result.ExitCode.Should().Be(-1);
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task RunAllowedSshCommandAsync_ShouldRejectInternalServiceConfigWriteCommand()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunAllowedSshCommandAsync(
            service,
            profiles,
            "service_config_nginx_write_config",
            "vps01");

        result.Error.Should().Be("Service config commands must be called through service_config_* tools.");
        result.ExitCode.Should().Be(-1);
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task RunAllowedSshCommandAsync_ShouldRejectInternalServiceLogfileCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunAllowedSshCommandAsync(
            service,
            profiles,
            "service_logfile_nginx_read",
            "vps01");

        result.Error.Should().Be("Service logfile commands must be called through service_logfile_* tools.");
        result.ExitCode.Should().Be(-1);
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task RunAllowedSshCommandAsync_ShouldRejectInternalWebPublicFileCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunAllowedSshCommandAsync(
            service,
            profiles,
            "web_public_file_read_internal",
            "vps01");

        result.Error.Should().Be("Web public file commands must be called through web_file_* tools.");
        result.ExitCode.Should().Be(-1);
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task RunAllowedSshCommandAsync_ShouldReturnErrorResultForUnknownCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunAllowedSshCommandAsync(
            service,
            profiles,
            "not_allowed",
            "vps01");

        result.ProfileName.Should().Be("vps01");
        result.CommandName.Should().Be("not_allowed");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("SSH command is not allowed: not_allowed");
        result.Ok.Should().BeFalse();
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be("KELPIE_POLICY_COMMAND_DENIED");
        result.ErrorInfo.Category.Should().Be("PolicyDenied");
        result.StandardError.Should().Be("SSH command is not allowed: not_allowed");
        result.Stderr.Should().Equal("SSH command is not allowed: not_allowed");
        result.StderrPlain.Should().Equal("SSH command is not allowed: not_allowed");
        runner.LastRequest.Should().BeNull();
    }


    [Fact]
    public async Task GetSshSystemInfoAsync_ShouldRejectMissingProfileName()
    {
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([]);

        var result = await KelpieTools.GetSshSystemInfoAsync(service, profiles, string.Empty);

        result.CommandName.Should().Be("get_system_info");
        result.Ok.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("SSH profile name is required.");
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be("KELPIE_MCP_INPUT_INVALID");
        result.ErrorInfo.Category.Should().Be("Validation");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task TailSshLogAsync_ShouldPassSafeArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.TailSshLogAsync(
            service,
            profiles,
            "nginx.service",
            "vps01",
            "50");

        result.CommandName.Should().Be("tail_log");
        result.CommandText.Should().Be("journalctl -u 'nginx.service' -n '50' --no-pager");
        runner.LastRequest!.Arguments["service"].Should().Be("nginx.service");
        runner.LastRequest.Arguments["lines"].Should().Be("50");
    }

    [Fact]
    public async Task GetSshServiceStatusAsync_ShouldPassSafeServiceArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshServiceStatusAsync(
            service,
            profiles,
            "nginx.service",
            "vps01");

        result.CommandName.Should().Be("service_status");
        result.CommandText.Should().Be("systemctl status 'nginx.service' --no-pager");
        runner.LastRequest!.Arguments["service"].Should().Be("nginx.service");
    }

    [Fact]
    public async Task GetSshServiceStatusAsync_ShouldReturnRemoteFailureResult()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner([
            new FakeSshCommandOutput("inactive\n", "Unit nginx.service could not be found.\n", 3),
        ]);
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshServiceStatusAsync(
            service,
            profiles,
            "nginx.service",
            "vps01");

        result.CommandName.Should().Be("service_status");
        result.ExitCode.Should().Be(3);
        result.Ok.Should().BeFalse();
        result.StandardOutput.Should().Be("inactive\n");
        result.StandardError.Should().Be("Unit nginx.service could not be found.\n");
        result.Stdout.Should().Equal("inactive", string.Empty);
        result.Stderr.Should().Equal("Unit nginx.service could not be found.", string.Empty);
        result.Error.Should().BeNull();
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be("KELPIE_REMOTE_COMMAND_FAILED");
        result.ErrorInfo.Category.Should().Be("RemoteCommand");
        result.Data.Should().BeNull();
        runner.LastRequest!.Arguments["service"].Should().Be("nginx.service");
    }

    [Fact]
    public async Task GetSshServiceStatusAsync_ShouldRejectUnsafeServiceArgumentBeforeExecution()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshServiceStatusAsync(
            service,
            profiles,
            "nginx.service;whoami",
            "vps01");

        result.CommandName.Should().Be("service_status");
        result.Ok.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("SSH command argument contains a dangerous fragment: service");
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be("KELPIE_MCP_INPUT_INVALID");
        result.ErrorInfo.Category.Should().Be("Validation");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task GetSshServiceIsActiveAsync_ShouldPassSafeServiceArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshServiceIsActiveAsync(
            service,
            profiles,
            "nginx.service",
            "vps01");

        result.CommandName.Should().Be("service_is_active");
        result.CommandText.Should().Be("systemctl is-active 'nginx.service'");
        runner.LastRequest!.Arguments["service"].Should().Be("nginx.service");
    }

    [Fact]
    public async Task GetSshServiceIsEnabledAsync_ShouldPassSafeServiceArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshServiceIsEnabledAsync(
            service,
            profiles,
            "nginx.service",
            "vps01");

        result.CommandName.Should().Be("service_is_enabled");
        result.CommandText.Should().Be("systemctl is-enabled 'nginx.service'");
        runner.LastRequest!.Arguments["service"].Should().Be("nginx.service");
    }

    [Fact]
    public async Task ListSshServicesAsync_ShouldPassStateAndLimitArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ListSshServicesAsync(
            service,
            profiles,
            "vps01",
            "running",
            "25");

        result.CommandName.Should().Be("list_services");
        result.CommandText.Should().Contain("systemctl");
        result.CommandText.Should().Contain("state=\"$1\"");
        result.CommandText.Should().Contain("'running' '25'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["state"].Should().Be("running");
        runner.LastRequest.Arguments["limit"].Should().Be("25");
    }

    [Fact]
    public async Task GetSshJournalRecentAsync_ShouldPassLinesArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshJournalRecentAsync(
            service,
            profiles,
            "vps01",
            "25");

        result.CommandName.Should().Be("get_journal_recent");
        result.CommandText.Should().Be("journalctl -n '25' --no-pager");
        runner.LastRequest!.Arguments["lines"].Should().Be("25");
    }

    [Fact]
    public async Task CollectSshSupportReportAsync_ShouldPassLimitArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CollectSshSupportReportAsync(
            service,
            profiles,
            "vps01",
            "20");

        result.CommandName.Should().Be("support_report_collect");
        result.Host.Should().BeEmpty();
        result.Port.Should().Be(0);
        result.UserName.Should().BeEmpty();
        result.CommandText.Should().Contain("sh -s -- '20'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task CheckSshCronWriteAsync_ShouldPassSafeArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshCronWriteAsync(
            service,
            profiles,
            "vps01",
            "user",
            "deploy",
            "*/5 * * * *",
            "/usr/local/bin/job --once",
            "/var/log/kelpie/job.log");

        result.CommandName.Should().Be("cron_check_write");
        runner.LastRequest!.Arguments["targetType"].Should().Be("user");
        runner.LastRequest.Arguments["runUser"].Should().Be("deploy");
        runner.LastRequest.Arguments["cronExpression"].Should().Be("*/5 * * * *");
        runner.LastRequest.Arguments["command"].Should().Be("/usr/local/bin/job --once");
        runner.LastRequest.Arguments["logPath"].Should().Be("/var/log/kelpie/job.log");
    }

    [Fact]
    public async Task WriteSshCronAsync_ShouldReturnConfirmationError()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.WriteSshCronAsync(
            service,
            profiles,
            "vps01",
            "user",
            "deploy",
            "*/5 * * * *",
            "/usr/local/bin/job --once",
            "/var/log/kelpie/job.log",
            string.Empty);

        result.CommandName.Should().Be("cron_write");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: cron_write:user:deploy");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task WriteSshCronAsync_ShouldExecuteWithConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.WriteSshCronAsync(
            service,
            profiles,
            "vps01",
            "user",
            "deploy",
            "*/5 * * * *",
            "/usr/local/bin/job --once",
            "/var/log/kelpie/job.log",
            "cron_write:user:deploy");

        result.CommandName.Should().Be("cron_write");
        runner.LastRequest!.Arguments["targetType"].Should().Be("user");
        runner.LastRequest.Arguments["runUser"].Should().Be("deploy");
        runner.LastRequest.Arguments["cronExpression"].Should().Be("*/5 * * * *");
        runner.LastRequest.Arguments["command"].Should().Be("/usr/local/bin/job --once");
        runner.LastRequest.Arguments["logPath"].Should().Be("/var/log/kelpie/job.log");
    }

    [Fact]
    public async Task RollbackSshCronAsync_ShouldExecuteWithConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RollbackSshCronAsync(
            service,
            profiles,
            "vps01",
            "user",
            "deploy",
            "cron_rollback:user:deploy");

        result.CommandName.Should().Be("cron_rollback");
        runner.LastRequest!.Arguments["targetType"].Should().Be("user");
        runner.LastRequest.Arguments["runUser"].Should().Be("deploy");
    }

    [Fact]
    public async Task CheckSshUserGroupChangeAsync_ShouldPassSafeArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshUserGroupChangeAsync(
            service,
            profiles,
            "vps01",
            "deploy",
            "nginx,wheel",
            "replace");

        result.CommandName.Should().Be("user_check_group_change");
        runner.LastRequest!.Arguments["user"].Should().Be("deploy");
        runner.LastRequest.Arguments["groups"].Should().Be("nginx,wheel");
        runner.LastRequest.Arguments["mode"].Should().Be("replace");
    }

    [Fact]
    public async Task ApplySshUserGroupChangeAsync_ShouldReturnConfirmationError()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ApplySshUserGroupChangeAsync(
            service,
            profiles,
            "vps01",
            "deploy",
            "nginx,wheel",
            "append",
            string.Empty);

        result.CommandName.Should().Be("user_apply_group_change");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: user_apply_group_change:deploy:append:nginx,wheel");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ApplySshUserGroupChangeAsync_ShouldExecuteWithConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ApplySshUserGroupChangeAsync(
            service,
            profiles,
            "vps01",
            "deploy",
            "nginx,wheel",
            "append",
            "user_apply_group_change:deploy:append:nginx,wheel");

        result.CommandName.Should().Be("user_apply_group_change");
        runner.LastRequest!.Arguments["user"].Should().Be("deploy");
        runner.LastRequest.Arguments["groups"].Should().Be("nginx,wheel");
        runner.LastRequest.Arguments["mode"].Should().Be("append");
    }

    [Fact]
    public async Task RollbackSshUserGroupChangeAsync_ShouldExecuteWithConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RollbackSshUserGroupChangeAsync(
            service,
            profiles,
            "vps01",
            "deploy",
            "user_rollback_group_change:deploy");

        result.CommandName.Should().Be("user_rollback_group_change");
        runner.LastRequest!.Arguments["user"].Should().Be("deploy");
    }

    [Fact]
    public async Task CheckSshUserPermissionChangeAsync_ShouldPassSafeArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshUserPermissionChangeAsync(
            service,
            profiles,
            "vps01",
            "deploy",
            "/bin/bash",
            "disabled",
            "absent");

        result.CommandName.Should().Be("user_check_permission_change");
        runner.LastRequest!.Arguments["user"].Should().Be("deploy");
        runner.LastRequest.Arguments["shell"].Should().Be("/bin/bash");
        runner.LastRequest.Arguments["login"].Should().Be("disabled");
        runner.LastRequest.Arguments["sudo"].Should().Be("absent");
    }

    [Fact]
    public async Task ApplySshUserPermissionChangeAsync_ShouldReturnConfirmationError()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ApplySshUserPermissionChangeAsync(
            service,
            profiles,
            "vps01",
            "deploy",
            "/bin/bash",
            "disabled",
            "absent",
            string.Empty);

        result.CommandName.Should().Be("user_apply_permission_change");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: user_apply_permission_change:deploy:/bin/bash:disabled:absent");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ApplySshUserPermissionChangeAsync_ShouldExecuteWithConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ApplySshUserPermissionChangeAsync(
            service,
            profiles,
            "vps01",
            "deploy",
            "/bin/bash",
            "disabled",
            "absent",
            "user_apply_permission_change:deploy:/bin/bash:disabled:absent");

        result.CommandName.Should().Be("user_apply_permission_change");
        runner.LastRequest!.Arguments["user"].Should().Be("deploy");
        runner.LastRequest.Arguments["shell"].Should().Be("/bin/bash");
        runner.LastRequest.Arguments["login"].Should().Be("disabled");
        runner.LastRequest.Arguments["sudo"].Should().Be("absent");
    }

    [Fact]
    public async Task RollbackSshUserPermissionChangeAsync_ShouldReturnConfirmationError()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RollbackSshUserPermissionChangeAsync(
            service,
            profiles,
            "vps01",
            "deploy",
            string.Empty);

        result.CommandName.Should().Be("user_rollback_permission_change");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: user_rollback_permission_change:deploy");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task RollbackSshUserPermissionChangeAsync_ShouldExecuteWithConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RollbackSshUserPermissionChangeAsync(
            service,
            profiles,
            "vps01",
            "deploy",
            "user_rollback_permission_change:deploy");

        result.CommandName.Should().Be("user_rollback_permission_change");
        runner.LastRequest!.Arguments["user"].Should().Be("deploy");
    }

    [Fact]
    public async Task GetSshFirewallStatusAsync_ShouldRunProviderCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshFirewallStatusAsync(service, profiles, "vps01");

        result.CommandName.Should().Be("firewall_status");
        result.CommandText.Should().StartWith("sh -c");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.CommandName.Should().Be("firewall_status");
    }

    [Fact]
    public async Task CheckSshFirewallRuleAsync_ShouldPassSafeArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshFirewallRuleAsync(
            service,
            profiles,
            "vps01",
            "add",
            "port",
            "443/tcp",
            "public",
            "false");

        result.CommandName.Should().Be("firewall_check_rule");
        runner.LastRequest!.Arguments["action"].Should().Be("add");
        runner.LastRequest.Arguments["target"].Should().Be("port");
        runner.LastRequest.Arguments["value"].Should().Be("443/tcp");
        runner.LastRequest.Arguments["zone"].Should().Be("public");
        runner.LastRequest.Arguments["permanent"].Should().Be("false");
    }

    [Fact]
    public async Task ApplySshFirewallRuleAsync_ShouldReturnConfirmationError()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ApplySshFirewallRuleAsync(
            service,
            profiles,
            "vps01",
            "add",
            "port",
            "443/tcp",
            "public",
            "false",
            string.Empty);

        result.CommandName.Should().Be("firewall_apply_rule");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: firewall_apply_rule:add:port:443/tcp:public:false");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ApplySshFirewallRuleAsync_ShouldExecuteWithConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ApplySshFirewallRuleAsync(
            service,
            profiles,
            "vps01",
            "add",
            "port",
            "443/tcp",
            "public",
            "false",
            "firewall_apply_rule:add:port:443/tcp:public:false");

        result.CommandName.Should().Be("firewall_apply_rule");
        runner.LastRequest!.Arguments["value"].Should().Be("443/tcp");
    }

    [Fact]
    public async Task CheckSshBackupPlanAsync_ShouldPassSafeArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshBackupPlanAsync(
            service,
            profiles,
            "vps01",
            "/var/www",
            "2",
            "20");

        result.CommandName.Should().Be("backup_plan_check");
        runner.LastRequest!.Arguments["scanRoot"].Should().Be("/var/www");
        runner.LastRequest.Arguments["depth"].Should().Be("2");
        runner.LastRequest.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task RunSshBackupAsync_ShouldReturnConfirmationError()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunSshBackupAsync(
            service,
            profiles,
            "vps01",
            "/var/www",
            "2",
            "20",
            string.Empty);

        result.CommandName.Should().Be("backup_run");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: backup_run:/var/www");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task RunSshBackupAsync_ShouldExecuteWithConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunSshBackupAsync(
            service,
            profiles,
            "vps01",
            "/var/www",
            "2",
            "20",
            "backup_run:/var/www");

        result.CommandName.Should().Be("backup_run");
        runner.LastRequest!.Arguments["scanRoot"].Should().Be("/var/www");
        runner.LastRequest.Arguments["depth"].Should().Be("2");
        runner.LastRequest.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task VerifySshBackupAsync_ShouldPassSafePath()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.VerifySshBackupAsync(
            service,
            profiles,
            "vps01",
            "/var/backups/kelpie/site/full.tar.gz");

        result.CommandName.Should().Be("backup_verify");
        result.CommandText.Should().Contain("sh -s -- '/var/backups/kelpie/site/full.tar.gz'");
        result.CommandText.Should().NotContain("python3");
        runner.LastRequest!.Arguments["backupPath"].Should().Be("/var/backups/kelpie/site/full.tar.gz");
    }

    [Fact]
    public async Task VerifySshAuditAsync_ShouldPassSafeArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.VerifySshAuditAsync(
            service,
            profiles,
            "vps01",
            "/var/log/kelpie/audit.log",
            "50");

        result.CommandName.Should().Be("audit_verify");
        runner.LastRequest!.Arguments["logPath"].Should().Be("/var/log/kelpie/audit.log");
        runner.LastRequest.Arguments["limit"].Should().Be("50");
    }

    [Fact]
    public async Task ExportSshAuditAsync_ShouldPassSafeArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ExportSshAuditAsync(
            service,
            profiles,
            "vps01",
            "/var/log/kelpie/audit.log",
            "50");

        result.CommandName.Should().Be("audit_export");
        runner.LastRequest!.Arguments["logPath"].Should().Be("/var/log/kelpie/audit.log");
        runner.LastRequest.Arguments["limit"].Should().Be("50");
    }

    [Fact]
    public async Task RunAllowedSshCommandAsync_ShouldRejectAuditCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunAllowedSshCommandAsync(
            service,
            profiles,
            "audit_export",
            "vps01",
            new Dictionary<string, string>
            {
                ["logPath"] = "/var/log/kelpie/audit.log",
                ["limit"] = "50",
            });

        result.Error.Should().Be("Audit commands must be called through ssh_audit_* tools.");
        result.ExitCode.Should().Be(-1);
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task RunAllowedSshCommandAsync_ShouldRejectSupportReportCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.RunAllowedSshCommandAsync(
            service,
            profiles,
            "support_report_collect",
            "vps01");

        result.Error.Should().Be("Support report commands must be called through ssh_support_report_collect.");
        result.ExitCode.Should().Be(-1);
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task CheckSshPackageUpdatesAsync_ShouldRunProviderCommand()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.CheckSshPackageUpdatesAsync(service, profiles, "vps01");

        result.CommandName.Should().Be("pkg_check_updates");
        result.CommandText.Should().Be("apt list --upgradable");
        runner.LastRequest!.CommandName.Should().Be("pkg_check_updates");
    }

    [Fact]
    public async Task GetSshPackageInfoAsync_ShouldPassPackageArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.GetSshPackageInfoAsync(
            service,
            profiles,
            "nginx",
            "vps01");

        result.CommandName.Should().Be("pkg_info");
        result.CommandText.Should().Be("apt-cache policy 'nginx'");
        runner.LastRequest!.Arguments["package"].Should().Be("nginx");
    }

    [Fact]
    public async Task SearchSshPackagesAsync_ShouldPassQueryAndLimitArguments()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.SearchSshPackagesAsync(
            service,
            profiles,
            "nginx",
            "vps01",
            "20");

        result.CommandName.Should().Be("pkg_search");
        result.CommandText.Should().Contain("apt-cache");
        result.CommandText.Should().Contain("query='nginx'");
        result.CommandText.Should().Contain("limit=int('20')");
        runner.LastRequest!.Arguments["query"].Should().Be("nginx");
        runner.LastRequest.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task ListInstalledSshPackagesAsync_ShouldPassFilterAndLimitArguments()
    {
        var profile = CreateProfile("vps01", osFamily: "alma", packageManager: "dnf");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ListInstalledSshPackagesAsync(
            service,
            profiles,
            "nginx",
            "vps01",
            "20");

        result.CommandName.Should().Be("pkg_list_installed");
        result.CommandText.Should().Contain("dnf");
        result.CommandText.Should().Contain("filter_text='nginx'.lower()");
        result.CommandText.Should().Contain("limit=int('20')");
        runner.LastRequest!.Arguments["filter"].Should().Be("nginx");
        runner.LastRequest.Arguments["limit"].Should().Be("20");
    }

    [Fact]
    public async Task SimulateSshPackageInstallAsync_ShouldPassPackageArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.SimulateSshPackageInstallAsync(
            service,
            profiles,
            "nginx",
            "vps01");

        result.CommandName.Should().Be("pkg_simulate_install");
        result.CommandText.Should().Be("apt-get -s install 'nginx'");
        runner.LastRequest!.Arguments["package"].Should().Be("nginx");
    }

    [Fact]
    public async Task SimulateSshPackageRemoveAsync_ShouldPassPackageArgument()
    {
        var profile = CreateProfile("vps01");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.SimulateSshPackageRemoveAsync(
            service,
            profiles,
            "nginx",
            "vps01");

        result.CommandName.Should().Be("pkg_simulate_remove");
        result.CommandText.Should().Be("apt-get -s remove 'nginx'");
        runner.LastRequest!.Arguments["package"].Should().Be("nginx");
    }

    [Fact]
    public void ConfirmSshPackageInstall_ShouldReturnConfirmationWithoutExecution()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = KelpieTools.ConfirmSshPackageInstall(service, profiles, "nginx", "vps01");

        result.CommandName.Should().Be("pkg_install");
        result.CommandText.Should().Be("sudo -n env DEBIAN_FRONTEND=noninteractive apt-get install -y 'nginx'");
        result.RiskLevel.Should().Be(nameof(SshCommandRiskLevel.ConfirmRequired));
        result.RequiresConfirmation.Should().BeTrue();
        result.Message.Should().Be("Command requires confirmation and has not been executed.");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task InstallSshPackageConfirmedAsync_ShouldExecuteWithConfirmation()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.InstallSshPackageConfirmedAsync(
            service,
            profiles,
            "nginx",
            "vps01",
            "pkg_install:nginx");

        result.CommandName.Should().Be("pkg_install");
        runner.LastRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task InstallSshPackageConfirmedAsync_ShouldReturnConfirmationError()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.InstallSshPackageConfirmedAsync(
            service,
            profiles,
            "nginx",
            "vps01",
            string.Empty);

        result.CommandName.Should().Be("pkg_install");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: pkg_install:nginx");
        result.StandardError.Should().Be("Confirmation is required: pkg_install:nginx");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task InstallSshPackageConfirmedAsync_ShouldRejectSafeModePolicyBeforeExecution()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Safe);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.InstallSshPackageConfirmedAsync(
            service,
            profiles,
            "nginx",
            "vps01",
            "pkg_install:nginx");

        result.CommandName.Should().Be("pkg_install");
        result.Ok.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("KelpiePolicyError: AllowSudo is required for command: pkg_install");
        result.ErrorInfo.Should().NotBeNull();
        result.ErrorInfo!.Code.Should().Be("KELPIE_POLICY_COMMAND_DENIED");
        result.ErrorInfo.Category.Should().Be("PolicyDenied");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task EnableNowSshServiceAsync_ShouldReturnConfirmationError()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert, "alma", "dnf");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.EnableNowSshServiceAsync(
            service,
            profiles,
            "nginx",
            "vps01",
            string.Empty);

        result.CommandName.Should().Be("service_enable_now");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: service_enable_now:nginx");
        result.StandardError.Should().Be("Confirmation is required: service_enable_now:nginx");
        runner.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task ReloadSshServiceAsync_ShouldReturnConfirmationError()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert, "alma", "dnf");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = await KelpieTools.ReloadSshServiceAsync(
            service,
            profiles,
            "nginx",
            "vps01",
            string.Empty);

        result.CommandName.Should().Be("service_reload");
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: service_reload:nginx");
        result.StandardError.Should().Be("Confirmation is required: service_reload:nginx");
        runner.LastRequest.Should().BeNull();
    }

    [Theory]
    [InlineData("restart", "service_restart", "service_restart:nginx")]
    [InlineData("stop", "service_stop", "service_stop:nginx")]
    [InlineData("disable", "service_disable", "service_disable:nginx")]
    public async Task ServiceMaintenanceAsync_ShouldReturnConfirmationError(
        string operation,
        string commandName,
        string expectedErrorSuffix)
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert, "alma", "dnf");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = operation switch
        {
            "restart" => await KelpieTools.RestartSshServiceAsync(service, profiles, "nginx", "vps01", string.Empty),
            "stop" => await KelpieTools.StopSshServiceAsync(service, profiles, "nginx", "vps01", string.Empty),
            "disable" => await KelpieTools.DisableSshServiceAsync(service, profiles, "nginx", "vps01", string.Empty),
            _ => throw new InvalidOperationException("Unexpected operation."),
        };

        result.CommandName.Should().Be(commandName);
        result.ExitCode.Should().Be(-1);
        result.Error.Should().Be("Confirmation is required: " + expectedErrorSuffix);
        result.StandardError.Should().Be("Confirmation is required: " + expectedErrorSuffix);
        runner.LastRequest.Should().BeNull();
    }

    [Theory]
    [InlineData("restart", "service_restart")]
    [InlineData("stop", "service_stop")]
    [InlineData("disable", "service_disable")]
    public async Task ServiceMaintenanceAsync_ShouldExecuteWithConfirmation(string operation, string commandName)
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert, "alma", "dnf");
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);
        var confirmation = commandName + ":nginx";

        var result = operation switch
        {
            "restart" => await KelpieTools.RestartSshServiceAsync(service, profiles, "nginx", "vps01", confirmation),
            "stop" => await KelpieTools.StopSshServiceAsync(service, profiles, "nginx", "vps01", confirmation),
            "disable" => await KelpieTools.DisableSshServiceAsync(service, profiles, "nginx", "vps01", confirmation),
            _ => throw new InvalidOperationException("Unexpected operation."),
        };

        result.CommandName.Should().Be(commandName);
        runner.LastRequest.Should().NotBeNull();
        runner.LastRequest!.Arguments["service"].Should().Be("nginx");
    }

    [Fact]
    public void ConfirmSshPackageRemove_ShouldReturnConfirmationWithoutExecution()
    {
        var profile = CreateProfile("vps01", KelpiePolicyMode.Expert);
        var runner = new FakeSshCommandRunner();
        var service = CreateProviderBackedService(runner);
        var profiles = new SshConnectionProfileCatalog([profile]);

        var result = KelpieTools.ConfirmSshPackageRemove(service, profiles, "nginx", "vps01");

        result.CommandName.Should().Be("pkg_remove");
        result.CommandText.Should().Be("sudo -n env DEBIAN_FRONTEND=noninteractive apt-get remove -y 'nginx'");
        result.RiskLevel.Should().Be(nameof(SshCommandRiskLevel.ConfirmRequired));
        result.RequiresConfirmation.Should().BeTrue();
        runner.LastRequest.Should().BeNull();
    }

    private static SshConnectionProfile CreateProfile(
        string name,
        KelpiePolicyMode mode = KelpiePolicyMode.Safe,
        string osFamily = "debian",
        string packageManager = "apt",
        PolicySet? capabilities = null,
        IReadOnlyCollection<EnvironmentValueRule>? environmentValues = null)
    {
        return new SshConnectionProfile
        {
            Name = name,
            Host = "example.invalid",
            UserName = "deploy",
            PrivateKeyPath = "id_ed25519",
            OsFamily = osFamily,
            PackageManager = packageManager,
            Mode = mode,
            Capabilities = capabilities ?? PolicySet.Empty,
            EnvironmentValues = environmentValues ?? [],
        };
    }

    private static SshCommandService CreateProviderBackedService(ISshCommandRunner runner)
    {
        return new SshCommandService(CommandProcessingProviderCatalog.CreateDefault(), runner);
    }

    private static SshConnectionProfile CreatePasswordProfile(string name, string passwordSecretName)
    {
        return new SshConnectionProfile
        {
            Name = name,
            Host = "example.invalid",
            UserName = "deploy",
            AuthenticationMethod = "password",
            PasswordSecretName = passwordSecretName,
            OsFamily = "debian",
            PackageManager = "apt",
            Mode = KelpiePolicyMode.Safe,
            Capabilities = PolicySet.Empty,
            EnvironmentValues = [],
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kelpie-profiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateProfileJson(string userName)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid"
          },
          "Auth": {
            "UserName": "{{userName}}",
            "Method": "privateKey",
            "PrivateKeyFile": "id_ed25519"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithEnvironmentPolicy(string userName)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid"
          },
          "Auth": {
            "UserName": "{{userName}}",
            "Method": "privateKey",
            "PrivateKeyFile": "id_ed25519"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "Capabilities": "AllowPeekEnvironmentKeys|AllowSetEnvironmentValues",
          "EnvironmentValues": {
            "APP_ENV": "Common",
            "SECRET_TOKEN": "Hidden"
          }
        }
        """;
    }

    private sealed class FakeSshCommandRunner : ISshCommandRunner
    {
        private readonly Queue<FakeSshCommandOutput> _outputs;

        public FakeSshCommandRunner(
            string standardOutput = "ok\n\u001b[31mERROR\u001b[0m",
            string standardError = "warning")
        {
            _outputs = new Queue<FakeSshCommandOutput>(
            [
                new FakeSshCommandOutput(standardOutput, standardError),
            ]);
        }

        public FakeSshCommandRunner(IEnumerable<FakeSshCommandOutput> outputs)
        {
            _outputs = new Queue<FakeSshCommandOutput>(outputs);
        }

        public SshCommandRequest? LastRequest { get; private set; }

        public IReadOnlyList<SshCommandRequest> Requests => _requests;

        private readonly List<SshCommandRequest> _requests = [];

        public Task<SshCommandResult> ExecuteAsync(
            SshCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            _requests.Add(request);
            var output = _outputs.Count > 0
                ? _outputs.Dequeue()
                : new FakeSshCommandOutput("ok\n\u001b[31mERROR\u001b[0m", "warning");

            return Task.FromResult(new SshCommandResult(
                request.CommandName,
                request.CommandText,
                output.ExitCode,
                output.StandardOutput,
                output.StandardError,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                TimedOut: false));
        }
    }

    private sealed record FakeSshCommandOutput(
        string StandardOutput,
        string StandardError,
        int ExitCode = 0);
}
