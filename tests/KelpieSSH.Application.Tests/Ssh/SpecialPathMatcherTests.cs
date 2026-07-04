using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SpecialPathMatcherTests
{
    [Fact]
    public void FindAction_ShouldResolveParentSegmentsBeforeMatching()
    {
        var action = SpecialPathMatcher.FindAction(
            "/var/www/../../etc/shadow",
            [new SpecialPathRule("/etc/shadow", SpecialPathAction.Deny)],
            "debian");

        action.Should().Be(SpecialPathAction.Deny);
    }
}
