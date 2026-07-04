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

    private static SshConnectionProfile CreateProfile()
    {
        return new SshConnectionProfile
        {
            Name = "vps01",
            Host = "example.invalid",
            UserName = "deploy",
            PrivateKeyPath = "id_ed25519",
            OsFamily = "debian",
            PackageManager = "apt",
            Mode = KelpiePolicyMode.Safe,
        };
    }
}
