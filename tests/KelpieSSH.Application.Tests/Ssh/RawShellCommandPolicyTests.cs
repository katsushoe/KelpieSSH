using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class RawShellCommandPolicyTests
{
    [Theory]
    [InlineData("pwd & id")]
    [InlineData("awk '{print $1}' /etc/passwd")]
    [InlineData("find /var/log -type f")]
    [InlineData("sed -n 1p /etc/os-release")]
    [InlineData("systemctl status ssh")]
    [InlineData("ip addr show")]
    public void EnsureAllowed_ShouldRejectUnsafeRawShellReadOnlyBypass(string commandText)
    {
        var profile = CreateProfile();

        var action = () => RawShellCommandPolicy.Default.EnsureAllowed(
            profile,
            commandText,
            KelpieExecutionChannel.Mcp);

        action.Should().Throw<KelpiePolicyError>();
    }

    [Theory]
    [InlineData("rm /")]
    [InlineData("rm /*")]
    public void EnsureAllowed_ShouldRejectRootDeleteTargetsEvenWithGlobalWrite(string commandText)
    {
        var profile = CreateProfile(
            KelpiePolicyMode.Expert,
            [new AllowedRootRule("*", AllowedRootAccess.Write)]);

        var action = () => RawShellCommandPolicy.Default.EnsureAllowed(
            profile,
            commandText,
            KelpieExecutionChannel.Cli);

        action.Should().Throw<KelpiePolicyError>()
            .Which.Message.Should().Contain("rm target is forbidden:");
    }

    private static SshConnectionProfile CreateProfile(
        KelpiePolicyMode mode = KelpiePolicyMode.Safe,
        IReadOnlyCollection<AllowedRootRule>? allowedRootRules = null)
    {
        return new SshConnectionProfile
        {
            Name = "vps01",
            Host = "example.invalid",
            UserName = "deploy",
            PrivateKeyPath = "id_ed25519",
            OsFamily = "debian",
            PackageManager = "apt",
            Mode = mode,
            AllowedRootRules = allowedRootRules ?? [],
        };
    }
}
