using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshCommandRiskLevelTests
{
    [Fact]
    public void SshCommandRiskLevel_ShouldDefineExpectedLevels()
    {
        Enum.GetNames<SshCommandRiskLevel>().Should().BeEquivalentTo(
        [
            nameof(SshCommandRiskLevel.ReadOnly),
            nameof(SshCommandRiskLevel.ConfirmRequired),
            nameof(SshCommandRiskLevel.Forbidden),
        ]);
    }
}
