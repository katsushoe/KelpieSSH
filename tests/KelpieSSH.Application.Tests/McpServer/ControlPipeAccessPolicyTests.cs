using FluentAssertions;
using KelpieMCPServer;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.McpServer;

public sealed class ControlPipeAccessPolicyTests
{
    private static readonly ControlPipeCaller CrossUser = new(
        "S-1-5-21-test",
        IsServerOwner: false,
        IsAdministrator: false,
        IsSystem: false);

    [Theory]
    [InlineData("ping")]
    [InlineData("sessions")]
    [InlineData("secret-list")]
    [InlineData("env-list {}")]
    [InlineData("profile-capabilities sample")]
    public void IsAllowed_ShouldAllowReferenceCommandsForCrossUser(string command)
    {
        ControlPipeAccessPolicy.IsAllowed(command, CrossUser).Should().BeTrue();
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("login sample")]
    [InlineData("secret-put {}")]
    [InlineData("env-clear {}")]
    [InlineData("profile-reload sample")]
    [InlineData("kill ssh-123")]
    public void IsAllowed_ShouldDenyUpdateCommandsForCrossUser(string command)
    {
        ControlPipeAccessPolicy.IsAllowed(command, CrossUser).Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_ShouldAllowEveryCommandForServerOwner()
    {
        var owner = CrossUser with { IsServerOwner = true };

        ControlPipeAccessPolicy.IsAllowed("stop", owner).Should().BeTrue();
        ControlPipeAccessPolicy.IsAllowed("secret-put {}", owner).Should().BeTrue();
    }

    [Fact]
    public void RedactSessions_ShouldHideHandlesProfilesAndSecretNames()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var sessions = new[]
        {
            new SshSessionInfo("ssh-secret", "prod", "password-ref", startedAt, "password"),
        };

        var result = ControlPipeAccessPolicy.RedactSessions(sessions).Should().ContainSingle().Which;

        result.Handle.Should().Be("(redacted)");
        result.ProfileName.Should().Be("(redacted)");
        result.SecretName.Should().BeEmpty();
        result.Kind.Should().Be("password");
        result.StartedAtUtc.Should().Be(startedAt);
    }

    [Fact]
    public void RedactSecretsAndEnvironment_ShouldHideNamesAndSizes()
    {
        var now = DateTimeOffset.UtcNow;

        var secret = ControlPipeAccessPolicy.RedactSecrets(
            [new KelpieSecretInfo("deploy-token", 42, now, now.AddMinutes(10))])
            .Should().ContainSingle().Which;
        var environment = ControlPipeAccessPolicy.RedactEnvironmentOverrides(
            [new KelpieEnvironmentOverrideInfo("prod", "TOKEN", 42, now)])
            .Should().ContainSingle().Which;

        secret.Name.Should().Be("(redacted)");
        secret.Size.Should().Be(0);
        environment.ProfileName.Should().Be("(redacted)");
        environment.Key.Should().Be("(redacted)");
        environment.ValueLength.Should().Be(0);
    }

    [Fact]
    public void RedactProfileCapabilities_ShouldNotRevealProfileOrPrivileges()
    {
        var result = ControlPipeAccessPolicy.RedactProfileCapabilities();

        result.ProfileName.Should().Be("(redacted)");
        result.AddAllowed.Should().BeFalse();
        result.ReloadAllowed.Should().BeFalse();
        result.RevokeAllowed.Should().BeFalse();
        result.Reason.Should().Be("redacted-for-cross-user");
    }
}
