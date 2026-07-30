using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KelpieWebPermissionHelper;

internal static partial class BulkWebTransferCommand
{
    private const int MaximumFiles = 100;
    private const long MaximumArchiveBytes = 257L * 1024 * 1024;
    private const string PolicyPath = "/etc/kelpie/web-permission-helper-policy.json";

    public static int Write(string[] args, Stream input, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Length != 7)
            {
                throw new InvalidOperationException("bulk-write requires siteRoot, archive input, maxBytes, createDirectories, transferId, and archiveSha256");
            }

            var siteRoot = Decode(args[1]);
            if (!string.Equals(args[2], "-", StringComparison.Ordinal)
                || !long.TryParse(args[3], out var maxBytes)
                || maxBytes is <= 0 or > MaximumArchiveBytes
                || args[4] is not ("0" or "1")
                || !TransferIdRegex().IsMatch(args[5])
                || !Sha256Regex().IsMatch(args[6]))
            {
                throw new InvalidOperationException("bulk-write arguments are invalid");
            }

            var root = Path.GetFullPath(siteRoot);
            var rootInfo = new DirectoryInfo(root);
            if (!rootInfo.Exists || rootInfo.LinkTarget is not null)
            {
                throw new InvalidOperationException("site root must be an existing non-symbolic directory");
            }

            var transactionRoot = Path.Combine(root, ".kelpie-bulk-" + args[5]);
            if (Directory.Exists(transactionRoot) || File.Exists(transactionRoot))
            {
                throw new InvalidOperationException("bulk transfer transaction already exists");
            }

            Directory.CreateDirectory(transactionRoot);
            if (new DirectoryInfo(transactionRoot).LinkTarget is not null)
            {
                throw new InvalidOperationException("bulk transaction directory metadata is unsafe");
            }

            if (OperatingSystem.IsLinux())
            {
                var operations = new LibcUnixPermissionOperations();
                if (operations.GetOwnerIds(transactionRoot).Uid != 0)
                {
                    throw new InvalidOperationException("bulk transaction directory metadata is unsafe");
                }

                operations.ChangeMode(transactionRoot, 0x1C0);
            }
            var archivePath = Path.Combine(transactionRoot, "archive.zip");
            try
            {
                CopyBounded(input, archivePath, maxBytes, args[6]);
                var manifest = ReadAndValidateArchive(archivePath, root);
                ValidateManagedPolicy(root, manifest);
                ApplyArchive(archivePath, root, transactionRoot, manifest, args[4] == "1");
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    applied = true,
                    files = manifest.Select(file => new
                    {
                        path = file.Path,
                        resolvedPath = ResolveTarget(root, file.Path),
                        created = file.Created,
                        overwritten = !file.Created,
                        size = file.Size,
                        sha256 = file.Sha256,
                        backupPath = file.Created ? string.Empty : Path.Combine(transactionRoot, "backups", file.Index.ToString("D4")),
                    }),
                    error = (string?)null,
                }));
                return 0;
            }
            catch
            {
                if (Directory.Exists(transactionRoot))
                {
                    Directory.Delete(transactionRoot, recursive: true);
                }

                throw;
            }
        }
        catch (Exception ex)
        {
            error.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    public static int Commit(string[] args, TextWriter output, TextWriter error)
    {
        return Complete(args, rollback: false, output, error);
    }

    public static int Rollback(string[] args, TextWriter output, TextWriter error)
    {
        return Complete(args, rollback: true, output, error);
    }

    private static int Complete(string[] args, bool rollback, TextWriter output, TextWriter error)
    {
        try
        {
            if (args.Length != 3 || !TransferIdRegex().IsMatch(args[2]))
            {
                throw new InvalidOperationException("bulk completion arguments are invalid");
            }

            var root = Path.GetFullPath(Decode(args[1]));
            var transactionRoot = Path.Combine(root, ".kelpie-bulk-" + args[2]);
            var statePath = Path.Combine(transactionRoot, "state.json");
            var state = JsonSerializer.Deserialize<BulkState>(File.ReadAllBytes(statePath), JsonOptions)
                ?? throw new InvalidOperationException("bulk transfer state is invalid");
            if (rollback)
            {
                Restore(root, transactionRoot, state.Files);
            }

            Directory.Delete(transactionRoot, recursive: true);
            output.WriteLine(JsonSerializer.Serialize(new { applied = !rollback, files = Array.Empty<object>(), error = (string?)null }));
            return 0;
        }
        catch (Exception ex)
        {
            error.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static IReadOnlyList<BulkFile> ReadAndValidateArchive(string archivePath, string root)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count is < 2 or > MaximumFiles + 1)
        {
            throw new InvalidOperationException("bulk archive entry count is invalid");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (!names.Add(entry.FullName)
                || entry.FullName.StartsWith("/", StringComparison.Ordinal)
                || entry.FullName.Contains('\\', StringComparison.Ordinal)
                || entry.FullName.Split('/').Contains("..", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("bulk archive contains an unsafe or duplicate entry");
            }

            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixType != 0 && unixType != 0x8000)
            {
                throw new InvalidOperationException("bulk archive links and non-regular entries are not accepted");
            }
        }

        var manifestEntry = archive.GetEntry("manifest.json")
            ?? throw new InvalidOperationException("bulk archive manifest is missing");
        using var manifestStream = manifestEntry.Open();
        var manifest = JsonSerializer.Deserialize<List<BulkFile>>(manifestStream, JsonOptions)
            ?? throw new InvalidOperationException("bulk archive manifest is invalid");
        if (manifest.Count is < 1 or > MaximumFiles)
        {
            throw new InvalidOperationException("bulk manifest file count is invalid");
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < manifest.Count; index++)
        {
            var file = manifest[index] with { Index = index };
            manifest[index] = file;
            if (!string.Equals(file.ArchiveEntry, $"files/{index:D4}", StringComparison.Ordinal)
                || !paths.Add(file.Path)
                || file.Size < 0
                || !Sha256Regex().IsMatch(file.Sha256)
                || archive.GetEntry(file.ArchiveEntry) is null)
            {
                throw new InvalidOperationException("bulk manifest contains an invalid or duplicate file");
            }

            _ = ResolveTarget(root, file.Path);
        }

        if (names.Count != manifest.Count + 1)
        {
            throw new InvalidOperationException("bulk archive contains entries not declared by the manifest");
        }

        return manifest;
    }

    private static void ApplyArchive(
        string archivePath,
        string root,
        string transactionRoot,
        IReadOnlyList<BulkFile> files,
        bool createDirectories)
    {
        var staging = Path.Combine(transactionRoot, "staging");
        var backups = Path.Combine(transactionRoot, "backups");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(backups);
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            foreach (var file in files)
            {
                var staged = Path.Combine(staging, file.Index.ToString("D4"));
                using var source = archive.GetEntry(file.ArchiveEntry)!.Open();
                using var destination = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                long size = 0;
                int read;
                while ((read = source.Read(buffer)) > 0)
                {
                    size += read;
                    if (size > file.Size)
                    {
                        throw new InvalidOperationException("bulk archive file exceeds its declared size");
                    }

                    destination.Write(buffer, 0, read);
                    hash.AppendData(buffer, 0, read);
                }

                var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                if (size != file.Size || !string.Equals(actualHash, file.Sha256, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("bulk archive file does not match its manifest");
                }
            }
        }

        var applied = new List<BulkFile>();
        var operations = new LibcUnixPermissionOperations();
        try
        {
            foreach (var original in files)
            {
                var target = ResolveTarget(root, original.Path);
                var parent = Path.GetDirectoryName(target)!;
                if (!Directory.Exists(parent))
                {
                    if (!createDirectories)
                    {
                        throw new InvalidOperationException("bulk target parent directory does not exist");
                    }

                    Directory.CreateDirectory(parent);
                }

                EnsureNoSymlinkBetween(root, parent);
                var created = !File.Exists(target);
                var originalOwner = created ? operations.GetSudoUserIds() : operations.GetOwnerIds(target);
                var originalMode = created ? "644" : operations.GetMode(target);
                if (!created)
                {
                    var info = new FileInfo(target);
                    if (info.LinkTarget is not null)
                    {
                        throw new InvalidOperationException("bulk target must not be a symbolic link");
                    }

                    File.Move(target, Path.Combine(backups, original.Index.ToString("D4")));
                }

                var file = original with { Created = created };
                applied.Add(file);
                File.Move(Path.Combine(staging, file.Index.ToString("D4")), target);
                ApplyMetadata(target, file, originalOwner, originalMode, operations);
            }

            File.WriteAllBytes(Path.Combine(transactionRoot, "state.json"), JsonSerializer.SerializeToUtf8Bytes(new BulkState(applied), JsonOptions));
            File.Delete(archivePath);
            Directory.Delete(staging);
        }
        catch
        {
            Restore(root, transactionRoot, applied);
            throw;
        }
    }

    private static void Restore(string root, string transactionRoot, IReadOnlyList<BulkFile> files)
    {
        foreach (var file in files.Reverse())
        {
            var target = ResolveTarget(root, file.Path);
            File.Delete(target);
            var backup = Path.Combine(transactionRoot, "backups", file.Index.ToString("D4"));
            if (!file.Created && File.Exists(backup))
            {
                File.Move(backup, target);
            }
        }
    }

    private static void ValidateManagedPolicy(string root, IReadOnlyList<BulkFile> files)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(PolicyPath));
        if (!document.RootElement.GetProperty("Sites").TryGetProperty(root, out var site)
            || !site.TryGetProperty("AllowedFiles", out var allowed))
        {
            throw new InvalidOperationException("managed web policy does not allow this site");
        }

        foreach (var file in files)
        {
            var exists = File.Exists(ResolveTarget(root, file.Path));
            if (!allowed.TryGetProperty(file.Path, out var access)
                || (exists && access.GetString() is not ("Update" or "Create"))
                || (!exists && access.GetString() != "Create"))
            {
                throw new InvalidOperationException(file.Path + ": managed web policy does not allow this write");
            }
        }
    }

    private static string ResolveTarget(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.Split('/').Contains("..", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("bulk target path is unsafe");
        }

        var target = Path.GetFullPath(Path.Combine(root, path.TrimStart('/')));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("bulk target path is outside the site root");
        }

        return target;
    }

    private static void EnsureNoSymlinkBetween(string root, string directory)
    {
        var relative = Path.GetRelativePath(root, directory);
        var current = root;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (new DirectoryInfo(current).LinkTarget is not null)
            {
                throw new InvalidOperationException("bulk target parent must not contain a symbolic link");
            }
        }
    }

    private static void ApplyMetadata(
        string path,
        BulkFile file,
        (uint Uid, uint Gid) originalOwner,
        string originalMode,
        IUnixPermissionOperations operations)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var owner = originalOwner;
        if (!string.IsNullOrEmpty(file.Owner))
        {
            var parts = file.Owner.Split(':');
            if (parts.Length > 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                throw new InvalidOperationException("bulk owner must use owner[:group] format");
            }

            owner = (
                operations.ResolveUserId(parts[0]),
                parts.Length == 2 && parts[1].Length > 0
                    ? operations.ResolveGroupId(parts[1])
                    : originalOwner.Gid);
            if (owner.Uid == 0 || owner.Gid == 0)
            {
                throw new InvalidOperationException("bulk owner and group must not resolve to root");
            }
        }

        operations.ChangeOwner(path, owner.Uid, owner.Gid);
        operations.ChangeMode(path, ParseMode(string.IsNullOrEmpty(file.Mode) ? originalMode : file.Mode));
    }

    private static uint ParseMode(string mode)
    {
        if (!ModeRegex().IsMatch(mode))
        {
            throw new InvalidOperationException("bulk file mode is invalid");
        }

        return Convert.ToUInt32(mode, 8);
    }

    private static void CopyBounded(Stream input, string path, long maximum, string expectedHash)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            total += read;
            if (total > maximum)
            {
                throw new InvalidOperationException("bulk archive exceeds maximum size");
            }

            output.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
        }

        var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(actual, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("bulk archive SHA-256 does not match");
        }
    }

    private static string Decode(string value)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex TransferIdRegex();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex("^[0-7]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex ModeRegex();

    private sealed record BulkState(IReadOnlyList<BulkFile> Files);

    private sealed record BulkFile(
        string ArchiveEntry,
        string Path,
        long Size,
        string Sha256,
        string ContentType,
        string? Owner,
        string? Mode,
        int Index = 0,
        bool Created = false);
}
