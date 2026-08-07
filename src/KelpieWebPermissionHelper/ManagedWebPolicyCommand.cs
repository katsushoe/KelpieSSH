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
    private const int MaxManifestBytes = 64 * 1024;
    private const int MaxManifestChanges = 256;
    private const int MaxManifestSites = 32;
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
            "preview-apply" => PreviewManifest(args.Skip(2).ToArray(), operations, output),
            "apply-manifest" => ApplyManifest(args.Skip(2).ToArray(), operations, output),
            "preview-rollback" => PreviewRollback(args.Skip(2).ToArray(), operations, output),
            "apply-rollback" => ApplyRollback(args.Skip(2).ToArray(), operations, output),
            _ => WriteError(error, "ERROR: unsupported policy action"),
        };
    }

    private static int PreviewManifest(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter output)
    {
        if (args.Count != 1)
        {
            throw new InvalidOperationException("policy apply preview requires one manifest");
        }

        var changes = ParseManifest(args[0]);
        var currentBytes = ReadSecurePolicy(operations);
        var proposed = CreateManifestProposed(ParseAndValidate(currentBytes), changes);
        WritePreview(output, currentBytes, Encoding.UTF8.GetBytes(Serialize(proposed)));
        return 0;
    }

    private static int ApplyManifest(
        IReadOnlyList<string> args,
        IUnixPermissionOperations operations,
        TextWriter output)
    {
        if (args.Count != 2 || !Sha256Regex().IsMatch(args[1]))
        {
            throw new InvalidOperationException("policy apply arguments are invalid");
        }

        var changes = ParseManifest(args[0]);
        var currentBytes = ReadSecurePolicy(operations);
        EnsureHash(currentBytes, args[1]);
        var proposed = CreateManifestProposed(ParseAndValidate(currentBytes), changes);
        Apply(operations, "apply", string.Empty, changes.Count + " changes", currentBytes, Encoding.UTF8.GetBytes(Serialize(proposed)));
        output.WriteLine(JsonSerializer.Serialize(new { changed = true, changeCount = changes.Count }));
        return 0;
    }

    private static IReadOnlyList<ManifestChange> ParseManifest(string encoded)
    {
        byte[] content;
        try
        {
            content = Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("manifest is not valid Base64", ex);
        }

        if (content.Length == 0 || content.Length > MaxManifestBytes)
        {
            throw new InvalidOperationException("manifest size is invalid");
        }

        using var document = JsonDocument.Parse(content, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
        });
        var root = document.RootElement;
        EnsureProperties(root, "sites");
        var sites = root.GetProperty("sites");
        if (sites.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("manifest sites must be an object");
        }

        var changes = new List<ManifestChange>();
        var siteCount = 0;
        foreach (var site in sites.EnumerateObject())
        {
            siteCount++;
            if (siteCount > MaxManifestSites)
            {
                throw new InvalidOperationException("manifest contains too many sites");
            }

            EnsureSafeUnixPath(site.Name, allowRoot: false);
            EnsureProperties(site.Value, "changes");
            var siteChanges = site.Value.GetProperty("changes");
            if (siteChanges.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("manifest changes must be an array");
            }

            foreach (var item in siteChanges.EnumerateArray())
            {
                EnsureProperties(item, "operation", "path", "access");
                var operation = item.GetProperty("operation").GetString();
                var path = item.GetProperty("path").GetString() ?? string.Empty;
                var access = item.GetProperty("access").GetString();
                if (!string.Equals(operation, "add", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("manifest operation must be add");
                }

                EnsureSafeUnixPath(path, allowRoot: false);
                if (access is not ("Update" or "Create"))
                {
                    throw new InvalidOperationException("manifest access must be Update or Create");
                }

                changes.Add(new ManifestChange(site.Name, path, access));
                if (changes.Count > MaxManifestChanges)
                {
                    throw new InvalidOperationException("manifest contains too many changes");
                }
            }
        }

        if (changes.Count == 0)
        {
            throw new InvalidOperationException("manifest must contain at least one change");
        }

        if (changes.GroupBy(change => (change.SiteRoot, change.FilePath)).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("manifest contains duplicate paths");
        }

        return changes;
    }

    private static void EnsureProperties(JsonElement element, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("manifest object is invalid");
        }

        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != expected.Length || actual.Any(name => !expected.Contains(name, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("manifest contains missing or unknown fields");
        }
    }

    private static JsonObject CreateManifestProposed(JsonObject current, IReadOnlyList<ManifestChange> changes)
    {
        var proposed = (JsonObject)current.DeepClone();
        foreach (var change in changes)
        {
            proposed = CreateProposed(
                proposed,
                new PolicyRequest(change.SiteRoot, change.FilePath, change.Access, null),
                PolicyOperation.Add);
        }

        return proposed;
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
        var replaced = false;
        try
        {
            WriteWithMetadata(operations, temporaryPath, proposed, metadata.Uid, metadata.Gid, mode);
            ParseAndValidate(operations.ReadAllBytes(temporaryPath));
            operations.MoveFileOverwrite(temporaryPath, PolicyPath);
            replaced = true;
            ParseAndValidate(ReadSecurePolicy(operations));
            AppendAudit(operations, operation, "completed", siteRoot, filePath);
        }
        catch
        {
            if (replaced)
            {
                var restorePath = PolicyPath + ".restore-" + Guid.NewGuid().ToString("N");
                try
                {
                    WriteWithMetadata(operations, restorePath, current, metadata.Uid, metadata.Gid, mode);
                    operations.MoveFileOverwrite(restorePath, PolicyPath);
                }
                finally
                {
                    operations.DeleteFileIfExists(restorePath);
                }
            }

            throw;
        }
        finally
        {
            operations.DeleteFileIfExists(temporaryPath);
        }
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
                throw new InvalidOperationException("policy entry already exists: " + request.FilePath);
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

    private sealed record ManifestChange(string SiteRoot, string FilePath, string Access);
}
