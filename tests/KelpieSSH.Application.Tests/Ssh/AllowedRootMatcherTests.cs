using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class AllowedRootMatcherTests
{
    [Theory]
    [InlineData("*")]
    [InlineData("**")]
    public void IsAllowed_ShouldTreatStandaloneWildcardAsFullAccess(string pattern)
    {
        var result = AllowedRootMatcher.IsAllowed(
            "/var/www/app",
            [pattern],
            "debian");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_ShouldAllowSingleSegmentWildcardAsRoot()
    {
        var result = AllowedRootMatcher.IsAllowed(
            "/home/deploy/app",
            ["/home/*"],
            "debian");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_ShouldRejectSingleSegmentWildcardOutsideRoot()
    {
        var result = AllowedRootMatcher.IsAllowed(
            "/home2/deploy/app",
            ["/home/*"],
            "debian");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_ShouldAllowDoubleSegmentWildcardBelowRoot()
    {
        var result = AllowedRootMatcher.IsAllowed(
            "/var/log/nginx/access.log",
            ["/var/**"],
            "debian");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_ShouldUseCaseInsensitiveWindowsMatching()
    {
        var result = AllowedRootMatcher.IsAllowed(
            "c:/users/deploy/app",
            ["C:/Users/*"],
            "windows");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_ShouldResolveParentSegmentsBeforeMatching()
    {
        var result = AllowedRootMatcher.IsAllowed(
            "/var/www/../../etc/shadow",
            ["/var/www"],
            "debian");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_ShouldAllowPathThatStaysUnderRootAfterParentResolution()
    {
        var result = AllowedRootMatcher.IsAllowed(
            "/var/www/site/../html/index.html",
            ["/var/www"],
            "debian");

        result.Should().BeTrue();
    }
}
