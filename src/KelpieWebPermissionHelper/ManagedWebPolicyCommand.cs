using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KelpieWebPermissionHelper;

internal static partial class ManagedWebPolicyCommand
{
    private const string PolicyPath = "/etc/kelpie/web-permission-helper-policy.json";
    private const string BackupDirectory = "/etc/kelpie/.web-policy-backups";
    private const string AuditPath = "/var/log/kelpie/web-policy-audit.jsonl";
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };

    public static int Run(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter output,
        TextWriter error)
    {
        if (args.Count < 2)
        {
            return WriteError(error, "ERROR: policy action is required");
        }

        return args[1] switch
        {
            "list" => List(args.Skip(2).ToArray(), operations, output),
            "preview-add" => PreviewChange(args.Skip(2).ToArray(), operations, output, PolicyOperation.Add),
            "apply-add" => ApplyChange(args.Skip(2).ToArray(), operations, output, PolicyOperation.Add),
            "preview-remove" => PreviewChange(args.Skip(2).ToArray(), operations, output, PolicyOperation.Remove),
            "apply-remove" => ApplyChange(args.Skip(2).ToArray(), operations, output, PolicyOperation.Remove),
            "preview-rollback" => PreviewRollback(args.Skip(2).ToArray(), operations, output),
            "apply-rollback" => ApplyRollback(args.Skip(2).ToArray(), operations, output),
            _ => WriteError(error, "ERROR: unsupported policy action"),
        };
    }

    private static int List(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter output)
    {
        if (args.Count > 1)
        {
            throw new InvalidOperationException("policy list accepts at most one site root");
        }

        var root = LoadPolicy(operations);
        if (args.Count == 0)
        {
            output.WriteLine(Serialize(root));
            return 0;
        }

        var siteRoot = DecodePath(args[0], "siteRoot");
        var sites = root["Sites"]!.AsObject();
        var result = new JsonObject
        {
            ["Sites"] = sites.TryGetPropertyValue(siteRoot, out var site)
                ? new JsonObject { [siteRoot] = site?.DeepClone() }
                : new JsonObject(),
        };
        output.WriteLine(Serialize(result));
        return 0;
    }

    private static int PreviewChange(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter output,
        PolicyOperation operation)
    {
        var request = ParseChange(args, operation, requiresHash: false);
        var currentBytes = ReadSecurePolicy(operations);
        var current = ParseAndValidate(currentBytes);
        var proposed = CreateProposed(current, request, operation);
        WritePreview(output, currentBytes, Encoding.UTF8.GetBytes(Serialize(proposed)));
        return 0;
    }

    private static int ApplyChange(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter output,
        PolicyOperation operation)
    {
        var request = ParseChange(args, operation, requiresHash: true);
        var currentBytes = ReadSecurePolicy(operations);
        EnsureHash(currentBytes, request.ExpectedHash!);
        var current = ParseAndValidate(currentBytes);
        var proposedBytes = Encoding.UTF8.GetBytes(Serialize(CreateProposed(current, request, operation)));
        Apply(operations, operation.ToString().ToLowerInvariant(), request.SiteRoot, request.FilePath, currentBytes, proposedBytes);
        output.WriteLine("{\"changed\":true}");
        return 0;
    }

    private static int PreviewRollback(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter output)
    {
        if (args.Count != 0)
        {
            throw new InvalidOperationException("policy rollback does not accept extra arguments");
        }

        var current = ReadSecurePolicy(operations);
        var backup = ReadLatestBackup(operations);
        ParseAndValidate(backup.Content);
        WritePreview(output, current, backup.Content, backup.Path);
        return 0;
    }

    private static int ApplyRollback(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter output)
    {
        if (args.Count != 2)
        {
            throw new InvalidOperationException("policy rollback requires current hash and backup name");
        }

        var current = ReadSecurePolicy(operations);
        EnsureHash(current, args[0]);
        var backupName = DecodePath(args[1], "backupName");
        if (backupName.Contains('/', StringComparison.Ordinal) || !backupName.EndsWith(".json", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("backup name is invalid");
        }

        var latest = ReadLatestBackup(operations);
        if (!string.Equals(Path.GetFileName(latest.Path), backupName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("latest backup changed after preview");
        }

        ParseAndValidate(latest.Content);
        Apply(operations, "rollback", string.Empty, string.Empty, current, latest.Content);
        output.WriteLine("{\"changed\":true}");
        return 0;
    }

    private static void Apply(
        IUnixPermissionOperations operations,
        string operation,
        string siteRoot,
        string filePath,
        byte[] current,
        byte[] proposed)
    {
        var metadata = operations.GetOwnerIds(PolicyPath);
        var mode = Convert.ToUInt32(operations.GetMode(PolicyPath), 8);
        operations.CreateDirectory(BackupDirectory);
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ", System.Globalization.CultureInfo.InvariantCulture);
        var backupPath = BackupDirectory + "/web-policy-" + stamp + ".json";
        WriteWithMetadata(operations, backupPath, current, metadata.Uid, metadata.Gid, mode);

        AppendAudit(operations, operation, "confirmed", siteRoot, filePath);
        var temporaryPath = PolicyPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            WriteWithMetadata(operations, temporaryPath, proposed, metadata.Uid, metadata.Gid, mode);
            ParseAndValidate(operations.ReadAllBytes(temporaryPath));
            operations.MoveFileOverwrite(temporaryPath, PolicyPath);
        }
        finally
        {
            operations.DeleteFileIfExists(temporaryPath);
        }

        AppendAudit(operations, operation, "completed", siteRoot, filePath);
    }

    private static void AppendAudit(
        IUnixPermissionOperations operations,
        string operation,
        string state,
        string siteRoot,
        string filePath)
    {
        var directory = Path.GetDirectoryName(AuditPath)!;
        operations.CreateDirectory(directory);
        if (operations.FileExists(AuditPath))
        {
            if (!operations.IsRegularFile(AuditPath)
                || operations.IsSymbolicLink(AuditPath)
                || operations.GetOwnerIds(AuditPath).Uid != 0
                || !string.Equals(operations.GetMode(AuditPath), "600", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("policy audit log metadata is unsafe");
            }

        }
        var entry = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            operation,
            state,
            siteRoot,
            filePath,
        }) + "\n";
        operations.AppendAllText(AuditPath, entry);
        operations.ChangeOwner(AuditPath, 0, 0);
        operations.ChangeMode(AuditPath, 0x180);
    }

    private static void WriteWithMetadata(
        IUnixPermissionOperations operations,
        string path,
        byte[] content,
        uint uid,
        uint gid,
        uint mode)
    {
        operations.WriteAllBytes(path, content);
        operations.ChangeOwner(path, uid, gid);
        operations.ChangeMode(path, mode);
    }

    private static PolicyRequest ParseChange(
        IReadOnlyList<string> args,
        PolicyOperation operation,
        bool requiresHash)
    {
        var required = operation == PolicyOperation.Add ? 3 : 2;
        if (requiresHash)
        {
            required++;
        }

        if (args.Count != required)
        {
            throw new InvalidOperationException("policy change arguments are invalid");
        }

        var siteRoot = DecodePath(args[0], "siteRoot");
        var filePath = DecodePath(args[1], "filePath");
        EnsureSafeUnixPath(siteRoot, allowRoot: false);
        EnsureSafeUnixPath(filePath, allowRoot: false);
        var access = operation == PolicyOperation.Add ? args[2] : string.Empty;
        if (operation == PolicyOperation.Add
            && !string.Equals(access, "Update", StringComparison.Ordinal)
            && !string.Equals(access, "Create", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("access must be Update or Create");
        }

        var hashIndex = operation == PolicyOperation.Add ? 3 : 2;
        var hash = requiresHash ? args[hashIndex] : null;
        if (hash is not null && !Sha256Regex().IsMatch(hash))
        {
            throw new InvalidOperationException("current hash is invalid");
        }

        return new PolicyRequest(siteRoot, filePath, access, hash);
    }

    private static JsonObject CreateProposed(
        JsonObject current,
        PolicyRequest request,
        PolicyOperation operation)
    {
        var proposed = (JsonObject)current.DeepClone();
        var sites = proposed["Sites"]!.AsObject();
        if (operation == PolicyOperation.Add)
        {
            if (!sites.TryGetPropertyValue(request.SiteRoot, out var siteNode))
            {
                siteNode = new JsonObject { ["AllowedFiles"] = new JsonObject() };
                sites[request.SiteRoot] = siteNode;
            }

            var files = siteNode!.AsObject()["AllowedFiles"]!.AsObject();
            if (files.ContainsKey(request.FilePath))
            {
                throw new InvalidOperationException("policy entry already exists");
            }

            files[request.FilePath] = request.Access;
        }
        else
        {
            if (!sites.TryGetPropertyValue(request.SiteRoot, out var siteNode)
                || !siteNode!.AsObject()["AllowedFiles"]!.AsObject().Remove(request.FilePath))
            {
                throw new InvalidOperationException("policy entry was not found");
            }
        }

        Validate(proposed);
        return proposed;
    }

    private static JsonObject LoadPolicy(IUnixPermissionOperations operations)
    {
        return ParseAndValidate(ReadSecurePolicy(operations));
    }

    private static byte[] ReadSecurePolicy(IUnixPermissionOperations operations)
    {
        if (!operations.FileExists(PolicyPath)
            || !operations.IsRegularFile(PolicyPath)
            || operations.IsSymbolicLink(PolicyPath))
        {
            throw new InvalidOperationException("managed web permission policy is not available");
        }

        var owner = operations.GetOwnerIds(PolicyPath);
        var mode = Convert.ToUInt32(operations.GetMode(PolicyPath), 8);
        if (owner.Uid != 0 || (mode & 0x12u) != 0)
        {
            throw new InvalidOperationException("managed web permission policy must be root-owned and not group/world-writable");
        }

        return operations.ReadAllBytes(PolicyPath);
    }

    private static JsonObject ParseAndValidate(byte[] content)
    {
        var root = JsonNode.Parse(content)?.AsObject()
            ?? throw new InvalidOperationException("policy JSON must be an object");
        Validate(root);
        return root;
    }

    private static void Validate(JsonObject root)
    {
        if (root.Count != 1 || root["Sites"] is not JsonObject sites)
        {
            throw new InvalidOperationException("policy must contain only Sites");
        }

        foreach (var site in sites)
        {
            EnsureSafeUnixPath(site.Key, allowRoot: false);
            if (site.Value is not JsonObject siteObject
                || siteObject.Count != 1
                || siteObject["AllowedFiles"] is not JsonObject files)
            {
                throw new InvalidOperationException("site policy must contain only AllowedFiles");
            }

            foreach (var file in files)
            {
                EnsureSafeUnixPath(file.Key, allowRoot: false);
                var access = file.Value?.GetValue<string>();
                if (!string.Equals(access, "Update", StringComparison.Ordinal)
                    && !string.Equals(access, "Create", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("policy access must be Update or Create");
                }
            }
        }
    }

    private static void EnsureSafeUnixPath(string path, bool allowRoot)
    {
        if (!SafeUnixPathRegex().IsMatch(path) || (!allowRoot && string.Equals(path, "/", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("policy path is invalid");
        }
    }

    private static string DecodePath(string value, string label)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(label + " is not valid Base64", ex);
        }
    }

    private static void WritePreview(
        TextWriter output,
        byte[] current,
        byte[] proposed,
        string? backupPath = null)
    {
        output.WriteLine(JsonSerializer.Serialize(new
        {
            current = Encoding.UTF8.GetString(current),
            proposed = Encoding.UTF8.GetString(proposed),
            currentSha256 = ComputeHash(current),
            backupName = backupPath is null ? null : Path.GetFileName(backupPath),
        }));
    }

    private static (string Path, byte[] Content) ReadLatestBackup(IUnixPermissionOperations operations)
    {
        if (!operations.DirectoryExists(BackupDirectory))
        {
            throw new InvalidOperationException("policy backup is not available");
        }

        var path = operations.EnumerateFileSystemEntries(BackupDirectory)
            .Where(item => item.EndsWith(".json", StringComparison.Ordinal))
            .OrderByDescending(item => item, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("policy backup is not available");
        if (!operations.IsRegularFile(path)
            || operations.IsSymbolicLink(path)
            || operations.GetOwnerIds(path).Uid != 0)
        {
            throw new InvalidOperationException("policy backup metadata is unsafe");
        }

        return (path, operations.ReadAllBytes(path));
    }

    private static void EnsureHash(byte[] content, string expected)
    {
        if (!string.Equals(ComputeHash(content), expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("policy changed after preview");
        }
    }

    private static string ComputeHash(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private static string Serialize(JsonObject root)
    {
        return root.ToJsonString(PrettyJson) + "\n";
    }

    private static int WriteError(TextWriter error, string message)
    {
        error.WriteLine(message);
        return 1;
    }

    [GeneratedRegex(@"^/(?:[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeUnixPathRegex();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    private enum PolicyOperation
    {
        Add,
        Remove,
    }

    private sealed record PolicyRequest(
        string SiteRoot,
        string FilePath,
        string Access,
        string? ExpectedHash);
}
