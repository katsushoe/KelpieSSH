using FluentAssertions;
using KelpieMCPServer;

namespace KelpieSSH.Application.Tests.McpServer;

public sealed class KelpieMcpGuidanceTests
{
    [Fact]
    public void ServerInstructions_ShouldDescribePurposeAndSafetyBoundaries()
    {
        KelpieMcpGuidance.ServerInstructions.Should().Contain("diagnose and maintain");
        KelpieMcpGuidance.ServerInstructions.Should().Contain("ssh_get_capabilities");
        KelpieMcpGuidance.ServerInstructions.Should().Contain("Never request, reveal, persist, or echo passwords");
        KelpieMcpGuidance.ServerInstructions.Should().Contain("Root login and unrestricted shell access are outside");
    }

    [Fact]
    public void GetStarted_WhenTaskIsProvided_ShouldIncludeTaskAndSafeWorkflow()
    {
        var result = KelpieMcpGuidance.GetStarted("Investigate disk pressure");

        result.Should().Contain("Operator goal: Investigate disk pressure");
        result.Should().Contain("get_target_inventory");
        result.Should().Contain("ssh_get_capabilities");
        result.Should().Contain("check, preview, or simulation tool");
        result.Should().Contain("Never attempt to bypass");
    }

    [Fact]
    public void GetStarted_WhenTaskIsBlank_ShouldAskForGoalWithoutIncludingBlankGoal()
    {
        var result = KelpieMcpGuidance.GetStarted("   ");

        result.Should().Contain("Determine the operator's diagnosis or maintenance goal.");
        result.Should().NotContain("Operator goal:");
    }
}
