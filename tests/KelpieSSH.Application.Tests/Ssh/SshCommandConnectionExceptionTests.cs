using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshCommandConnectionExceptionTests
{
    [Theory]
    [InlineData(SshConnectionFailureKind.ConnectionFailed, "SSH connection failed. Verify the SSH profile and host key settings.")]
    [InlineData(SshConnectionFailureKind.HostUnreachable, "SSH host is unreachable. Verify the host, port, and network path.")]
    [InlineData(SshConnectionFailureKind.Timeout, "SSH connection timed out. Verify the host, port, and connection timeout.")]
    [InlineData(SshConnectionFailureKind.AuthenticationFailed, "SSH authentication failed. Verify the configured user and credentials.")]
    public void SharedConstructor_ShouldUseSanitizedMessage(
        SshConnectionFailureKind failureKind,
        string expectedMessage)
    {
        var exception = new SshConnectionFailureException(failureKind, new Exception("secret detail"));

        exception.Message.Should().Be(expectedMessage);
        exception.Message.Should().NotContain("secret detail");
        exception.FailureKind.Should().Be(failureKind);
    }

    [Theory]
    [InlineData(SshConnectionFailureKind.ConnectionFailed, false)]
    [InlineData(SshConnectionFailureKind.HostUnreachable, false)]
    [InlineData(SshConnectionFailureKind.Timeout, true)]
    [InlineData(SshConnectionFailureKind.AuthenticationFailed, false)]
    public void Constructor_ShouldExposeFailureKindAndCompatibleTimeoutFlag(
        SshConnectionFailureKind failureKind,
        bool expectedTimedOut)
    {
        var exception = new SshCommandConnectionException("sanitized", failureKind);

        exception.FailureKind.Should().Be(failureKind);
        exception.TimedOut.Should().Be(expectedTimedOut);
    }

    [Theory]
    [InlineData(false, SshConnectionFailureKind.ConnectionFailed)]
    [InlineData(true, SshConnectionFailureKind.Timeout)]
    public void LegacyConstructor_ShouldMapTimeoutFlag(
        bool timedOut,
        SshConnectionFailureKind expectedFailureKind)
    {
        var exception = new SshCommandConnectionException("sanitized", timedOut);

        exception.FailureKind.Should().Be(expectedFailureKind);
    }
}
