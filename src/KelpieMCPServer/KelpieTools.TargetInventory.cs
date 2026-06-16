using System.ComponentModel;
using System.Text.RegularExpressions;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    private const string InventoryStatusAvailable = "Available";
    private const string InventoryStatusNotAvailable = "Not Available";

    /// <summary>
    /// Returns target OS, helper, and software inventory for a profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The target inventory result.</returns>
    [McpServerTool(Name = "get_target_inventory")]
    [Description("Returns read-only OS, helper, and software inventory for a configured SSH profile.")]
    public static async Task<TargetInventoryResult> GetTargetInventoryAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: get_target_inventory profile={profileName}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        var operation = SshRemoteOperation.FromProfile(
            profile,
            "managed",
            "target_inventory",
            correlationId: profileName);
        var result = await sshCommandService.ExecuteAsync(
            operation,
            KelpieExecutionChannel.Mcp,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            var detail = FirstInventoryDetail(result.StandardError)
                ?? FirstInventoryDetail(result.StandardOutput)
                ?? $"exit code {result.ExitCode}";
            throw new InvalidOperationException($"Target inventory probe failed: {detail}");
        }

        return ParseTargetInventory(profile, result.StandardOutput);
    }

    private static TargetInventoryResult ParseTargetInventory(
        SshConnectionProfile profile,
        string standardOutput)
    {
        var osName = string.Empty;
        var osVersion = string.Empty;
        var helpers = new List<TargetInventoryItem>();
        var software = new List<TargetInventoryItem>();

        foreach (var line in SplitInventoryLines(standardOutput))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("OS\t", StringComparison.Ordinal))
            {
                var parts = SplitInventoryLine(line, 4);
                osName = parts.Length > 1 ? parts[1] : string.Empty;
                osVersion = parts.Length > 2 ? parts[2] : string.Empty;
                continue;
            }

            if (!line.StartsWith("ITEM\t", StringComparison.Ordinal))
            {
                continue;
            }

            var itemParts = SplitInventoryLine(line, 6);
            if (itemParts.Length < 6)
            {
                continue;
            }

            var category = itemParts[1];
            var name = itemParts[2];
            var executable = itemParts[3];
            var exitCode = int.TryParse(itemParts[4], out var parsedExitCode)
                ? parsedExitCode
                : -1;
            var detail = itemParts[5];
            var status = exitCode == 0 ? InventoryStatusAvailable : InventoryStatusNotAvailable;
            var version = exitCode == 0 ? ExtractInventoryVersion(detail) : string.Empty;
            var item = new TargetInventoryItem(
                name,
                executable,
                status,
                version,
                detail,
                exitCode);

            if (category.Equals("helper", StringComparison.OrdinalIgnoreCase))
            {
                helpers.Add(item);
            }
            else if (category.Equals("software", StringComparison.OrdinalIgnoreCase))
            {
                software.Add(item);
            }
        }

        var os = new TargetInventoryOs(
            profile.OsFamily,
            osName,
            osVersion,
            profile.PackageManager);

        return new TargetInventoryResult(
            profile.Name,
            os,
            helpers.ToArray(),
            software.ToArray());
    }

    private static string? FirstInventoryDetail(string value)
    {
        return SplitInventoryLines(value)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
    }

    private static string[] SplitInventoryLines(string value)
    {
        return value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[] SplitInventoryLine(string line, int count)
    {
        var parts = new List<string>(count);
        var remaining = line.AsSpan();

        for (var index = 1; index < count; index++)
        {
            var tabIndex = remaining.IndexOf('\t');
            if (tabIndex < 0)
            {
                break;
            }

            parts.Add(remaining[..tabIndex].ToString());
            remaining = remaining[(tabIndex + 1)..];
        }

        parts.Add(remaining.ToString());
        return parts.ToArray();
    }

    private static string ExtractInventoryVersion(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return string.Empty;
        }

        var match = InventoryVersionRegex().Match(detail);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    [GeneratedRegex(@"\b[vV]?(\d+(?:\.\d+){0,3})(?:[-+~][A-Za-z0-9_.-]+)?\b", RegexOptions.CultureInvariant)]
    private static partial Regex InventoryVersionRegex();

    /// <summary>
    /// Represents target OS, helper, and software inventory.
    /// </summary>
    public sealed record TargetInventoryResult(
        string Profile,
        TargetInventoryOs Os,
        TargetInventoryItem[] Helpers,
        TargetInventoryItem[] Software);

    /// <summary>
    /// Represents target OS inventory.
    /// </summary>
    public sealed record TargetInventoryOs(
        string Family,
        string Name,
        string Version,
        string PackageManager);

    /// <summary>
    /// Represents one helper or software inventory item.
    /// </summary>
    public sealed record TargetInventoryItem(
        string Name,
        string Executable,
        string Status,
        string Version,
        string Detail,
        int ExitCode);
}
