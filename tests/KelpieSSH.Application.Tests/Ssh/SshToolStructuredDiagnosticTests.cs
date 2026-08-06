using FluentAssertions;
using KelpieMCPServer;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshToolStructuredDiagnosticTests
{
    [Fact]
    public void Data_WhenDiskUsageCommand_ShouldExposeFilesystems()
    {
        var result = CreateResult(
            "get_disk_usage",
            "Filesystem Size Used Avail Use% Mounted on\n/dev/sda1 40G 12G 26G 32% /\nmalformed\ntmpfs 1.0G 0 1.0G 0% /run\n");

        var data = result.Data;

        data.Should().NotBeNull();
        data!.DiskUsage.Should().NotBeNull();
        data.DiskUsage!.Filesystems.Should().HaveCount(2);
        data.DiskUsage.Filesystems[0].Should().BeEquivalentTo(
            new KelpieTools.SshDiskUsageItem("/dev/sda1", "40G", "12G", "26G", "32%", "/"));
        data.StandardOutput.Should().Contain("malformed");
        data.MemoryUsage.Should().BeNull();
        data.ListeningPorts.Should().BeNull();
    }

    [Fact]
    public void Data_WhenMemoryUsageCommand_ShouldExposeMemoryRows()
    {
        var result = CreateResult(
            "get_memory_usage",
            "               total used free shared buff/cache available\nMem: 15989 4102 2170 341 9716 11122\nSwap: 2047 0 2047\n");

        var data = result.Data;

        data.Should().NotBeNull();
        data!.MemoryUsage.Should().NotBeNull();
        data.MemoryUsage!.Rows.Should().HaveCount(2);
        data.MemoryUsage.Rows[0].Should().BeEquivalentTo(
            new KelpieTools.SshMemoryUsageItem("Mem", 15989, 4102, 2170, 341, 9716, 11122));
        data.MemoryUsage.Rows[1].Should().BeEquivalentTo(
            new KelpieTools.SshMemoryUsageItem("Swap", 2047, 0, 2047, null, null, null));
    }

    [Fact]
    public void Data_WhenListeningPortsCommand_ShouldExposeEndpoints()
    {
        var result = CreateResult(
            "get_listening_ports",
            "Netid State Recv-Q Send-Q Local Address:Port Peer Address:Port\ntcp LISTEN 0 128 0.0.0.0:22 0.0.0.0:*\nudp UNCONN 0 0 127.0.0.53:53 0.0.0.0:*\n");

        var data = result.Data;

        data.Should().NotBeNull();
        data!.ListeningPorts.Should().NotBeNull();
        data.ListeningPorts!.Ports.Should().HaveCount(2);
        data.ListeningPorts.Ports[0].Should().BeEquivalentTo(
            new KelpieTools.SshListeningPortItem("tcp", "LISTEN", "0.0.0.0:22", "0.0.0.0:*"));
    }

    private static KelpieTools.SshToolResult CreateResult(string commandName, string standardOutput)
    {
        var completedAt = DateTimeOffset.UtcNow;
        return new KelpieTools.SshToolResult(
            "vps01",
            "example.invalid",
            22,
            "deploy",
            commandName,
            "test-command",
            0,
            standardOutput,
            string.Empty,
            standardOutput.Split('\n'),
            [],
            standardOutput.Split('\n'),
            [],
            completedAt,
            completedAt,
            TimedOut: false);
    }
}
