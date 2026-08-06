using System.Globalization;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    private static SshDiskUsageData? ParseSshDiskUsage(string commandName, string output)
    {
        if (!string.Equals(commandName, "get_disk_usage", StringComparison.Ordinal))
        {
            return null;
        }

        var items = SplitOutputLines(output)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split((char[]?)null, 6, StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Length == 6)
            .Select(fields => new SshDiskUsageItem(
                fields[0],
                fields[1],
                fields[2],
                fields[3],
                fields[4],
                fields[5]))
            .ToArray();

        return new SshDiskUsageData(items);
    }

    private static SshMemoryUsageData? ParseSshMemoryUsage(string commandName, string output)
    {
        if (!string.Equals(commandName, "get_memory_usage", StringComparison.Ordinal))
        {
            return null;
        }

        var rows = SplitOutputLines(output)
            .Where(line => line.StartsWith("Mem:", StringComparison.Ordinal)
                || line.StartsWith("Swap:", StringComparison.Ordinal))
            .Select(ParseSshMemoryUsageRow)
            .Where(row => row is not null)
            .Cast<SshMemoryUsageItem>()
            .ToArray();

        return new SshMemoryUsageData(rows);
    }

    private static SshMemoryUsageItem? ParseSshMemoryUsageRow(string line)
    {
        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 4
            || !long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var totalMiB)
            || !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var usedMiB)
            || !long.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var freeMiB))
        {
            return null;
        }

        return new SshMemoryUsageItem(
            fields[0].TrimEnd(':'),
            totalMiB,
            usedMiB,
            freeMiB,
            ParseOptionalLong(fields, 4),
            ParseOptionalLong(fields, 5),
            ParseOptionalLong(fields, 6));
    }

    private static long? ParseOptionalLong(string[] fields, int index)
    {
        return index < fields.Length
            && long.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
    }

    private static SshListeningPortsData? ParseSshListeningPorts(string commandName, string output)
    {
        if (!string.Equals(commandName, "get_listening_ports", StringComparison.Ordinal))
        {
            return null;
        }

        var items = SplitOutputLines(output)
            .Where(line => !string.IsNullOrWhiteSpace(line)
                && !line.StartsWith("Netid ", StringComparison.Ordinal))
            .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Length >= 6)
            .Select(fields => new SshListeningPortItem(
                fields[0],
                fields[1],
                fields[4],
                fields[5]))
            .ToArray();

        return new SshListeningPortsData(items);
    }

    public sealed record SshDiskUsageData(SshDiskUsageItem[] Filesystems);

    public sealed record SshDiskUsageItem(
        string Filesystem,
        string Size,
        string Used,
        string Available,
        string UsePercent,
        string MountPoint);

    public sealed record SshMemoryUsageData(SshMemoryUsageItem[] Rows);

    public sealed record SshMemoryUsageItem(
        string Kind,
        long TotalMiB,
        long UsedMiB,
        long FreeMiB,
        long? SharedMiB,
        long? CacheMiB,
        long? AvailableMiB);

    public sealed record SshListeningPortsData(SshListeningPortItem[] Ports);

    public sealed record SshListeningPortItem(
        string Protocol,
        string State,
        string LocalEndpoint,
        string PeerEndpoint);
}
