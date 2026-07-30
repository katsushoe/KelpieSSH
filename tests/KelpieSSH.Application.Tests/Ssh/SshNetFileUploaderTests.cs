using System.Reflection;
using FluentAssertions;
using Renci.SshNet.Sftp;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshNetFileUploaderTests
{
    [Fact]
    public void SetPermissions_WhenModeIs600_ShouldApplyOwnerReadWriteOnly()
    {
        var constructor = typeof(SftpFileAttributes)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        var attributes = (SftpFileAttributes)constructor.Invoke(
            [DateTime.UnixEpoch, DateTime.UnixEpoch, 0L, 0, 0, 0u, null]);

        var action = () => attributes.SetPermissions(600);

        action.Should().NotThrow();
        attributes.OwnerCanRead.Should().BeTrue();
        attributes.OwnerCanWrite.Should().BeTrue();
        attributes.OwnerCanExecute.Should().BeFalse();
        attributes.GroupCanRead.Should().BeFalse();
        attributes.OthersCanRead.Should().BeFalse();
    }
}
