using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshHostKeyVerifierTests
{
    [Fact]
    public void IsTrusted_ShouldAcceptMatchingSha256Fingerprint()
    {
        var trusted = SshHostKeyVerifier.IsTrusted("SHA256:abc123=", "abc123");

        trusted.Should().BeTrue();
    }

    [Fact]
    public void IsTrusted_ShouldRejectMismatchedSha256Fingerprint()
    {
        var trusted = SshHostKeyVerifier.IsTrusted("SHA256:abc123", "def456");

        trusted.Should().BeFalse();
    }

    [Fact]
    public void HasPinnedFingerprint_ShouldReturnFalseForMissingFingerprint()
    {
        SshHostKeyVerifier.HasPinnedFingerprint(null).Should().BeFalse();
        SshHostKeyVerifier.HasPinnedFingerprint(" ").Should().BeFalse();
    }

    [Fact]
    public void HasPinnedFingerprint_ShouldReturnTrueForConfiguredFingerprint()
    {
        SshHostKeyVerifier.HasPinnedFingerprint("SHA256:abc123").Should().BeTrue();
    }
}
