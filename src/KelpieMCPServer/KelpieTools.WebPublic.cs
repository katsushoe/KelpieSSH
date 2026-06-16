using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
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
    /// Checks whether one web public file can be written without writing it.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="webPublicFileProvider">The web public file provider.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="siteKey">The web public site key.</param>
    /// <param name="path">The absolute site-relative file path.</param>
    /// <param name="contentType">The optional MIME content type.</param>
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
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The web public file write result.</returns>
    [McpServerTool(Name = "web_file_write")]
    [Description("Writes one provider-approved web file after explicit confirmation, optionally applying owner[:group] and/or mode atomically through the sudo helper.")]
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
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: web_file_write siteKey={siteKey}, profile={profileName}, path={path}");
        var permissionSuffix = CreateWritePermissionConfirmationSuffix(owner, mode);
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
            cancellationToken);
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
