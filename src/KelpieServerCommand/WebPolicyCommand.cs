using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kelpie.Core;

namespace KelpieServerCommand;

/// <summary>
/// Provides the human-only managed web permission policy command.
/// </summary>
public static partial class WebPolicyCommand
{
    private const string DefaultPolicyPath = "/etc/kelpie/web-permission-helper-policy.json";
    private const string DefaultAuditPath = "/var/log/kelpie/web-policy-audit.jsonl";
    private const string BackupDirectoryName = ".web-policy-backups";
    private static readonly UTF8Encoding Utf8NoBom = new(false, true);
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Runs a web-policy subcommand.
    /// </summary>
    /// <param name="args">Arguments after <c>web-policy</c>.</param>
    /// <param name="interaction">Optional interaction adapter for tests.</param>
    /// <param name="policyPath">Optional policy path for tests.</param>
    /// <param name="auditPath">Optional audit path for tests.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(
        IReadOnlyList<string> args,
        IWebPolicyInteraction? interaction = null,
        string? policyPath = null,
        string? auditPath = null)
    {
        interaction ??= new ConsoleWebPolicyInteraction();
        policyPath ??= DefaultPolicyPath;
        auditPath ??= DefaultAuditPath;

        try
        {
            return args.Count == 0
                ? WriteUsage(interaction.Error)
                : args[0].ToLowerInvariant() switch
                {
                    "list" => List(args.Skip(1).ToArray(), interaction, policyPath),
                    "add" => Change(args.Skip(1).ToArray(), interaction, policyPath, auditPath, add: true),
                    "remove" => Change(args.Skip(1).ToArray(), interaction, policyPath, auditPath, add: false),
                    "rollback" => Rollback(args.Skip(1).ToArray(), interaction, policyPath, auditPath),
                    _ => WriteUsage(interaction.Error),
                };
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidOperationException)
        {
            interaction.Error.WriteLine("ERROR: " + ex.Message);
            KpLog.Err("web-policy failed.", ex);
            return 1;
        }
    }

    private static int List(
        IReadOnlyList<string> args,
        IWebPolicyInteraction interaction,
        string policyPath)
    {
        if (args.Count > 1)
        {
            return WriteUsage(interaction.Error);
        }

        var root = LoadAndValidate(policyPath);
        var siteFilter = args.Count == 1 ? ValidateSiteRoot(args[0]) : null;
        var sites = root["Sites"]!.AsObject();
        var count = 0;
        foreach (var site in sites.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (siteFilter is not null && !string.Equals(site.Key, siteFilter, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var file in site.Value!["AllowedFiles"]!.AsObject()
                         .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                interaction.Output.WriteLine($"{site.Key}  {file.Key}  {file.Value!.GetValue<string>()}");
                count++;
            }
        }

        if (count == 0)
        {
            interaction.Output.WriteLine("(none)");
        }

        return 0;
    }

    private static int Change(
        IReadOnlyList<string> args,
        IWebPolicyInteraction interaction,
        string policyPath,
        string auditPath,
        bool add)
    {
        if ((add && args.Count != 3) || (!add && args.Count != 2))
        {
            return WriteUsage(interaction.Error);
        }

        EnsureHumanInteractive(interaction);
        EnsureRootOwnedPolicy(policyPath);
        var siteRoot = ValidateSiteRoot(args[0]);
        var filePath = ValidateFilePath(args[1]);
        var access = add ? ValidateAccess(args[2]) : string.Empty;
        var root = LoadAndValidate(policyPath);
        var before = Serialize(root);
        var sites = root["Sites"]!.AsObject();

        if (add)
        {
            var site = GetOrCreateSite(sites, siteRoot);
            var files = site["AllowedFiles"]!.AsObject();
            if (files.ContainsKey(filePath))
            {
                throw new InvalidOperationException("The web policy entry already exists.");
            }

            files.Add(filePath, access);
        }
        else
        {
            if (!sites.TryGetPropertyValue(siteRoot, out var siteNode)
                || siteNode is null
                || !siteNode["AllowedFiles"]!.AsObject().Remove(filePath))
            {
                throw new InvalidOperationException("The web policy entry does not exist.");
            }

            if (siteNode["AllowedFiles"]!.AsObject().Count == 0)
            {
                sites.Remove(siteRoot);
            }
        }

        ValidateRoot(root);
        var after = Serialize(root);
        var operation = add ? "add" : "remove";
        ConfirmChange(interaction, before, after);
        Apply(policyPath, auditPath, before, after, operation, siteRoot, filePath);
        interaction.Output.WriteLine($"Web policy {operation} completed.");
        return 0;
    }

    private static int Rollback(
        IReadOnlyList<string> args,
        IWebPolicyInteraction interaction,
        string policyPath,
        string auditPath)
    {
        if (args.Count != 0)
        {
            return WriteUsage(interaction.Error);
        }

        EnsureHumanInteractive(interaction);
        EnsureRootOwnedPolicy(policyPath);
        var beforeRoot = LoadAndValidate(policyPath);
        var backupPath = GetLatestBackupPath(policyPath)
            ?? throw new InvalidOperationException("No web policy backup is available.");
        var afterRoot = LoadAndValidate(backupPath);
        var before = Serialize(beforeRoot);
        var after = Serialize(afterRoot);
        ConfirmChange(interaction, before, after);
        Apply(policyPath, auditPath, before, after, "rollback", string.Empty, string.Empty);
        interaction.Output.WriteLine("Web policy rollback completed.");
        return 0;
    }

    private static void Apply(
        string policyPath,
        string auditPath,
        string before,
        string after,
        string operation,
        string siteRoot,
        string filePath)
    {
        var current = Serialize(LoadAndValidate(policyPath));
        if (!string.Equals(current, before, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Web policy changed after the difference was displayed. No change was made.");
        }

        var metadata = ReadMetadata(policyPath);
        var directory = Path.GetDirectoryName(policyPath)
            ?? throw new InvalidOperationException("Policy directory is not available.");
        var backupDirectory = Path.Combine(directory, BackupDirectoryName);
        Directory.CreateDirectory(backupDirectory);
        SetSecureDirectoryMode(backupDirectory);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ");
        var backupPath = Path.Combine(
            backupDirectory,
            Path.GetFileName(policyPath) + "." + stamp + "." + Guid.NewGuid().ToString("N") + ".bak");

        AppendAudit(auditPath, operation, "confirmed", siteRoot, filePath, policyPath, null);
        WriteNewFileWithMetadata(backupPath, before, metadata);
        AtomicWrite(policyPath, after, metadata);
        AppendAudit(auditPath, operation, "completed", siteRoot, filePath, policyPath, backupPath);
        KpLog.Warn($"Human web-policy change completed. operation={operation} site={siteRoot} path={filePath}");
    }

    private static void AtomicWrite(string policyPath, string content, UnixMetadata metadata)
    {
        var directory = Path.GetDirectoryName(policyPath)!;
        var tempPath = Path.Combine(directory, "." + Path.GetFileName(policyPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            WriteNewFileWithMetadata(tempPath, content, metadata);
            _ = LoadAndValidate(tempPath);
            File.Move(tempPath, policyPath, overwrite: true);
            var replaced = ReadMetadata(policyPath);
            if (replaced != metadata)
            {
                throw new InvalidOperationException("Policy owner or mode was not preserved.");
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static void WriteNewFileWithMetadata(string path, string content, UnixMetadata metadata)
    {
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            var bytes = Utf8NoBom.GetBytes(content);
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, metadata.Mode);
            if (chown(path, metadata.Uid, metadata.Gid) != 0)
            {
                throw new IOException("Failed to preserve policy ownership.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }
        }
    }

    private static void ConfirmChange(IWebPolicyInteraction interaction, string before, string after)
    {
        interaction.Output.WriteLine("--- current");
        interaction.Output.WriteLine("+++ proposed");
        foreach (var line in before.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            interaction.Output.WriteLine("-" + line);
        }

        foreach (var line in after.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            interaction.Output.WriteLine("+" + line);
        }

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        interaction.Output.Write($"Type confirmation code {code}: ");
        var entered = interaction.ReadLine();
        if (!string.Equals(entered, code, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Confirmation code did not match. No change was made.");
        }
    }

    private static JsonObject LoadAndValidate(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Web policy file was not found: {path}");
        }

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Web policy file must not be a symbolic link.");
        }

        var bytes = File.ReadAllBytes(path);
        var root = JsonNode.Parse(bytes, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
        }) as JsonObject ?? throw new InvalidOperationException("Web policy root must be an object.");
        ValidateRoot(root);
        return root;
    }

    private static void ValidateRoot(JsonObject root)
    {
        if (root.Count != 1 || root["Sites"] is not JsonObject sites)
        {
            throw new InvalidOperationException("Web policy must contain only a Sites object.");
        }

        foreach (var site in sites)
        {
            _ = ValidateSiteRoot(site.Key);
            if (site.Value is not JsonObject siteObject
                || siteObject.Count != 1
                || siteObject["AllowedFiles"] is not JsonObject files)
            {
                throw new InvalidOperationException($"Web policy site must contain only AllowedFiles: {site.Key}");
            }

            foreach (var file in files)
            {
                _ = ValidateFilePath(file.Key);
                if (file.Value is not JsonValue value
                    || !value.TryGetValue<string>(out var access))
                {
                    throw new InvalidOperationException($"Web policy access must be a string: {file.Key}");
                }

                _ = ValidateAccess(access);
            }
        }
    }

    private static JsonObject GetOrCreateSite(JsonObject sites, string siteRoot)
    {
        if (sites.TryGetPropertyValue(siteRoot, out var existing))
        {
            return existing!.AsObject();
        }

        var site = new JsonObject { ["AllowedFiles"] = new JsonObject() };
        sites.Add(siteRoot, site);
        return site;
    }

    private static string ValidateSiteRoot(string value)
    {
        if (!SafeAbsoluteUnixPathRegex().IsMatch(value) || value.EndsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Site root must be a normalized absolute Unix path.");
        }

        return value;
    }

    private static string ValidateFilePath(string value)
    {
        if (!SafeAbsoluteUnixPathRegex().IsMatch(value)
            || value == "/"
            || value.EndsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("File path must be a normalized absolute site-relative Unix file path.");
        }

        return value;
    }

    private static string ValidateAccess(string value)
    {
        return value is "Update" or "Create"
            ? value
            : throw new InvalidOperationException("Access must be Update or Create.");
    }

    private static string Serialize(JsonObject root)
    {
        return root.ToJsonString(PrettyJson) + Environment.NewLine;
    }

    private static void EnsureHumanInteractive(IWebPolicyInteraction interaction)
    {
        if (!interaction.IsInteractive)
        {
            throw new InvalidOperationException("web-policy changes require an interactive human terminal.");
        }

        if (OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("web-policy changes are supported on Unix only.");
        }
    }

    private static void EnsureRootOwnedPolicy(string policyPath)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("web-policy is supported on Unix only.");
        }

        if (geteuid() != 0)
        {
            throw new InvalidOperationException("web-policy changes must run as root.");
        }

        var metadata = ReadMetadata(policyPath);
        if (metadata.Uid != 0 || metadata.Gid != 0)
        {
            throw new InvalidOperationException("Web policy must be owned by root:root.");
        }

        if ((metadata.Mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
        {
            throw new InvalidOperationException("Web policy must not be writable by group or others.");
        }
    }

    private static UnixMetadata ReadMetadata(string path)
    {
        var mode = OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(path);
        if (OperatingSystem.IsWindows())
        {
            return new UnixMetadata(0, 0, mode);
        }

        const int atFdcwd = -100;
        const int atSymlinkNoFollow = 0x100;
        const uint statxBasicStats = 0x7ff;
        if (statx(atFdcwd, path, atSymlinkNoFollow, statxBasicStats, out var status) != 0)
        {
            throw new IOException("Failed to inspect policy ownership.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
        }

        return new UnixMetadata(status.Uid, status.Gid, mode);
    }

    private static string? GetLatestBackupPath(string policyPath)
    {
        var directory = Path.Combine(Path.GetDirectoryName(policyPath)!, BackupDirectoryName);
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, Path.GetFileName(policyPath) + ".*.bak")
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .FirstOrDefault()
            : null;
    }

    private static void AppendAudit(
        string auditPath,
        string operation,
        string status,
        string siteRoot,
        string filePath,
        string policyPath,
        string? backupPath)
    {
        var directory = Path.GetDirectoryName(auditPath)
            ?? throw new InvalidOperationException("Audit log directory is not available.");
        var directoryExisted = Directory.Exists(directory);
        Directory.CreateDirectory(directory);
        if (!directoryExisted)
        {
            SetSecureDirectoryMode(directory);
        }

        if (File.Exists(auditPath))
        {
            if ((File.GetAttributes(auditPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Web policy audit log must not be a symbolic link.");
            }

            var auditMetadata = ReadMetadata(auditPath);
            if (auditMetadata.Uid != 0
                || (auditMetadata.Mode & (UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
            {
                throw new InvalidOperationException("Web policy audit log has insecure ownership or mode.");
            }
        }

        var entry = JsonSerializer.Serialize(new
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Operation = operation,
            Status = status,
            SiteRoot = siteRoot,
            FilePath = filePath,
            PolicyPath = policyPath,
            BackupPath = backupPath,
            ProcessId = Environment.ProcessId,
        });
        using var stream = new FileStream(auditPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        var bytes = Utf8NoBom.GetBytes(entry + "\n");
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(auditPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            if (chown(auditPath, 0, 0) != 0)
            {
                throw new IOException("Failed to secure the web policy audit log.");
            }
        }
    }

    private static void SetSecureDirectoryMode(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private static int WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  kelpiemcp web-policy list [<site-root>]");
        writer.WriteLine("  kelpiemcp web-policy add <site-root> <file-path> <Update|Create>");
        writer.WriteLine("  kelpiemcp web-policy remove <site-root> <file-path>");
        writer.WriteLine("  kelpiemcp web-policy rollback");
        return 1;
    }

    [GeneratedRegex(@"^/(?:[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeAbsoluteUnixPathRegex();

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();

    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int chown(string path, uint owner, uint group);

    [DllImport("libc", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int statx(int directoryFileDescriptor, string path, int flags, uint mask, out LinuxStatx status);

    [StructLayout(LayoutKind.Sequential, Size = 256)]
    private struct LinuxStatx
    {
        public uint Mask;
        public uint BlockSize;
        public ulong Attributes;
        public uint LinkCount;
        public uint Uid;
        public uint Gid;
        public ushort Mode;
        private readonly ushort Spare0;
    }

    private sealed record UnixMetadata(uint Uid, uint Gid, UnixFileMode Mode);
}

/// <summary>
/// Abstracts the terminal used by the human-only web-policy command.
/// </summary>
public interface IWebPolicyInteraction
{
    /// <summary>Gets whether the input and output are attached to a human terminal.</summary>
    bool IsInteractive { get; }

    /// <summary>Gets standard output.</summary>
    TextWriter Output { get; }

    /// <summary>Gets standard error.</summary>
    TextWriter Error { get; }

    /// <summary>Reads one confirmation line.</summary>
    string? ReadLine();
}

internal sealed class ConsoleWebPolicyInteraction : IWebPolicyInteraction
{
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public TextWriter Output => Console.Out;

    public TextWriter Error => Console.Error;

    public string? ReadLine()
    {
        return Console.ReadLine();
    }
}
