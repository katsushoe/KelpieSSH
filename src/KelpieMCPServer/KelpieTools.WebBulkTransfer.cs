using System.ComponentModel;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    private const long MaximumBulkTransferBytes = 256L * 1024 * 1024;

    [McpServerTool(Name = "web_bulk_transfer_begin")]
    [Description("Begins a bounded in-memory web bulk transfer draft.")]
    public static WebBulkTransfer BeginWebBulkTransfer(
        WebBulkTransferStore store,
        string profileName,
        string siteKey)
    {
        return store.Create(profileName.Trim(), siteKey.Trim());
    }

    [McpServerTool(Name = "web_bulk_transfer_add_file")]
    [Description("Adds one regular local file and remote path to a web bulk transfer draft.")]
    public static async Task<WebBulkTransfer> AddWebBulkTransferFileAsync(
        WebBulkTransferStore store,
        string transferId,
        string localPath,
        string remotePath,
        string? contentType = null,
        string? owner = null,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(localPath);
        var info = new FileInfo(fullPath);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The local path must identify an existing regular file.");
        }

        if (info.Length > MaximumBulkTransferBytes)
        {
            throw new InvalidOperationException("The local file exceeds the bulk transfer size limit.");
        }

        await using var input = OpenLocalFile(fullPath);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
        if (input.Length != info.Length)
        {
            throw new InvalidOperationException("The local file changed while it was being hashed.");
        }

        var item = new WebBulkTransferItem(
            fullPath,
            NormalizeBulkRemotePath(remotePath),
            info.Length,
            hash,
            contentType?.Trim() ?? string.Empty,
            owner?.Trim(),
            mode?.Trim());
        return store.AddFile(transferId, item);
    }

    [McpServerTool(Name = "web_bulk_transfer_list")]
    [Description("Lists web bulk transfer summaries without file details.")]
    public static IReadOnlyList<WebBulkTransferSummary> ListWebBulkTransfers(WebBulkTransferStore store)
    {
        return store.List().Select(ToSummary).ToArray();
    }

    [McpServerTool(Name = "web_bulk_transfer_get")]
    [Description("Returns one web bulk transfer including its complete file list.")]
    public static WebBulkTransfer GetWebBulkTransfer(WebBulkTransferStore store, string transferId)
    {
        return store.Get(transferId);
    }

    [McpServerTool(Name = "web_bulk_transfer_preview")]
    [Description("Validates every file against the managed web policy and returns the manifest-bound execute confirmation.")]
    public static async Task<WebBulkTransferPreview> PreviewWebBulkTransferAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider provider,
        WebBulkTransferStore store,
        string transferId,
        CancellationToken cancellationToken = default)
    {
        var transfer = store.Get(transferId);
        if (transfer.State is not (WebBulkTransferState.Draft or WebBulkTransferState.Validated))
        {
            throw new InvalidOperationException("Only a draft bulk transfer can be previewed.");
        }

        if (transfer.Files.Count == 0)
        {
            throw new InvalidOperationException("The bulk transfer contains no files.");
        }

        var profile = ResolveSshProfile(profileCatalog, transfer.ProfileName);
        var checks = new List<WebPublicFileWriteCheckResult>(transfer.Files.Count);
        foreach (var file in transfer.Files)
        {
            checks.Add(await provider.CheckWriteAsync(
                sshCommandService,
                profile,
                transfer.SiteKey,
                file.RemotePath,
                EmptyToNull(file.ContentType),
                usePrivilegedHelper: true,
                cancellationToken));
        }

        var error = checks.FirstOrDefault(check => !check.CanWrite || check.Error is not null);
        if (error is not null)
        {
            throw new InvalidOperationException($"{error.Path}: {error.Error ?? error.Reason}");
        }

        var manifestHash = ComputeManifestHash(transfer);
        if (transfer.State == WebBulkTransferState.Draft)
        {
            transfer = store.SetState(transfer.Id, WebBulkTransferState.Draft, WebBulkTransferState.Validated, manifestHash);
        }

        var confirmation = $"web_bulk_transfer_execute:{transfer.Id}:{manifestHash}";
        return new WebBulkTransferPreview(ToSummary(transfer), transfer.Files, manifestHash, true, confirmation);
    }

    [McpServerTool(Name = "web_bulk_transfer_execute")]
    [Description("Creates one internal ZIP, transfers it once, and atomically applies all policy-approved files after manifest-bound confirmation.")]
    public static async Task<WebBulkTransferResult> ExecuteWebBulkTransferAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider provider,
        WebBulkTransferStore store,
        string transferId,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        var transfer = store.Get(transferId);
        if (transfer.State != WebBulkTransferState.Validated || transfer.ManifestSha256 is null)
        {
            throw new InvalidOperationException("Preview the bulk transfer immediately before execution.");
        }

        var required = $"web_bulk_transfer_execute:{transfer.Id}:{transfer.ManifestSha256}";
        if (!string.Equals(confirmation, required, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Confirmation is required: {required}");
        }

        ValidateLocalFilesUnchanged(transfer);
        store.SetState(transfer.Id, WebBulkTransferState.Validated, WebBulkTransferState.Executing);
        var archivePath = Path.Combine(Path.GetTempPath(), $"kelpie-web-bulk-{transfer.Id}.zip");
        try
        {
            await CreateArchiveAsync(archivePath, transfer, cancellationToken);
            await using var archive = OpenLocalFile(archivePath);
            var archiveHash = Convert.ToHexString(await SHA256.HashDataAsync(archive, cancellationToken)).ToLowerInvariant();
            archive.Position = 0;
            var files = transfer.Files.Select((file, index) => new WebBulkTransferFile(
                $"files/{index:D4}",
                file.RemotePath,
                file.Size,
                file.Sha256,
                file.ContentType,
                file.Owner,
                file.Mode)).ToArray();
            var result = await provider.WriteBulkAsync(
                sshCommandService,
                ResolveSshProfile(profileCatalog, transfer.ProfileName),
                transfer.SiteKey,
                transfer.Id,
                archive,
                archive.Length,
                archiveHash,
                files,
                cancellationToken);
            store.SetState(
                transfer.Id,
                WebBulkTransferState.Executing,
                result.Applied ? WebBulkTransferState.Applied : WebBulkTransferState.Failed,
                error: result.Error,
                appliedFiles: result.Files);
            return result;
        }
        catch (Exception ex)
        {
            store.SetState(transfer.Id, WebBulkTransferState.Executing, WebBulkTransferState.Failed, error: ex.Message);
            throw;
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    [McpServerTool(Name = "web_bulk_transfer_commit")]
    [Description("Commits an applied web bulk transfer and removes its remote rollback data.")]
    public static async Task<WebBulkTransferResult> CommitWebBulkTransferAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider provider,
        WebBulkTransferStore store,
        string transferId,
        CancellationToken cancellationToken = default)
    {
        var transfer = store.Get(transferId);
        if (transfer.State != WebBulkTransferState.Applied)
        {
            throw new InvalidOperationException("Only an applied bulk transfer can be committed.");
        }

        var result = await provider.CommitBulkAsync(sshCommandService, ResolveSshProfile(profileCatalog, transfer.ProfileName), transfer.SiteKey, transfer.Id, cancellationToken);
        if (result.Error is null)
        {
            store.SetState(transfer.Id, WebBulkTransferState.Applied, WebBulkTransferState.Committed);
        }

        return result;
    }

    [McpServerTool(Name = "web_bulk_transfer_rollback")]
    [Description("Rolls back every file from an applied web bulk transfer.")]
    public static async Task<WebBulkTransferResult> RollbackWebBulkTransferAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider provider,
        WebBulkTransferStore store,
        string transferId,
        CancellationToken cancellationToken = default)
    {
        var transfer = store.Get(transferId);
        if (transfer.State != WebBulkTransferState.Applied)
        {
            throw new InvalidOperationException("Only an applied bulk transfer can be rolled back.");
        }

        var result = await provider.RollbackBulkAsync(sshCommandService, ResolveSshProfile(profileCatalog, transfer.ProfileName), transfer.SiteKey, transfer.Id, cancellationToken);
        if (result.Error is null)
        {
            store.SetState(transfer.Id, WebBulkTransferState.Applied, WebBulkTransferState.RolledBack);
        }

        return result;
    }

    [McpServerTool(Name = "web_bulk_transfer_cancel")]
    [Description("Cancels and removes a non-applied web bulk transfer.")]
    public static bool CancelWebBulkTransfer(WebBulkTransferStore store, string transferId)
    {
        store.Cancel(transferId);
        return true;
    }

    private static FileStream OpenLocalFile(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);

    private static void ValidateLocalFilesUnchanged(WebBulkTransfer transfer)
    {
        foreach (var item in transfer.Files)
        {
            var info = new FileInfo(item.LocalPath);
            if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length != item.Size)
            {
                throw new InvalidOperationException($"Local file changed after registration: {item.LocalPath}");
            }

            using var input = OpenLocalFile(item.LocalPath);
            var hash = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
            if (!string.Equals(hash, item.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Local file changed after registration: {item.LocalPath}");
            }
        }
    }

    private static async Task CreateArchiveAsync(string archivePath, WebBulkTransfer transfer, CancellationToken cancellationToken)
    {
        await using var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var manifest = transfer.Files.Select((file, index) => new
        {
            archiveEntry = $"files/{index:D4}",
            path = file.RemotePath,
            size = file.Size,
            sha256 = file.Sha256,
            contentType = file.ContentType,
            owner = file.Owner,
            mode = file.Mode,
        }).ToArray();
        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using (var manifestStream = manifestEntry.Open())
        {
            await JsonSerializer.SerializeAsync(manifestStream, manifest, cancellationToken: cancellationToken);
        }

        for (var index = 0; index < transfer.Files.Count; index++)
        {
            var entry = archive.CreateEntry($"files/{index:D4}", CompressionLevel.Optimal);
            await using var destination = entry.Open();
            await using var source = OpenLocalFile(transfer.Files[index].LocalPath);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long size = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                size += read;
                if (size > transfer.Files[index].Size)
                {
                    throw new InvalidOperationException($"Local file changed during archive creation: {transfer.Files[index].LocalPath}");
                }

                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (size != transfer.Files[index].Size
                || !string.Equals(actualHash, transfer.Files[index].Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Local file changed during archive creation: {transfer.Files[index].LocalPath}");
            }
        }
    }

    private static string ComputeManifestHash(WebBulkTransfer transfer)
    {
        var canonical = string.Join(
            "\n",
            transfer.Files.OrderBy(file => file.RemotePath, StringComparer.Ordinal)
                .Select(file => $"{file.RemotePath}\0{file.Size}\0{file.Sha256}\0{file.ContentType}\0{file.Owner}\0{file.Mode}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static WebBulkTransferSummary ToSummary(WebBulkTransfer transfer) =>
        new(transfer.Id, transfer.ProfileName, transfer.SiteKey, transfer.State, transfer.Files.Count, transfer.Files.Sum(file => file.Size), transfer.CreatedAt, transfer.ExpiresAt, transfer.ManifestSha256, transfer.Error);

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static string NormalizeBulkRemotePath(string path)
    {
        var value = path.Trim();
        if (!value.StartsWith("/", StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("The remote path must be an absolute site-relative path without traversal.");
        }

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new InvalidOperationException("The remote path must identify a file.");
        }

        return "/" + string.Join('/', parts);
    }
}

public sealed record WebBulkTransferSummary(
    string Id,
    string ProfileName,
    string SiteKey,
    WebBulkTransferState State,
    int FileCount,
    long TotalBytes,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string? ManifestSha256,
    string? Error);

public sealed record WebBulkTransferPreview(
    WebBulkTransferSummary Summary,
    IReadOnlyList<WebBulkTransferItem> Files,
    string ManifestSha256,
    bool RequiresConfirmation,
    string Confirmation);
