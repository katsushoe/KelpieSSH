using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class KelpiePolicyEvaluatorTests
{
    [Fact]
    public void EnsureAllowed_ShouldAllowMaintenancePackageInstall()
    {
        var evaluator = KelpiePolicyEvaluator.Default;
        var profile = CreateProfile(KelpiePolicyMode.Maintenance, PolicySet.Empty);
        var command = CreateCommand("pkg_install", "apt-get install nginx", SshCommandRiskLevel.ConfirmRequired);

        var action = () => evaluator.EnsureAllowed(profile, command, command.CommandTemplate, KelpieExecutionChannel.Mcp);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnsureAllowed_ShouldUseCapabilitiesForCli()
    {
        var evaluator = KelpiePolicyEvaluator.Default;
        var profile = CreateProfile(
            KelpiePolicyMode.Safe,
            PolicySet.FromNames([KelpiePolicyNames.AllowInstallPackage]));
        var command = CreateCommand("pkg_install", "apt-get install nginx", SshCommandRiskLevel.ConfirmRequired);

        var action = () => evaluator.EnsureAllowed(profile, command, command.CommandTemplate, KelpieExecutionChannel.Cli);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnsureAllowed_ShouldIgnoreCapabilitiesForMcp()
    {
        var evaluator = KelpiePolicyEvaluator.Default;
        var profile = CreateProfile(
            KelpiePolicyMode.Safe,
            PolicySet.FromNames([KelpiePolicyNames.AllowInstallPackage]));
        var command = CreateCommand("pkg_install", "apt-get install nginx", SshCommandRiskLevel.ConfirmRequired);

        var action = () => evaluator.EnsureAllowed(profile, command, command.CommandTemplate, KelpieExecutionChannel.Mcp);

        action.Should().Throw<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: AllowInstallPackage is required for command: pkg_install");
    }

    [Theory]
    [InlineData("pkg_install", "sudo -n env DEBIAN_FRONTEND=noninteractive apt-get install -y nginx")]
    [InlineData("pkg_remove", "sudo -n env DEBIAN_FRONTEND=noninteractive apt-get remove -y nginx")]
    public void EnsureAllowed_ShouldAllowSudoPackageMaintenanceThroughMcpInMaintenanceMode(
        string commandName,
        string commandText)
    {
        var evaluator = KelpiePolicyEvaluator.Default;
        var profile = CreateProfile(KelpiePolicyMode.Maintenance, PolicySet.Empty);
        var command = CreateCommand(commandName, commandText, SshCommandRiskLevel.ConfirmRequired);

        var action = () => evaluator.EnsureAllowed(profile, command, command.CommandTemplate, KelpieExecutionChannel.Mcp);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(KelpiePolicyMode.ReadOnly)]
    [InlineData(KelpiePolicyMode.Safe)]
    public void EnsureAllowed_ShouldRejectSudoPackageInstallThroughMcpWithoutMaintenanceOrExpertMode(KelpiePolicyMode mode)
    {
        var evaluator = KelpiePolicyEvaluator.Default;
        var profile = CreateProfile(mode, PolicySet.Empty);
        var command = CreateCommand("pkg_install", "sudo -n dnf install -y nginx", SshCommandRiskLevel.ConfirmRequired);

        var action = () => evaluator.EnsureAllowed(profile, command, command.CommandTemplate, KelpieExecutionChannel.Mcp);

        action.Should().Throw<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: AllowSudo is required for command: pkg_install");
    }

    [Fact]
    public void EnsureAllowed_ShouldAllowSudoCommandThroughMcpInExpertMode()
    {
        var evaluator = KelpiePolicyEvaluator.Default;
        var profile = CreateProfile(KelpiePolicyMode.Expert, PolicySet.Empty);
        var command = CreateCommand("pkg_install", "sudo -n dnf install -y nginx", SshCommandRiskLevel.ConfirmRequired);

        var action = () => evaluator.EnsureAllowed(profile, command, command.CommandTemplate, KelpieExecutionChannel.Mcp);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnsureAllowed_ShouldRejectSecretDisplayThroughMcpEvenInExpertMode()
    {
        var evaluator = KelpiePolicyEvaluator.Default;
        var profile = CreateProfile(KelpiePolicyMode.Expert, PolicySet.Empty);
        var command = CreateCommand("show_private_key", "cat ~/.ssh/id_ed25519", SshCommandRiskLevel.ReadOnly);

        var action = () => evaluator.EnsureAllowed(profile, command, command.CommandTemplate, KelpieExecutionChannel.Mcp);

        action.Should().Throw<KelpiePolicyError>()
            .WithMessage("KelpiePolicyError: secrets cannot be displayed through MCP.");
    }

    [Fact]
    public void EnsureAllowed_ShouldAllowSecretDisplayThroughCliInExpertMode()
    {
        var evaluator = KelpiePolicyEvaluator.Default;
        var profile = CreateProfile(KelpiePolicyMode.Expert, PolicySet.Empty);
        var command = CreateCommand("show_private_key", "cat ~/.ssh/id_ed25519", SshCommandRiskLevel.ReadOnly);

        var action = () => evaluator.EnsureAllowed(profile, command, command.CommandTemplate, KelpieExecutionChannel.Cli);

        action.Should().NotThrow();
    }

    private static AllowedCommandDefinition CreateCommand(
        string name,
        string commandText,
        SshCommandRiskLevel riskLevel)
    {
        return new AllowedCommandDefinition(name, commandText, TimeSpan.FromSeconds(10), RiskLevel: riskLevel);
    }

    private static SshConnectionProfile CreateProfile(KelpiePolicyMode mode, PolicySet capabilities)
    {
        return new SshConnectionProfile
        {
            Name = "test",
            Host = "example.invalid",
            UserName = "deploy",
            PrivateKeyPath = "id_ed25519",
            OsFamily = "debian",
            PackageManager = "apt",
            Mode = mode,
            Capabilities = capabilities,
        };
    }
}
