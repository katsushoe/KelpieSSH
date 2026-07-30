using System.ComponentModel;
using System.Security.Cryptography;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    internal const long MaxLocalWebUploadBytes = 256L * 1024 * 1024;

    /// <summary>
    /// Lists web public files and directories from a provider-approved site root.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative directory path.</param>
    /// <param name="maxDepth">The maximum recursive depth. Zero lists the immediate directory only.</param>
    /// <param name="limit">The maximum number of entries to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public file list result.</returns>
    [McpServerTool(Name = "web_file_list")]
    [Description("Lists provider-approved web files and directories on a configured SSH profile.")]
    public static async Task<WebPublicFileListResult> ListWebFilesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path = "/",
        int maxDepth = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_list siteKey={siteKey}, profile={profileName}, path={path}, maxDepth={maxDepth}, limit={limit}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.ListAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            maxDepth,
            limit,
            cancellationToken);
    }

    /// <summary>
    /// Searches file and directory names under a provider-approved web public path.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="pattern">The restricted file-name glob pattern.</param>
    /// <param name="path">The absolute site-relative directory path.</param>
    /// <param name="maxDepth">The maximum recursive depth.</param>
    /// <param name="limit">The maximum number of entries to scan.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The filtered web public file list result.</returns>
    [McpServerTool(Name = "web_file_search_name")]
    [Description("Searches provider-approved web file and directory names with a restricted glob pattern.")]
    public static async Task<WebPublicFileListResult> SearchWebFileNamesAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string pattern,
        string path = "/",
        int maxDepth = 3,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_search_name siteKey={siteKey}, profile={profileName}, path={path}, pattern={pattern}, maxDepth={maxDepth}, limit={limit}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.SearchNameAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            pattern,
            maxDepth,
            limit,
            cancellationToken);
    }

    /// <summary>
    /// Searches readable text content under a provider-approved web public path.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="query">The bounded text query.</param>
    /// <param name="path">The absolute site-relative directory path.</param>
    /// <param name="maxDepth">The maximum recursive depth.</param>
    /// <param name="limit">The maximum number of matches to return.</param>
    /// <param name="maxFileBytes">The maximum readable file size for search.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public text search result.</returns>
    [McpServerTool(Name = "web_file_search_text")]
    [Description("Searches readable provider-approved web text files with bounded file size and result limits.")]
    public static async Task<WebPublicTextSearchResult> SearchWebFileTextAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string query,
        string path = "/",
        int maxDepth = 3,
        int limit = 50,
        int maxFileBytes = 262144,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_search_text siteKey={siteKey}, profile={profileName}, path={path}, maxDepth={maxDepth}, limit={limit}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.SearchTextAsync(
            sshCommandService,
            profile,
            siteKey,
            query,
            path,
            maxDepth,
            limit,
            maxFileBytes,
            cancellationToken);
    }

    /// <summary>
    /// Returns metadata for one web public path from a provider-approved site root.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public file stat result.</returns>
    [McpServerTool(Name = "web_file_stat")]
    [Description("Returns metadata for one provider-approved web public path on a configured SSH profile.")]
    public static async Task<WebPublicFileStatResult> StatWebFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_stat siteKey={siteKey}, profile={profileName}, path={path}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.StatAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            cancellationToken);
    }

    /// <summary>
    /// Returns a metadata-only SHA-256 hash for one provider-approved web public file.
    /// </summary>
    [McpServerTool(Name = "web_file_hash")]
    [Description("Returns a bounded metadata-only SHA-256 hash for one readable provider-approved web public file.")]
    public static async Task<WebPublicFileHashResult> HashWebFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string? algorithm = null,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_hash siteKey={siteKey}, profile={profileName}, path={path}, algorithm={algorithm ?? "sha256"}");
        SshConnectionProfile profile;
        try
        {
            profile = ResolveSshProfile(profileCatalog, profileName);
        }
        catch (InvalidOperationException)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            return new WebPublicFileHashResult(
                profileName?.Trim() ?? string.Empty,
                siteKey?.Trim() ?? string.Empty,
                path?.Trim() ?? string.Empty,
                ResolvedPath: string.Empty,
                string.IsNullOrWhiteSpace(algorithm) ? "sha256" : algorithm.Trim().ToLowerInvariant(),
                Hash: null,
                Size: 0,
                Owner: string.Empty,
                Group: string.Empty,
                Mode: string.Empty,
                IsSymlink: false,
                Warnings: [],
                Error: new WebPublicFileHashError("profile-not-trusted", "The SSH profile is not trusted or available.", correlationId));
        }

        return await webPublicFileProvider.HashAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            algorithm,
            cancellationToken);
    }

    /// <summary>
    /// Checks whether one web public file can be written without writing it.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative file path.</param>
    /// <param name="contentType">The optional MIME content type.</param>
    /// <param name="usePrivilegedHelper">Whether to check the bounded privileged atomic update helper.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public file write check result.</returns>
    [McpServerTool(Name = "web_file_check_write")]
    [Description("Checks whether one provider-approved web file can be written without applying changes.")]
    public static async Task<WebPublicFileWriteCheckResult> CheckWriteWebFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string? contentType = null,
        bool usePrivilegedHelper = false,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_check_write siteKey={siteKey}, profile={profileName}, path={path}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.CheckWriteAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            contentType,
            usePrivilegedHelper,
            cancellationToken);
    }

    /// <summary>
    /// Checks whether one explicitly allowed web secret file can be written without exposing the secret value.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="secretStore">The in-memory secret store.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative secret file path.</param>
    /// <param name="secretName">The server-side secret reference name.</param>
    /// <param name="contentType">The optional MIME content type. Defaults to text/plain.</param>
    /// <param name="owner">The optional target Linux owner spec in owner[:group] form.</param>
    /// <param name="mode">The optional target 3-digit octal mode.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web secret file write check result.</returns>
    [McpServerTool(Name = "web_secret_file_check_write")]
    [Description("Checks whether one explicitly allowed web secret file can be written from a server-side secret reference without exposing its value.")]
    public static async Task<WebPublicFileWriteCheckResult> CheckWriteWebSecretFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        IKelpieSecretStore secretStore,
        string profileName,
        string siteKey,
        string path,
        string secretName,
        string? contentType = null,
        string? owner = null,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_secret_file_check_write siteKey={siteKey}, profile={profileName}, path={path}, secretName={secretName}");
        if (!secretStore.TryGetContentBase64(secretName, out _, out _))
        {
            return new WebPublicFileWriteCheckResult(
                siteKey,
                DisplayName: string.Empty,
                path,
                ResolvedPath: string.Empty,
                Exists: false,
                CanWrite: false,
                RequiresConfirmation: false,
                Confirmation: string.Empty,
                ContentType: contentType ?? "text/plain",
                Reason: "Secret reference was not found or has expired.",
                Warnings: [],
                Error: "Secret reference was not found or has expired.");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.CheckSecretWriteAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            secretName,
            contentType,
            owner,
            mode,
            cancellationToken);
    }

    /// <summary>
    /// Checks whether one web public path is eligible for permission changes without changing it.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative path.</param>
    /// <param name="owner">The optional target Linux owner name or uid.</param>
    /// <param name="group">The optional target Linux group name or gid.</param>
    /// <param name="mode">The optional target 3-digit octal mode.</param>
    /// <param name="expectedSha256">The optional expected SHA-256 of the existing file.</param>
    /// <param name="createBackup">Whether to create a rollback backup before replacement.</param>
    /// <param name="preservePermissions">Whether to preserve the existing owner, group, and mode.</param>
    /// <param name="recursive">Whether to check recursive confirmation tokens.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public permission check result.</returns>
    [McpServerTool(Name = "web_file_check_permissions")]
    [Description("Checks whether one provider-approved web public path is eligible for owner/group or mode changes without applying changes.")]
    public static async Task<WebPublicPermissionCheckResult> CheckWebFilePermissionsAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string? owner = null,
        string? group = null,
        string? mode = null,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_check_permissions siteKey={siteKey}, profile={profileName}, path={path}, recursive={recursive}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.CheckPermissionsAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            owner,
            group,
            mode,
            recursive,
            cancellationToken);
    }

    /// <summary>
    /// Reads one web public file from a provider-approved site root.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative file path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public file read result.</returns>
    [McpServerTool(Name = "web_file_read")]
    [Description("Reads one provider-approved web file on a configured SSH profile.")]
    public static async Task<WebPublicFileReadResult> ReadWebFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_read siteKey={siteKey}, profile={profileName}, path={path}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.ReadFileAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            cancellationToken);
    }

    /// <summary>
    /// Reads the beginning of one web public file from a provider-approved site root.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative file path.</param>
    /// <param name="maxBytes">The maximum number of bytes to read.</param>
    /// <param name="maxLines">The maximum number of lines to return. Zero disables line limiting.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bounded web public file read result.</returns>
    [McpServerTool(Name = "web_file_head")]
    [Description("Reads the beginning of one provider-approved web file with bounded bytes and lines.")]
    public static async Task<WebPublicFileReadResult> ReadWebFileHeadAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        int maxBytes = 4096,
        int maxLines = 100,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_head siteKey={siteKey}, profile={profileName}, path={path}, maxBytes={maxBytes}, maxLines={maxLines}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.ReadHeadAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            maxBytes,
            maxLines,
            cancellationToken);
    }

    /// <summary>
    /// Reads the end of one web public file from a provider-approved site root.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative file path.</param>
    /// <param name="maxBytes">The maximum number of bytes to read.</param>
    /// <param name="maxLines">The maximum number of lines to return. Zero disables line limiting.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bounded web public file read result.</returns>
    [McpServerTool(Name = "web_file_tail")]
    [Description("Reads the end of one provider-approved web file with bounded bytes and lines.")]
    public static async Task<WebPublicFileReadResult> ReadWebFileTailAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        int maxBytes = 4096,
        int maxLines = 100,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_tail siteKey={siteKey}, profile={profileName}, path={path}, maxBytes={maxBytes}, maxLines={maxLines}");
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.ReadTailAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            maxBytes,
            maxLines,
            cancellationToken);
    }

    /// <summary>
    /// Writes one web public file to a provider-approved site root.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative file path.</param>
    /// <param name="contentBase64">The file bytes encoded as Base64.</param>
    /// <param name="confirmation">The required confirmation token: web_file_write:&lt;siteKey&gt;:&lt;path&gt;.</param>
    /// <param name="encoding">The optional text encoding label. Only utf-8 is accepted.</param>
    /// <param name="contentType">The optional MIME content type.</param>
    /// <param name="owner">The optional target Linux owner spec in owner[:group] form.</param>
    /// <param name="mode">The optional target 3-digit octal mode.</param>
    /// <param name="expectedSha256">The optional lowercase SHA-256 required to match the existing file.</param>
    /// <param name="createBackup">Whether to create a managed rollback backup before replacement.</param>
    /// <param name="preservePermissions">Whether to preserve owner, group, and mode through the managed helper.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public file write result.</returns>
    [McpServerTool(Name = "web_file_write")]
    [Description("Writes one provider-approved web file after explicit confirmation, with optional managed SHA-256 precondition, atomic backup, and permission preservation through the bounded sudo helper.")]
    public static async Task<WebPublicFileWriteResult> WriteWebFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string contentBase64,
        string confirmation,
        string? encoding = null,
        string? contentType = null,
        string? owner = null,
        string? mode = null,
        string? expectedSha256 = null,
        bool createBackup = false,
        bool preservePermissions = false,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_write siteKey={siteKey}, profile={profileName}, path={path}");
        var permissionSuffix = CreateWritePermissionConfirmationSuffix(owner, mode);
        if (expectedSha256 is not null || createBackup || preservePermissions)
        {
            permissionSuffix += $":{expectedSha256 ?? "-"}:{(createBackup ? 1 : 0)}:{(preservePermissions ? 1 : 0)}";
        }
        if (!TryGetConfirmationError("web_file_write", $"{siteKey}:{path}{permissionSuffix}", confirmation, out var confirmationError))
        {
            return new WebPublicFileWriteResult(
                siteKey,
                DisplayName: string.Empty,
                path,
                ResolvedPath: string.Empty,
                Written: false,
                Created: false,
                Overwritten: false,
                ContentType: contentType ?? string.Empty,
                Size: 0,
                Warnings: [],
                Error: confirmationError);
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.WriteFileAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            contentBase64,
            encoding,
            contentType,
            owner,
            mode,
            expectedSha256,
            createBackup,
            preservePermissions,
            cancellationToken);
    }

    /// <summary>
    /// Writes one local file directly to a provider-approved site root without returning its content.
    /// </summary>
    [McpServerTool(Name = "web_file_write_from_local")]
    [Description("Reads a bounded local regular file, verifies its required SHA-256, and writes it atomically to one provider-approved web path without returning file content.")]
    public static async Task<WebPublicFileWriteResult> WriteWebFileFromLocalAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string localPath,
        string remotePath,
        string expectedSha256,
        string confirmation,
        string? contentType = null,
        string? owner = null,
        string? mode = null,
        bool atomic = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedLocalPath = Path.GetFullPath(localPath);
        var normalizedExpectedSha256 = expectedSha256.Trim().ToLowerInvariant();
        var permissionSuffix = CreateWritePermissionConfirmationSuffix(owner, mode);
        var target = $"{siteKey}:{remotePath}:{normalizedLocalPath}:{normalizedExpectedSha256}{permissionSuffix}:atomic";
        KpLog.Info($"MCP SSH tool called: web_file_write_from_local siteKey={siteKey}, profile={profileName}, remotePath={remotePath}, localPath={normalizedLocalPath}");

        if (!atomic)
        {
            return CreateLocalWriteError(siteKey, remotePath, contentType, "web_file_write_from_local requires atomic=true.");
        }

        if (normalizedExpectedSha256.Length != 64
            || normalizedExpectedSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            return CreateLocalWriteError(siteKey, remotePath, contentType, "expectedSha256 must be a 64-character hexadecimal SHA-256.");
        }

        var file = new FileInfo(normalizedLocalPath);
        if (!file.Exists)
        {
            return CreateLocalWriteError(siteKey, remotePath, contentType, "Local file was not found.");
        }

        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return CreateLocalWriteError(siteKey, remotePath, contentType, "Local symbolic links and reparse points are not accepted.");
        }

        if (file.Length > MaxLocalWebUploadBytes)
        {
            return CreateLocalWriteError(siteKey, remotePath, contentType, $"Local file exceeds the {MaxLocalWebUploadBytes}-byte upload limit.");
        }

        await using var content = new FileStream(
            normalizedLocalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var sourceLength = content.Length;
        if (sourceLength > MaxLocalWebUploadBytes)
        {
            return CreateLocalWriteError(siteKey, remotePath, contentType, $"Local file exceeds the {MaxLocalWebUploadBytes}-byte upload limit.");
        }

        var actualSha256 = Convert.ToHexString(
            await SHA256.HashDataAsync(content, cancellationToken)).ToLowerInvariant();
        if (content.Length != sourceLength)
        {
            return CreateLocalWriteError(siteKey, remotePath, contentType, "Local file changed while it was being hashed.");
        }

        if (!string.Equals(actualSha256, normalizedExpectedSha256, StringComparison.Ordinal))
        {
            return CreateLocalWriteError(siteKey, remotePath, contentType, "Local file SHA-256 did not match expectedSha256.");
        }

        if (!TryGetConfirmationError("web_file_write_from_local", target, confirmation, out var confirmationError))
        {
            return CreateLocalWriteError(siteKey, remotePath, contentType, confirmationError);
        }

        content.Position = 0;
        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.WriteLocalFileAsync(
            sshCommandService,
            profile,
            siteKey,
            remotePath,
            content,
            sourceLength,
            normalizedExpectedSha256,
            contentType: contentType,
            owner: owner,
            mode: mode,
            cancellationToken: cancellationToken);
    }

    private static WebPublicFileWriteResult CreateLocalWriteError(
        string siteKey,
        string remotePath,
        string? contentType,
        string error)
    {
        return new WebPublicFileWriteResult(
            siteKey,
            DisplayName: string.Empty,
            remotePath,
            ResolvedPath: string.Empty,
            Written: false,
            Created: false,
            Overwritten: false,
            ContentType: contentType ?? string.Empty,
            Size: 0,
            Warnings: [],
            Error: error);
    }

    /// <summary>
    /// Restores one provider-approved web file from its managed backup.
    /// </summary>
    [McpServerTool(Name = "web_file_rollback")]
    [Description("Restores one provider-approved web file from its managed backup after exact SHA-256 and confirmation checks.")]
    public static async Task<WebPublicFileWriteResult> RollbackWebFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string expectedSha256,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_rollback siteKey={siteKey}, profile={profileName}, path={path}");
        if (!TryGetConfirmationError("web_file_rollback", $"{siteKey}:{path}:{expectedSha256}", confirmation, out var error))
        {
            return new WebPublicFileWriteResult(siteKey, string.Empty, path, string.Empty, false, false, false, string.Empty, 0, [], Error: error);
        }

        return await webPublicFileProvider.RollbackFileAsync(
            sshCommandService,
            ResolveSshProfile(profileCatalog, profileName),
            siteKey,
            path,
            expectedSha256,
            cancellationToken);
    }

    /// <summary>
    /// Commits one provider-approved web file update by removing its managed backup.
    /// </summary>
    [McpServerTool(Name = "web_file_commit")]
    [Description("Commits one provider-approved web file update by removing its managed backup after confirmation.")]
    public static async Task<WebPublicFileWriteResult> CommitWebFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_commit siteKey={siteKey}, profile={profileName}, path={path}");
        if (!TryGetConfirmationError("web_file_commit", $"{siteKey}:{path}", confirmation, out var error))
        {
            return new WebPublicFileWriteResult(siteKey, string.Empty, path, string.Empty, false, false, false, string.Empty, 0, [], Error: error);
        }

        return await webPublicFileProvider.CommitFileAsync(
            sshCommandService,
            ResolveSshProfile(profileCatalog, profileName),
            siteKey,
            path,
            cancellationToken);
    }

    /// <summary>
    /// Writes one explicitly allowed web secret file from a server-side secret reference.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="secretStore">The in-memory secret store.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative secret file path.</param>
    /// <param name="secretName">The server-side secret reference name.</param>
    /// <param name="confirmation">The required confirmation token: web_secret_file_write:&lt;siteKey&gt;:&lt;path&gt;:&lt;secretName&gt;.</param>
    /// <param name="contentType">The optional MIME content type. Defaults to text/plain.</param>
    /// <param name="owner">The optional target Linux owner spec in owner[:group] form.</param>
    /// <param name="mode">The optional target 3-digit octal mode.</param>
    /// <param name="forgetOnSuccess">Whether to remove the secret reference after a successful write.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web secret file write result.</returns>
    [McpServerTool(Name = "web_secret_file_write")]
    [Description("Writes one explicitly allowed web secret file from a server-side secret reference after explicit confirmation. The secret value is never returned.")]
    public static async Task<WebPublicFileWriteResult> WriteWebSecretFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        IKelpieSecretStore secretStore,
        string profileName,
        string siteKey,
        string path,
        string secretName,
        string confirmation,
        string? contentType = null,
        string? owner = null,
        string? mode = null,
        bool forgetOnSuccess = true,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_secret_file_write siteKey={siteKey}, profile={profileName}, path={path}, secretName={secretName}");
        var permissionSuffix = CreateWritePermissionConfirmationSuffix(owner, mode);
        if (!TryGetConfirmationError("web_secret_file_write", $"{siteKey}:{path}:{secretName}{permissionSuffix}", confirmation, out var confirmationError))
        {
            return new WebPublicFileWriteResult(
                siteKey,
                DisplayName: string.Empty,
                path,
                ResolvedPath: string.Empty,
                Written: false,
                Created: false,
                Overwritten: false,
                ContentType: contentType ?? "text/plain",
                Size: 0,
                Warnings: [],
                Error: confirmationError);
        }

        if (!secretStore.TryGetContentBase64(secretName, out var contentBase64, out _))
        {
            return new WebPublicFileWriteResult(
                siteKey,
                DisplayName: string.Empty,
                path,
                ResolvedPath: string.Empty,
                Written: false,
                Created: false,
                Overwritten: false,
                ContentType: contentType ?? "text/plain",
                Size: 0,
                Warnings: [],
                Error: "Secret reference was not found or has expired.");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        var result = await webPublicFileProvider.WriteSecretFileAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            contentBase64,
            contentType,
            owner,
            mode,
            cancellationToken);
        if (result.Written && forgetOnSuccess)
        {
            secretStore.Forget(secretName);
        }

        return result;
    }

    private static string CreateWritePermissionConfirmationSuffix(
        string? owner,
        string? mode)
    {
        return string.IsNullOrWhiteSpace(owner)
            && string.IsNullOrWhiteSpace(mode)
            ? string.Empty
            : $":{owner}:{mode}";
    }

    /// <summary>
    /// Changes the owner of one web public path.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative path. Use / for the site root.</param>
    /// <param name="owner">The target Linux owner name or numeric uid. root and 0 are rejected.</param>
    /// <param name="group">The target Linux group name or numeric gid. root and 0 are rejected.</param>
    /// <param name="confirmation">The required confirmation token: web_change_owner:&lt;siteKey&gt;:&lt;path&gt;:&lt;owner&gt;:&lt;group&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public permission change result.</returns>
    [McpServerTool(Name = "web_change_owner")]
    [Description("Runs sudo chown for one provider-approved web public path after explicit confirmation.")]
    public static async Task<WebPublicPermissionChangeResult> ChangeWebPublicOwnerAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string owner,
        string group,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_change_owner siteKey={siteKey}, profile={profileName}, path={path}, owner={owner}, group={group}");
        if (!TryGetConfirmationError("web_change_owner", $"{siteKey}:{path}:{owner}:{group}", confirmation, out var confirmationError))
        {
            return new WebPublicPermissionChangeResult(
                siteKey,
                DisplayName: string.Empty,
                path,
                ResolvedPath: string.Empty,
                Changed: false,
                owner,
                group,
                Mode: string.Empty,
                Warnings: [],
                Error: confirmationError);
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.ChangeOwnerAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            owner,
            group,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Changes the owner of a web public directory tree.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative path. Use / for the site root.</param>
    /// <param name="owner">The target Linux owner name or numeric uid. root and 0 are rejected.</param>
    /// <param name="group">The target Linux group name or numeric gid. root and 0 are rejected.</param>
    /// <param name="confirmation">The required confirmation token: web_change_owner_recursive:&lt;siteKey&gt;:&lt;path&gt;:&lt;owner&gt;:&lt;group&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public permission change result.</returns>
    [McpServerTool(Name = "web_change_owner_recursive")]
    [Description("Runs sudo chown recursively for one provider-approved web public directory tree after explicit confirmation. Symbolic links are skipped.")]
    public static async Task<WebPublicPermissionChangeResult> ChangeWebPublicOwnerRecursiveAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string owner,
        string group,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_change_owner_recursive siteKey={siteKey}, profile={profileName}, path={path}, owner={owner}, group={group}");
        if (!TryGetConfirmationError("web_change_owner_recursive", $"{siteKey}:{path}:{owner}:{group}", confirmation, out var confirmationError))
        {
            return new WebPublicPermissionChangeResult(
                siteKey,
                DisplayName: string.Empty,
                path,
                ResolvedPath: string.Empty,
                Changed: false,
                owner,
                group,
                Mode: string.Empty,
                Warnings: [],
                Error: confirmationError);
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.ChangeOwnerAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            owner,
            group,
            recursive: true,
            cancellationToken);
    }

    /// <summary>
    /// Changes the mode of one web public path.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative path. Use / for the site root.</param>
    /// <param name="mode">The target 3-digit octal mode. World-writable modes are rejected.</param>
    /// <param name="confirmation">The required confirmation token: web_change_mode:&lt;siteKey&gt;:&lt;path&gt;:&lt;mode&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public permission change result.</returns>
    [McpServerTool(Name = "web_change_mode")]
    [Description("Runs sudo chmod for one provider-approved web public path after explicit confirmation.")]
    public static async Task<WebPublicPermissionChangeResult> ChangeWebPublicModeAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string mode,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_change_mode siteKey={siteKey}, profile={profileName}, path={path}, mode={mode}");
        if (!TryGetConfirmationError("web_change_mode", $"{siteKey}:{path}:{mode}", confirmation, out var confirmationError))
        {
            return new WebPublicPermissionChangeResult(
                siteKey,
                DisplayName: string.Empty,
                path,
                ResolvedPath: string.Empty,
                Changed: false,
                Owner: string.Empty,
                Group: string.Empty,
                mode,
                Warnings: [],
                Error: confirmationError);
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.ChangeModeAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            mode,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Changes the mode of a web public directory tree.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative path. Use / for the site root.</param>
    /// <param name="mode">The target 3-digit octal mode. World-writable modes are rejected.</param>
    /// <param name="confirmation">The required confirmation token: web_change_mode_recursive:&lt;siteKey&gt;:&lt;path&gt;:&lt;mode&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public permission change result.</returns>
    [McpServerTool(Name = "web_change_mode_recursive")]
    [Description("Runs sudo chmod recursively for one provider-approved web public directory tree after explicit confirmation. Symbolic links are skipped.")]
    public static async Task<WebPublicPermissionChangeResult> ChangeWebPublicModeRecursiveAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider webPublicFileProvider,
        string profileName,
        string siteKey,
        string path,
        string mode,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_change_mode_recursive siteKey={siteKey}, profile={profileName}, path={path}, mode={mode}");
        if (!TryGetConfirmationError("web_change_mode_recursive", $"{siteKey}:{path}:{mode}", confirmation, out var confirmationError))
        {
            return new WebPublicPermissionChangeResult(
                siteKey,
                DisplayName: string.Empty,
                path,
                ResolvedPath: string.Empty,
                Changed: false,
                Owner: string.Empty,
                Group: string.Empty,
                mode,
                Warnings: [],
                Error: confirmationError);
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await webPublicFileProvider.ChangeModeAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            mode,
            recursive: true,
            cancellationToken);
    }
}
