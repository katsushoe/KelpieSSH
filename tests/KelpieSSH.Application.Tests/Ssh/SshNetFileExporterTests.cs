using FluentAssertions;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;
using NSubstitute;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshNetFileExporterTests
{
    [Fact]
    public void EnsureNoSymlinkComponents_WhenParentsAreSearchableWithoutListing_UsesDirectPathLookup()
    {
        var client = Substitute.For<ISftpClient>();
        var regularEntry = CreateEntry(isSymbolicLink: false);
        client.Get(Arg.Any<string>()).Returns(regularEntry);

        SshNetFileExporter.EnsureNoSymlinkComponents(client, "/restricted/private/manifest.json");

        client.Received(1).Get("/restricted");
        client.Received(1).Get("/restricted/private");
        client.Received(1).Get("/restricted/private/manifest.json");
        client.DidNotReceiveWithAnyArgs().ListDirectory(default!, default);
    }

    [Fact]
    public void EnsureNoSymlinkComponents_WhenAComponentIsSymlink_RejectsPath()
    {
        var client = Substitute.For<ISftpClient>();
        var regularEntry = CreateEntry(isSymbolicLink: false);
        var symbolicLinkEntry = CreateEntry(isSymbolicLink: true);
        client.Get("/restricted").Returns(regularEntry);
        client.Get("/restricted/link").Returns(symbolicLinkEntry);

        var action = () => SshNetFileExporter.EnsureNoSymlinkComponents(client, "/restricted/link/file.txt");

        action.Should().Throw<KelpiePolicyError>()
            .WithMessage("*rejects symlinks*");
        client.DidNotReceive().Get("/restricted/link/file.txt");
    }

    private static ISftpFile CreateEntry(bool isSymbolicLink)
    {
        var entry = Substitute.For<ISftpFile>();
        entry.IsSymbolicLink.Returns(isSymbolicLink);
        return entry;
    }
}
