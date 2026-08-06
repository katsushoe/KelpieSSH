using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshCommandConnectionExceptionTests
{
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
