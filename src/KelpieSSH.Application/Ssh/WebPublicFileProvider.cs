using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides safe read and write access to web public roots.
/// </summary>
public sealed partial class WebPublicFileProvider : IWebPublicFileProvider
{
    private const string OptionalArgumentNone = "\u001fKELPIE_NONE\u001f";
    private const string ListCommandName = "web_public_file_list_internal";
    private const string StatCommandName = "web_public_file_stat_internal";
    private const string HashCommandName = "web_public_file_hash_internal";
    private const string CheckWriteCommandName = "web_public_file_check_write_internal";
    private const string ReadCommandName = "web_public_file_read_internal";
    private const string SliceCommandName = "web_public_file_slice_internal";
    private const string WriteCommandName = "web_public_file_write_internal";
    private const string WriteWithPermissionsCommandName = "web_public_file_write_with_permissions_internal";
    private const string ChangeOwnerCommandName = "web_change_owner_internal";
    private const string ChangeModeCommandName = "web_change_mode_internal";

    private static readonly IReadOnlyDictionary<string, string> DefaultContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html",
            [".htm"] = "text/html",
            [".css"] = "text/css",
            [".js"] = "text/javascript",
            [".mjs"] = "text/javascript",
            [".txt"] = "text/plain",
            [".json"] = "application/json",
            [".xml"] = "application/xml",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".ico"] = "image/x-icon",
            [".zip"] = "application/zip",
            [".gz"] = "application/gzip",
            [".tgz"] = "application/gzip",
            [".tar"] = "application/x-tar",
            [".bz2"] = "application/x-bzip2",
            [".xz"] = "application/x-xz",
            [".br"] = "application/x-brotli",
        };

    private static readonly IReadOnlySet<string> DeniedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".php", ".cgi", ".pl", ".py", ".rb", ".sh", ".bash", ".exe", ".dll", ".so", ".jar", ".war",
        };

    private static readonly Regex LinuxPrincipalRegex = new(
        @"^([A-Za-z_][A-Za-z0-9_-]{0,31}|[1-9][0-9]{0,9})$",
        RegexOptions.CultureInvariant);

    private static readonly Regex SearchNamePatternRegex = new(
        @"^[A-Za-z0-9._*?@+-]{1,128}$",
        RegexOptions.CultureInvariant);

    private static readonly UTF8Encoding StrictUtf8Encoding = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly ILogger<WebPublicFileProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebPublicFileProvider"/> class.
    /// </summary>
    /// <param name="logger">Audit logger.</param>
    public WebPublicFileProvider(ILogger<WebPublicFileProvider>? logger = null)
    {
        _logger = logger ?? NullLogger<WebPublicFileProvider>.Instance;
    }

    /// <inheritdoc />
    public async Task<WebPublicFileListResult> ListAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        int maxDepth = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var pathError = ValidatePermissionPath(normalizedPath, site);
        if (pathError is not null)
        {
            return CreateListError(site, normalizedPath, pathError);
        }

        if (maxDepth is < 0 or > 5)
        {
            return CreateListError(site, normalizedPath, "MaxDepth must be between 0 and 5.");
        }

        if (limit is < 1 or > 500)
        {
            return CreateListError(site, normalizedPath, "Limit must be between 1 and 500.");
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            ListCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["maxDepth"] = maxDepth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateListError(
                site,
                normalizedPath,
                $"Web public file list failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
        }

        var remote = JsonSerializer.Deserialize<RemoteListResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web public file list returned empty JSON.");

        return new WebPublicFileListResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Exists,
            remote.Entries,
            remote.Truncated,
            Warnings: []);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileListResult> SearchNameAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string pattern,
        int maxDepth = 3,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var normalizedPattern = pattern.Trim();
        if (!IsSafeSearchNamePattern(normalizedPattern))
        {
            return CreateListError(site, normalizedPath, "Pattern must be a safe file-name glob without path separators.");
        }

        var listResult = await ListAsync(
            sshCommandService,
            profile,
            site.SiteKey,
            normalizedPath,
            maxDepth,
            limit,
            cancellationToken);
        if (listResult.Error is not null)
        {
            return listResult;
        }

        var matcher = GlobToRegex(normalizedPattern);
        var entries = listResult.Entries
            .Where(entry => matcher.IsMatch(entry.Name))
            .ToArray();

        return listResult with
        {
            Entries = entries,
            Warnings = listResult.Truncated
                ? ["Search was applied after the bounded directory scan; increase limit if expected matches are missing."]
                : [],
        };
    }

    /// <inheritdoc />
    public async Task<WebPublicTextSearchResult> SearchTextAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string query,
        string path = "/",
        int maxDepth = 3,
        int limit = 50,
        int maxFileBytes = 262144,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var normalizedQuery = query.Trim();
        if (!IsSafeTextSearchQuery(normalizedQuery))
        {
            return CreateTextSearchError(site, normalizedPath, normalizedQuery, "Query must be 1 to 128 printable characters without control characters.");
        }

        if (maxDepth is < 0 or > 5)
        {
            return CreateTextSearchError(site, normalizedPath, normalizedQuery, "MaxDepth must be between 0 and 5.");
        }

        if (limit is < 1 or > 200)
        {
            return CreateTextSearchError(site, normalizedPath, normalizedQuery, "Limit must be between 1 and 200.");
        }

        if (maxFileBytes is < 1 or > 1048576)
        {
            return CreateTextSearchError(site, normalizedPath, normalizedQuery, "MaxFileBytes must be between 1 and 1048576.");
        }

        var listResult = await ListAsync(
            sshCommandService,
            profile,
            site.SiteKey,
            normalizedPath,
            maxDepth,
            limit: Math.Min(500, limit * 5),
            cancellationToken);
        if (listResult.Error is not null)
        {
            return CreateTextSearchError(site, normalizedPath, normalizedQuery, listResult.Error);
        }

        var matches = new List<WebPublicTextSearchMatch>();
        var warnings = new List<string>();
        foreach (var entry in listResult.Entries.Where(entry => entry.Type == "file"))
        {
            if (matches.Count >= limit)
            {
                break;
            }

            if (entry.Size > maxFileBytes)
            {
                continue;
            }

            var contentType = ResolveContentType(entry.Path, site, null);
            if (!IsTextSearchContentType(contentType))
            {
                continue;
            }

            var readResult = await ReadFileAsync(
                sshCommandService,
                profile,
                site.SiteKey,
                entry.Path,
                cancellationToken);
            if (readResult.Error is not null || !readResult.Exists || string.IsNullOrWhiteSpace(readResult.ContentBase64))
            {
                continue;
            }

            if (!TryDecodeUtf8(readResult.ContentBase64, out var content))
            {
                continue;
            }

            var lineNumber = 0L;
            foreach (var line in SplitTextLines(content))
            {
                lineNumber++;
                if (line.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new WebPublicTextSearchMatch(
                        entry.Path,
                        entry.ResolvedPath,
                        lineNumber,
                        TruncateLine(line),
                        entry.Size));
                    if (matches.Count >= limit)
                    {
                        break;
                    }
                }
            }
        }

        if (listResult.Truncated)
        {
            warnings.Add("Search was applied after the bounded directory scan; increase limit if expected matches are missing.");
        }

        var truncated = listResult.Truncated || matches.Count >= limit;
        if (matches.Count >= limit)
        {
            warnings.Add("Search result limit was reached.");
        }

        return new WebPublicTextSearchResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            listResult.ResolvedPath,
            normalizedQuery,
            listResult.Exists,
            matches,
            truncated,
            warnings);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileStatResult> StatAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var pathError = ValidatePermissionPath(normalizedPath, site);
        if (pathError is not null)
        {
            return CreateStatError(site, normalizedPath, pathError);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            StatCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateStatError(
                site,
                normalizedPath,
                $"Web public file stat failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
        }

        var remote = JsonSerializer.Deserialize<RemoteStatResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web public file stat returned empty JSON.");

        return new WebPublicFileStatResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Exists,
            remote.Type ?? string.Empty,
            remote.Size,
            remote.Mode ?? string.Empty,
            remote.Owner ?? string.Empty,
            remote.Group ?? string.Empty,
            remote.LastModified,
            remote.IsSymlink,
            Warnings: []);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileHashResult> HashAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string? algorithm = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var correlationId = Guid.NewGuid().ToString("N");
        var normalizedSiteKey = string.IsNullOrWhiteSpace(siteKey) ? string.Empty : siteKey.Trim();
        var normalizedPath = NormalizePath(path);
        var normalizedAlgorithm = string.IsNullOrWhiteSpace(algorithm) ? "sha256" : algorithm.Trim().ToLowerInvariant();
        if (!string.Equals(normalizedAlgorithm, "sha256", StringComparison.Ordinal))
        {
            return CreateHashError(profile.Name, normalizedSiteKey, normalizedPath, normalizedAlgorithm, "algorithm-not-supported", correlationId);
        }

        if (string.IsNullOrWhiteSpace(normalizedSiteKey))
        {
            return CreateHashError(profile.Name, normalizedSiteKey, normalizedPath, normalizedAlgorithm, "site-not-found", correlationId);
        }

        if (string.IsNullOrWhiteSpace(path) || !path.Trim().StartsWith("/", StringComparison.Ordinal))
        {
            return CreateHashError(profile.Name, normalizedSiteKey, normalizedPath, normalizedAlgorithm, "invalid-path", correlationId);
        }

        WebPublicSite site;
        try
        {
            site = ResolveSite(profile, normalizedSiteKey);
        }
        catch (InvalidOperationException)
        {
            return CreateHashError(profile.Name, normalizedSiteKey, normalizedPath, normalizedAlgorithm, "site-not-found", correlationId);
        }

        var access = ValidatePath(normalizedPath, site, requireWrite: false);
        if (access.Error is not null)
        {
            var code = IsSafeSiteRelativePath(normalizedPath) ? "file-not-allowed" : "invalid-path";
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, code, correlationId);
        }

        var contentType = ResolveContentType(normalizedPath, site, null);
        if (!IsContentTypeAllowed(contentType, site, requireWrite: false, access.IsExplicitRule))
        {
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, "file-not-allowed", correlationId);
        }

        SshCommandResult commandResult;
        try
        {
            commandResult = await sshCommandService.ExecuteAsync(
                profile,
                HashCommandName,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["siteRootBase64"] = EncodeArgument(site.RootPath),
                    ["pathBase64"] = EncodeArgument(normalizedPath),
                    ["maxBytes"] = site.MaxReadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                channel: KelpieExecutionChannel.Mcp,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "web file hash cancelled or timed out. Profile={ProfileName}, SiteKey={SiteKey}, Path={Path}, Algorithm={Algorithm}, Result={Result}, CorrelationId={CorrelationId}",
                profile.Name,
                site.SiteKey,
                normalizedPath,
                normalizedAlgorithm,
                "remote-timeout",
                correlationId);
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, "remote-timeout", correlationId);
        }
        catch (SshCommandConnectionException ex)
        {
            var errorCode = ex.TimedOut ? "remote-timeout" : "remote-read-failed";
            _logger.LogWarning(
                "web file hash SSH connection failed. Profile={ProfileName}, SiteKey={SiteKey}, Path={Path}, Algorithm={Algorithm}, TimedOut={TimedOut}, Result={Result}, CorrelationId={CorrelationId}",
                profile.Name,
                site.SiteKey,
                normalizedPath,
                normalizedAlgorithm,
                ex.TimedOut,
                errorCode,
                correlationId);
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, errorCode, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "web file hash SSH execution failed. Profile={ProfileName}, SiteKey={SiteKey}, Path={Path}, Algorithm={Algorithm}, ExceptionType={ExceptionType}, Result={Result}, CorrelationId={CorrelationId}",
                profile.Name,
                site.SiteKey,
                normalizedPath,
                normalizedAlgorithm,
                ex.GetType().FullName ?? "UnknownException",
                "remote-read-failed",
                correlationId);
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, "remote-read-failed", correlationId);
        }

        if (commandResult.TimedOut)
        {
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, "remote-timeout", correlationId);
        }

        if (commandResult.ExitCode != 0)
        {
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, "remote-read-failed", correlationId);
        }

        RemoteHashResult? remote;
        try
        {
            remote = JsonSerializer.Deserialize<RemoteHashResult>(commandResult.StandardOutput, JsonOptions);
        }
        catch (JsonException)
        {
            remote = null;
        }

        if (remote is null)
        {
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, "invalid-provider-response", correlationId);
        }

        if (!string.IsNullOrWhiteSpace(remote.ErrorCode))
        {
            var safeCode = IsHashErrorCode(remote.ErrorCode) ? remote.ErrorCode : "invalid-provider-response";
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, safeCode, correlationId);
        }

        var expectedResolvedPath = site.RootPath.TrimEnd('/') + normalizedPath;
        if (!string.Equals(remote.Algorithm, "sha256", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(remote.Hash)
            || !Sha256Regex().IsMatch(remote.Hash)
            || remote.Size < 0
            || remote.Size > site.MaxReadBytes
            || remote.IsSymlink
            || !string.Equals(remote.ResolvedPath, expectedResolvedPath, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(remote.Owner)
            || string.IsNullOrWhiteSpace(remote.Group)
            || string.IsNullOrWhiteSpace(remote.Mode))
        {
            return CreateHashError(profile.Name, site.SiteKey, normalizedPath, normalizedAlgorithm, "invalid-provider-response", correlationId);
        }

        _logger.LogInformation(
            "web file hash completed. Profile={ProfileName}, SiteKey={SiteKey}, Path={Path}, Size={Size}, Algorithm={Algorithm}, Hash={Hash}, Result={Result}, CorrelationId={CorrelationId}",
            profile.Name,
            site.SiteKey,
            normalizedPath,
            remote.Size,
            normalizedAlgorithm,
            remote.Hash,
            "success",
            correlationId);
        return new WebPublicFileHashResult(
            profile.Name,
            site.SiteKey,
            normalizedPath,
            remote.ResolvedPath!,
            normalizedAlgorithm,
            remote.Hash,
            remote.Size,
            remote.Owner,
            remote.Group,
            remote.Mode,
            IsSymlink: false,
            Warnings: [],
            Error: null);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileWriteCheckResult> CheckWriteAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string? contentType = null,
        bool usePrivilegedHelper = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var access = ValidatePath(normalizedPath, site, requireWrite: true);
        var resolvedContentType = ResolveContentType(normalizedPath, site, contentType);
        var confirmation = CreateWebFileWriteConfirmation(site.SiteKey, normalizedPath);
        if (access.Error is not null)
        {
            return CreateWriteCheckResult(site, normalizedPath, resolvedContentType, canWrite: false, access.Error);
        }

        if (!IsContentTypeAllowed(resolvedContentType, site, requireWrite: true, access.IsExplicitRule))
        {
            return CreateWriteCheckResult(
                site,
                normalizedPath,
                resolvedContentType,
                canWrite: false,
                $"Content type is not writable: {resolvedContentType}");
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            CheckWriteCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["createDirectories"] = site.CreateDirectories ? "1" : "0",
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateWriteCheckResult(
                site,
                normalizedPath,
                resolvedContentType,
                canWrite: false,
                $"Web public file write check failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
        }

        var remote = JsonSerializer.Deserialize<RemoteWriteCheckResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web public file write check returned empty JSON.");

        var helperAvailable = false;
        var helperCreateAllowed = false;
        if (usePrivilegedHelper && (remote.CanWrite || IsPermissionOnlyWriteFailure(remote.Reason)))
        {
            var helperResult = await sshCommandService.ExecuteAsync(
                profile,
                "web_public_file_check_managed_internal",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["siteRootBase64"] = EncodeArgument(site.RootPath),
                    ["pathBase64"] = EncodeArgument(normalizedPath),
                    ["create"] = remote.Exists ? "0" : "1",
                },
                channel: KelpieExecutionChannel.Mcp,
                cancellationToken: cancellationToken);
            if (helperResult.ExitCode == 0)
            {
                var managed = JsonSerializer.Deserialize<RemoteManagedWriteCheckResult>(helperResult.StandardOutput, JsonOptions)
                    ?? throw new InvalidOperationException("Managed web write check returned empty JSON.");
                helperAvailable = managed.Allowed;
                helperCreateAllowed = managed.CreateAllowed;
            }
        }

        var canWrite = remote.CanWrite || helperAvailable;

        var failure = canWrite
            ? WebPublicWriteFailure.None
            : CreateWriteFailure(remote.Reason);

        return new WebPublicFileWriteCheckResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Exists,
            canWrite,
            RequiresConfirmation: canWrite,
            confirmation,
            resolvedContentType,
            remote.Reason,
            Warnings: [],
            Error: null,
            ReasonCode: failure.ReasonCode,
            Guidance: failure.Guidance,
            CreateAllowed: helperAvailable ? helperCreateAllowed : !remote.Exists && site.CreateDirectories,
            PrivilegedAtomicUpdate: helperAvailable,
            PreservesPermissions: helperAvailable,
            BackupAvailable: helperAvailable,
            RollbackAvailable: helperAvailable,
            ExpectedSha256Supported: helperAvailable,
            PostWriteSha256Supported: helperAvailable);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileWriteCheckResult> CheckSecretWriteAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string secretName,
        string? contentType = null,
        string? owner = null,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var access = ValidateSecretPath(normalizedPath, site, requireWrite: true);
        var resolvedContentType = ResolveSecretContentType(normalizedPath, contentType);
        var permissionRequest = CreateWritePermissionRequest(owner, mode);
        var confirmation = CreateWebSecretFileWriteConfirmation(
            site.SiteKey,
            normalizedPath,
            secretName,
            permissionRequest);
        if (access.Error is not null)
        {
            return CreateSecretWriteCheckResult(site, normalizedPath, resolvedContentType, canWrite: false, access.Error, secretName, permissionRequest);
        }

        if (permissionRequest.Error is not null)
        {
            return CreateSecretWriteCheckResult(site, normalizedPath, resolvedContentType, canWrite: false, permissionRequest.Error, secretName, permissionRequest);
        }

        if (!IsContentTypeAllowed(resolvedContentType, site, requireWrite: true, access.IsExplicitRule))
        {
            return CreateSecretWriteCheckResult(
                site,
                normalizedPath,
                resolvedContentType,
                canWrite: false,
                $"Content type is not writable: {resolvedContentType}",
                secretName,
                permissionRequest);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            CheckWriteCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["createDirectories"] = site.CreateDirectories ? "1" : "0",
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateSecretWriteCheckResult(
                site,
                normalizedPath,
                resolvedContentType,
                canWrite: false,
                $"Web secret file write check failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}",
                secretName,
                permissionRequest);
        }

        var remote = JsonSerializer.Deserialize<RemoteWriteCheckResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web secret file write check returned empty JSON.");

        var failure = remote.CanWrite
            ? WebPublicWriteFailure.None
            : CreateWriteFailure(remote.Reason);

        return new WebPublicFileWriteCheckResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Exists,
            remote.CanWrite,
            RequiresConfirmation: remote.CanWrite,
            confirmation,
            resolvedContentType,
            remote.Reason,
            Warnings: [],
            Error: null,
            ReasonCode: failure.ReasonCode,
            Guidance: failure.Guidance);
    }

    /// <inheritdoc />
    public async Task<WebPublicPermissionCheckResult> CheckPermissionsAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string? owner = null,
        string? group = null,
        string? mode = null,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var pathError = ValidatePermissionPath(normalizedPath, site);
        if (pathError is not null)
        {
            return CreatePermissionCheckError(site, normalizedPath, pathError);
        }

        var normalizedOwner = owner?.Trim() ?? string.Empty;
        var normalizedGroup = group?.Trim() ?? string.Empty;
        var normalizedMode = mode?.Trim() ?? string.Empty;
        var ownerReason = ValidateOwnerGroupForCheck(normalizedOwner, normalizedGroup);
        var modeReason = string.IsNullOrWhiteSpace(normalizedMode)
            ? "Mode is required to check mode changes."
            : ValidateMode(normalizedMode);

        var stat = await StatAsync(
            sshCommandService,
            profile,
            site.SiteKey,
            normalizedPath,
            cancellationToken);
        if (stat.Error is not null)
        {
            return CreatePermissionCheckError(site, normalizedPath, stat.Error);
        }

        var pathReason = stat.Exists
            ? stat.Type == "symlink" ? "Symbolic links are not eligible for permission changes." : null
            : "Target path does not exist.";
        var canChangeOwner = ownerReason is null && pathReason is null;
        var canChangeMode = modeReason is null && pathReason is null;
        var reason = pathReason ?? ownerReason ?? modeReason;
        var ownerOperation = recursive ? "web_change_owner_recursive" : "web_change_owner";
        var modeOperation = recursive ? "web_change_mode_recursive" : "web_change_mode";

        return new WebPublicPermissionCheckResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            stat.ResolvedPath,
            stat.Exists,
            stat.Type,
            stat.Owner,
            stat.Group,
            stat.Mode,
            canChangeOwner,
            canChangeMode,
            canChangeOwner ? $"{ownerOperation}:{site.SiteKey}:{normalizedPath}:{normalizedOwner}:{normalizedGroup}" : string.Empty,
            canChangeMode ? $"{modeOperation}:{site.SiteKey}:{normalizedPath}:{normalizedMode}" : string.Empty,
            reason,
            CreateRecursiveWarnings(recursive),
            Error: canChangeOwner || canChangeMode ? null : reason);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileReadResult> ReadFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var access = ValidatePath(normalizedPath, site, requireWrite: false);
        if (access.Error is not null)
        {
            return CreateReadError(site, normalizedPath, access.Error);
        }

        var resolvedContentType = ResolveContentType(normalizedPath, site, null);
        if (!IsContentTypeAllowed(resolvedContentType, site, requireWrite: false, access.IsExplicitRule))
        {
            return CreateReadError(site, normalizedPath, $"Content type is not readable: {resolvedContentType}");
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            ReadCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["maxBytes"] = site.MaxReadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateReadError(
                site,
                normalizedPath,
                $"Web public file read failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
        }

        var remote = JsonSerializer.Deserialize<RemoteReadResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web public file read returned empty JSON.");

        return new WebPublicFileReadResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Exists,
            remote.Exists ? remote.ContentBase64 : null,
            "utf-8",
            resolvedContentType,
            remote.Size,
            remote.LastModified,
            Warnings: []);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileReadResult> ReadHeadAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        int maxBytes = 4096,
        int maxLines = 100,
        CancellationToken cancellationToken = default)
    {
        return await ReadSliceAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            "head",
            maxBytes,
            maxLines,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileReadResult> ReadTailAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        int maxBytes = 4096,
        int maxLines = 100,
        CancellationToken cancellationToken = default)
    {
        return await ReadSliceAsync(
            sshCommandService,
            profile,
            siteKey,
            path,
            "tail",
            maxBytes,
            maxLines,
            cancellationToken);
    }

    private async Task<WebPublicFileReadResult> ReadSliceAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string mode,
        int maxBytes,
        int maxLines,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var access = ValidatePath(normalizedPath, site, requireWrite: false);
        if (access.Error is not null)
        {
            return CreateReadError(site, normalizedPath, access.Error);
        }

        var resolvedContentType = ResolveContentType(normalizedPath, site, null);
        if (!IsContentTypeAllowed(resolvedContentType, site, requireWrite: false, access.IsExplicitRule))
        {
            return CreateReadError(site, normalizedPath, $"Content type is not readable: {resolvedContentType}");
        }

        if (maxBytes is < 1 or > 1048576)
        {
            return CreateReadError(site, normalizedPath, "MaxBytes must be between 1 and 1048576.");
        }

        if (maxLines is < 0 or > 1000)
        {
            return CreateReadError(site, normalizedPath, "MaxLines must be between 0 and 1000.");
        }

        var boundedMaxBytes = Math.Min(maxBytes, site.MaxReadBytes);
        var result = await sshCommandService.ExecuteAsync(
            profile,
            SliceCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["mode"] = mode,
                ["maxBytes"] = boundedMaxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["maxLines"] = maxLines.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateReadError(
                site,
                normalizedPath,
                $"Web public file {mode} failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
        }

        var remote = JsonSerializer.Deserialize<RemoteReadResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web public file slice returned empty JSON.");
        var warnings = new List<string>
        {
            $"Returned {mode} slice is bounded by maxBytes={boundedMaxBytes} and maxLines={maxLines}.",
        };
        if (boundedMaxBytes != maxBytes)
        {
            warnings.Add($"MaxBytes was reduced to the site MaxReadBytes value: {boundedMaxBytes}.");
        }

        return new WebPublicFileReadResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Exists,
            remote.Exists ? remote.ContentBase64 : null,
            "utf-8",
            resolvedContentType,
            remote.Size,
            remote.LastModified,
            warnings);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileWriteResult> WriteFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string contentBase64,
        string? encoding,
        string? contentType,
        string? owner = null,
        string? mode = null,
        string? expectedSha256 = null,
        bool createBackup = false,
        bool preservePermissions = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var access = ValidatePath(normalizedPath, site, requireWrite: true);
        if (access.Error is not null)
        {
            return CreateWriteError(site, normalizedPath, access.Error);
        }

        if (!TryValidateContent(contentBase64, site, out var size, out var contentError))
        {
            return CreateWriteError(site, normalizedPath, contentError);
        }

        var resolvedContentType = ResolveContentType(normalizedPath, site, contentType);
        if (!IsContentTypeAllowed(resolvedContentType, site, requireWrite: true, access.IsExplicitRule))
        {
            return CreateWriteError(site, normalizedPath, $"Content type is not writable: {resolvedContentType}");
        }

        if (!string.IsNullOrWhiteSpace(encoding) && !string.Equals(encoding, "utf-8", StringComparison.OrdinalIgnoreCase))
        {
            return CreateWriteError(site, normalizedPath, $"Encoding is not supported: {encoding}");
        }

        var permissionRequest = CreateWritePermissionRequest(owner, mode);
        if (permissionRequest.Error is not null)
        {
            return CreateWriteError(site, normalizedPath, permissionRequest.Error);
        }

        if (permissionRequest.HasPermissions || expectedSha256 is not null || createBackup || preservePermissions)
        {
            return await WriteFileWithPermissionsAsync(
                sshCommandService,
                profile,
                site,
                normalizedPath,
                contentBase64,
                resolvedContentType,
                size,
                permissionRequest,
                expectedSha256,
                createBackup,
                preservePermissions,
                cancellationToken);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            WriteCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["maxBytes"] = site.MaxWriteBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["createDirectories"] = site.CreateDirectories ? "1" : "0",
            },
            channel: KelpieExecutionChannel.Mcp,
            standardInput: contentBase64,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateWriteExecutionError(site, normalizedPath, result.StandardError);
        }

        var remote = DeserializeWriteResult(result.StandardOutput);
        if (remote is null)
        {
            return CreateWriteError(site, normalizedPath, "Provider returned an invalid response.", "InvalidProviderResponse");
        }

        if (!string.IsNullOrWhiteSpace(remote.ErrorCode))
        {
            return CreateProviderWriteError(site, normalizedPath, remote.ErrorCode);
        }

        if (remote.Written)
        {
            LogWritableExecutableWrite(profile, site, normalizedPath);
        }

        return new WebPublicFileWriteResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Written,
            remote.Created,
            remote.Overwritten,
            resolvedContentType,
            remote.Size == 0 ? size : remote.Size,
            Warnings: [],
            Error: null,
            Owner: remote.Owner ?? string.Empty,
            Group: remote.Group ?? string.Empty,
            Mode: remote.Mode ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileWriteResult> WriteSecretFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string contentBase64,
        string? contentType,
        string? owner = null,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var access = ValidateSecretPath(normalizedPath, site, requireWrite: true);
        if (access.Error is not null)
        {
            return CreateWriteError(site, normalizedPath, access.Error);
        }

        if (!TryValidateContent(contentBase64, site, out var size, out var contentError))
        {
            return CreateWriteError(site, normalizedPath, contentError);
        }

        var resolvedContentType = ResolveSecretContentType(normalizedPath, contentType);
        if (!IsContentTypeAllowed(resolvedContentType, site, requireWrite: true, access.IsExplicitRule))
        {
            return CreateWriteError(site, normalizedPath, $"Content type is not writable: {resolvedContentType}");
        }

        var permissionRequest = CreateWritePermissionRequest(owner, mode);
        if (permissionRequest.Error is not null)
        {
            return CreateWriteError(site, normalizedPath, permissionRequest.Error);
        }

        if (permissionRequest.HasPermissions)
        {
            return await WriteFileWithPermissionsAsync(
                sshCommandService,
                profile,
                site,
                normalizedPath,
                contentBase64,
                resolvedContentType,
                size,
                permissionRequest,
                expectedSha256: null,
                createBackup: false,
                preservePermissions: false,
                cancellationToken: cancellationToken);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            WriteCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["maxBytes"] = site.MaxWriteBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["createDirectories"] = site.CreateDirectories ? "1" : "0",
            },
            channel: KelpieExecutionChannel.Mcp,
            standardInput: contentBase64,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateWriteExecutionError(site, normalizedPath, result.StandardError);
        }

        var remote = DeserializeWriteResult(result.StandardOutput);
        if (remote is null)
        {
            return CreateWriteError(site, normalizedPath, "Provider returned an invalid response.", "InvalidProviderResponse");
        }

        if (!string.IsNullOrWhiteSpace(remote.ErrorCode))
        {
            return CreateProviderWriteError(site, normalizedPath, remote.ErrorCode);
        }

        return new WebPublicFileWriteResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Written,
            remote.Created,
            remote.Overwritten,
            resolvedContentType,
            remote.Size == 0 ? size : remote.Size,
            Warnings: ["Secret content was not returned."],
            Error: null,
            Owner: remote.Owner ?? string.Empty,
            Group: remote.Group ?? string.Empty,
            Mode: remote.Mode ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileWriteResult> RollbackFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);
        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var access = ValidatePath(normalizedPath, site, requireWrite: true);
        if (access.Error is not null || !Sha256Regex().IsMatch(expectedSha256))
        {
            return CreateWriteError(site, normalizedPath, access.Error ?? "Expected SHA-256 is invalid.");
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            "web_public_file_rollback_internal",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["expectedSha256"] = expectedSha256,
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            return CreateWriteError(site, normalizedPath, $"Web public file rollback failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
        }

        var remote = JsonSerializer.Deserialize<RemoteWriteResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web public file rollback returned empty JSON.");
        return CreateManagedWriteResult(site, normalizedPath, remote);
    }

    /// <inheritdoc />
    public async Task<WebPublicFileWriteResult> CommitFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);
        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var access = ValidatePath(normalizedPath, site, requireWrite: true);
        if (access.Error is not null)
        {
            return CreateWriteError(site, normalizedPath, access.Error);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            "web_public_file_commit_internal",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);
        return result.ExitCode == 0
            ? new WebPublicFileWriteResult(site.SiteKey, site.DisplayName, normalizedPath, string.Empty, false, false, false, string.Empty, 0, ["Managed backup committed."])
            : CreateWriteError(site, normalizedPath, $"Web public file commit failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
    }

    private static WebPublicFileWriteResult CreateManagedWriteResult(WebPublicSite site, string normalizedPath, RemoteWriteResult remote)
    {
        return new WebPublicFileWriteResult(
            site.SiteKey, site.DisplayName, normalizedPath, remote.ResolvedPath ?? string.Empty,
            remote.Written, remote.Created, remote.Overwritten, string.Empty, remote.Size, [],
            Owner: remote.Owner ?? string.Empty, Group: remote.Group ?? string.Empty, Mode: remote.Mode ?? string.Empty,
            PreviousSha256: remote.PreviousSha256 ?? string.Empty, Sha256: remote.Sha256 ?? string.Empty,
            BackupPath: remote.BackupPath ?? string.Empty, PermissionsPreserved: remote.PermissionsPreserved);
    }

    private async Task<WebPublicFileWriteResult> WriteFileWithPermissionsAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        WebPublicSite site,
        string normalizedPath,
        string contentBase64,
        string resolvedContentType,
        long size,
        WritePermissionRequest permissionRequest,
        string? expectedSha256,
        bool createBackup,
        bool preservePermissions,
        CancellationToken cancellationToken)
    {
        var result = await sshCommandService.ExecuteAsync(
            profile,
            WriteWithPermissionsCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["maxBytes"] = site.MaxWriteBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["createDirectories"] = site.CreateDirectories ? "1" : "0",
                ["ownerBase64"] = EncodeOptionalArgument(permissionRequest.OwnerSpec),
                ["modeBase64"] = EncodeOptionalArgument(permissionRequest.Mode),
                ["expectedSha256"] = expectedSha256 ?? "-",
                ["backup"] = createBackup ? "1" : "0",
                ["preservePermissions"] = preservePermissions ? "1" : "0",
            },
            channel: KelpieExecutionChannel.Mcp,
            standardInput: contentBase64,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateWriteExecutionError(site, normalizedPath, result.StandardError);
        }

        var remote = JsonSerializer.Deserialize<RemoteWriteResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web public file write returned empty JSON.");

        if (remote.Written)
        {
            LogWritableExecutableWrite(profile, site, normalizedPath);
        }

        return new WebPublicFileWriteResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Written,
            remote.Created,
            remote.Overwritten,
            resolvedContentType,
            remote.Size == 0 ? size : remote.Size,
            Warnings: [],
            Error: null,
            Owner: remote.Owner ?? permissionRequest.Owner,
            Group: remote.Group ?? string.Empty,
            Mode: remote.Mode ?? permissionRequest.Mode,
            PreviousSha256: remote.PreviousSha256 ?? string.Empty,
            Sha256: remote.Sha256 ?? string.Empty,
            BackupPath: remote.BackupPath ?? string.Empty,
            PermissionsPreserved: remote.PermissionsPreserved);
    }

    /// <inheritdoc />
    public async Task<WebPublicPermissionChangeResult> ChangeOwnerAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string owner,
        string group,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var pathError = ValidatePermissionPath(normalizedPath, site);
        if (pathError is not null)
        {
            return CreatePermissionError(site, normalizedPath, owner, group, mode: string.Empty, pathError);
        }

        var ownerError = ValidateLinuxPrincipal(owner, "Owner");
        if (ownerError is not null)
        {
            return CreatePermissionError(site, normalizedPath, owner, group, mode: string.Empty, ownerError);
        }

        var groupError = ValidateLinuxPrincipal(group, "Group");
        if (groupError is not null)
        {
            return CreatePermissionError(site, normalizedPath, owner, group, mode: string.Empty, groupError);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            ChangeOwnerCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["ownerBase64"] = EncodeArgument(owner.Trim()),
                ["groupBase64"] = EncodeArgument(group.Trim()),
                ["recursive"] = recursive ? "1" : "0",
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreatePermissionError(
                site,
                normalizedPath,
                owner,
                group,
                mode: string.Empty,
                $"Web public owner change failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
        }

        var remote = JsonSerializer.Deserialize<RemotePermissionChangeResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web public owner change returned empty JSON.");

        return new WebPublicPermissionChangeResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Changed,
            remote.Owner ?? owner.Trim(),
            remote.Group ?? group.Trim(),
            remote.Mode ?? string.Empty,
            Warnings: CreateRecursiveWarnings(recursive));
    }

    /// <inheritdoc />
    public async Task<WebPublicPermissionChangeResult> ChangeModeAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string mode,
        bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var site = ResolveSite(profile, siteKey);
        var normalizedPath = NormalizePath(path);
        var pathError = ValidatePermissionPath(normalizedPath, site);
        if (pathError is not null)
        {
            return CreatePermissionError(site, normalizedPath, owner: string.Empty, group: string.Empty, mode, pathError);
        }

        var normalizedMode = mode.Trim();
        var modeError = ValidateMode(normalizedMode);
        if (modeError is not null)
        {
            return CreatePermissionError(site, normalizedPath, owner: string.Empty, group: string.Empty, normalizedMode, modeError);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            ChangeModeCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["siteRootBase64"] = EncodeArgument(site.RootPath),
                ["pathBase64"] = EncodeArgument(normalizedPath),
                ["mode"] = normalizedMode,
                ["recursive"] = recursive ? "1" : "0",
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreatePermissionError(
                site,
                normalizedPath,
                owner: string.Empty,
                group: string.Empty,
                normalizedMode,
                $"Web public mode change failed. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
        }

        var remote = JsonSerializer.Deserialize<RemotePermissionChangeResult>(result.StandardOutput, JsonOptions)
            ?? throw new InvalidOperationException("Web public mode change returned empty JSON.");

        return new WebPublicPermissionChangeResult(
            site.SiteKey,
            site.DisplayName,
            normalizedPath,
            remote.ResolvedPath ?? string.Empty,
            remote.Changed,
            remote.Owner ?? string.Empty,
            remote.Group ?? string.Empty,
            remote.Mode ?? normalizedMode,
            Warnings: CreateRecursiveWarnings(recursive));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static WebPublicSite ResolveSite(SshConnectionProfile profile, string siteKey)
    {
        var normalizedSiteKey = string.IsNullOrWhiteSpace(siteKey) ? "default" : siteKey.Trim();
        var sites = profile.WebPublicSites.Count == 0
            ? new[] { CreateDefaultSite() }
            : profile.WebPublicSites;
        return sites.FirstOrDefault(site => string.Equals(site.SiteKey, normalizedSiteKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Unsupported siteKey: {siteKey}");
    }

    private static WebPublicSite CreateDefaultSite()
    {
        return new WebPublicSite
        {
            SiteKey = "default",
            DisplayName = "Default Web Site",
            RootPath = "/var/www/html",
            AllowedExtensions = DefaultContentTypes.Keys.ToArray(),
            AllowedContentTypes = CreateContentTypeRules(DefaultContentTypes.Values
                .Append("application/javascript")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()),
            CreateDirectories = true,
        };
    }

    private static string CreateWebFileWriteConfirmation(string siteKey, string path)
    {
        return $"web_file_write:{siteKey}:{path}";
    }

    private static string CreateWebSecretFileWriteConfirmation(
        string siteKey,
        string path,
        string secretName,
        WritePermissionRequest permissionRequest)
    {
        var permissionSuffix = permissionRequest.HasPermissions
            ? $":{permissionRequest.OwnerSpec}:{permissionRequest.Mode}"
            : string.Empty;

        return $"web_secret_file_write:{siteKey}:{path}:{secretName.Trim()}{permissionSuffix}";
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : "/" + path.Trim().TrimStart('/');
    }

    private static WebPublicFileAccess ValidatePath(
        string path,
        WebPublicSite site,
        bool requireWrite)
    {
        if (!IsSafeAbsoluteUnixPath(site.RootPath))
        {
            return new WebPublicFileAccess(false, "Web public root must be a safe absolute Unix path.");
        }

        if (!IsSafeSiteRelativePath(path))
        {
            return new WebPublicFileAccess(false, "Requested path must be an absolute site-relative path without traversal.");
        }

        var fileName = path.Split('/').Last();
        if (fileName.StartsWith(".", StringComparison.Ordinal)
            || path.Split('/').Any(part => part.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            || string.Equals(fileName, "id_rsa", StringComparison.OrdinalIgnoreCase)
            || SecretFileRegex().IsMatch(fileName))
        {
            return new WebPublicFileAccess(false, "Requested path is denied by web public file safety rules.");
        }

        var extension = System.IO.Path.GetExtension(fileName);
        var rule = FindAllowedFileRule(path, fileName, site);
        if (rule is not null)
        {
            if (!rule.Access.HasFlag(AllowedRootAccess.Read))
            {
                return new WebPublicFileAccess(true, "Requested file is not readable by AllowedFiles.");
            }

            if (requireWrite && !rule.Access.HasFlag(AllowedRootAccess.Write))
            {
                return new WebPublicFileAccess(true, "Requested file is not writable by AllowedFiles.");
            }

            if (requireWrite
                && DeniedExtensions.Contains(extension)
                && !IsWritableExecutableExtensionAllowed(site, extension))
            {
                return new WebPublicFileAccess(true, "Requested file extension is denied for writing.");
            }

            return new WebPublicFileAccess(true, Error: null);
        }

        if (DeniedExtensions.Contains(extension)
            && (!requireWrite || !IsWritableExecutableExtensionAllowed(site, extension)))
        {
            return new WebPublicFileAccess(false, "Requested file extension is denied.");
        }

        if (site.AllowedFiles.Count > 0)
        {
            return new WebPublicFileAccess(true, "Requested file is not allowed by AllowedFiles.");
        }

        var allowedExtensions = site.AllowedExtensions.Count == 0
            ? DefaultContentTypes.Keys
            : site.AllowedExtensions;
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            && (!requireWrite || !IsWritableExecutableExtensionAllowed(site, extension)))
        {
            return new WebPublicFileAccess(false, $"Requested file extension is not allowed: {extension}");
        }

        return new WebPublicFileAccess(false, Error: null);
    }

    private static WebPublicFileAccess ValidateSecretPath(
        string path,
        WebPublicSite site,
        bool requireWrite)
    {
        if (!IsSafeAbsoluteUnixPath(site.RootPath))
        {
            return new WebPublicFileAccess(false, "Web public root must be a safe absolute Unix path.");
        }

        if (!IsSafeSiteRelativePath(path))
        {
            return new WebPublicFileAccess(false, "Requested path must be an absolute site-relative path without traversal.");
        }

        var fileName = path.Split('/').Last();
        if (path.Split('/').Any(part => part.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            || string.Equals(fileName, "id_rsa", StringComparison.OrdinalIgnoreCase))
        {
            return new WebPublicFileAccess(false, "Requested path is denied by web secret file safety rules.");
        }

        if (!SecretFileRegex().IsMatch(fileName))
        {
            return new WebPublicFileAccess(false, "Requested path is not a supported secret file name.");
        }

        var rule = FindAllowedFileRule(path, fileName, site);
        if (rule is null)
        {
            return new WebPublicFileAccess(false, "Secret file writes require an explicit writable AllowedFiles rule.");
        }

        if (!rule.Access.HasFlag(AllowedRootAccess.Write))
        {
            return new WebPublicFileAccess(true, "Requested secret file is not writable by AllowedFiles.");
        }

        return new WebPublicFileAccess(true, Error: null);
    }

    private static bool IsWritableExecutableExtensionAllowed(WebPublicSite site, string extension)
    {
        return !string.IsNullOrWhiteSpace(extension)
            && DeniedExtensions.Contains(extension)
            && site.WritableExecutableExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private void LogWritableExecutableWrite(
        SshConnectionProfile profile,
        WebPublicSite site,
        string normalizedPath)
    {
        var extension = System.IO.Path.GetExtension(normalizedPath);
        if (!IsWritableExecutableExtensionAllowed(site, extension))
        {
            return;
        }

        _logger.LogWarning(
            "executable web file written under explicit profile permission. Profile={ProfileName}, SiteKey={SiteKey}, Path={Path}, Extension={Extension}",
            profile.Name,
            site.SiteKey,
            normalizedPath,
            extension);
    }

    private static bool TryValidateContent(
        string contentBase64,
        WebPublicSite site,
        out long size,
        out string error)
    {
        size = 0;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            error = "ContentBase64 is required.";
            return false;
        }

        byte[] contentBytes;
        try
        {
            contentBytes = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException)
        {
            error = "ContentBase64 is not valid Base64.";
            return false;
        }

        if (contentBytes.Length > site.MaxWriteBytes)
        {
            error = $"Content exceeds the maximum write size of {site.MaxWriteBytes} bytes.";
            return false;
        }

        size = contentBytes.Length;
        return true;
    }

    private static string ResolveContentType(string path, WebPublicSite site, string? requestedContentType)
    {
        if (!string.IsNullOrWhiteSpace(requestedContentType))
        {
            return requestedContentType.Trim();
        }

        var extension = System.IO.Path.GetExtension(path);
        return DefaultContentTypes.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    private static string ResolveSecretContentType(string path, string? requestedContentType)
    {
        return !string.IsNullOrWhiteSpace(requestedContentType)
            ? requestedContentType.Trim()
            : "text/plain";
    }

    private static bool IsContentTypeAllowed(
        string contentType,
        WebPublicSite site,
        bool requireWrite,
        bool isExplicitRule)
    {
        if (isExplicitRule && site.AllowedContentTypes.Count == 0)
        {
            return true;
        }

        var allowedContentTypes = site.AllowedContentTypes.Count == 0
            ? CreateContentTypeRules(DefaultContentTypes.Values.Append("application/javascript"))
            : site.AllowedContentTypes;
        var rule = allowedContentTypes.FirstOrDefault(item =>
            string.Equals(item.ContentType, contentType, StringComparison.OrdinalIgnoreCase));
        return rule is not null
            && (requireWrite
                ? rule.Access.HasFlag(AllowedRootAccess.Write)
                : rule.Access.HasFlag(AllowedRootAccess.Read));
    }

    private static IReadOnlyCollection<WebPublicContentTypeRule> CreateContentTypeRules(IEnumerable<string> contentTypes)
    {
        return contentTypes
            .Where(contentType => !string.IsNullOrWhiteSpace(contentType))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(contentType => new WebPublicContentTypeRule(contentType, AllowedRootAccess.Read | AllowedRootAccess.Write))
            .ToArray();
    }

    private static WebPublicFileRule? FindAllowedFileRule(
        string path,
        string fileName,
        WebPublicSite site)
    {
        return site.AllowedFiles.FirstOrDefault(rule => IsAllowedFileRuleMatch(rule.Pattern, path, fileName));
    }

    private static bool IsAllowedFileRuleMatch(
        string pattern,
        string path,
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var normalizedRulePattern = NormalizeAllowedFileRulePattern(pattern);
        var target = normalizedRulePattern.Contains('/', StringComparison.Ordinal)
            ? path
            : fileName;
        var normalizedPattern = normalizedRulePattern.Contains('/', StringComparison.Ordinal)
            ? "/" + normalizedRulePattern.Trim().TrimStart('/')
            : normalizedRulePattern.Trim();
        return GlobToRegex(normalizedPattern).IsMatch(target);
    }

    private static string NormalizeAllowedFileRulePattern(string pattern)
    {
        var trimmed = pattern.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal)
            && !trimmed.Contains('*', StringComparison.Ordinal)
            && !trimmed.Contains('?', StringComparison.Ordinal)
            && !trimmed.Contains('/', StringComparison.Ordinal)
            ? "*" + trimmed
            : trimmed;
    }

    private static Regex GlobToRegex(string pattern)
    {
        var regexText = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", @"[^/]*", StringComparison.Ordinal)
            .Replace(@"\?", @"[^/]", StringComparison.Ordinal) + "$";
        return new Regex(regexText, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private static bool IsSafeSearchNamePattern(string pattern)
    {
        return SearchNamePatternRegex.IsMatch(pattern)
            && !pattern.Contains('/', StringComparison.Ordinal)
            && !pattern.Contains('\\', StringComparison.Ordinal)
            && pattern is not "." and not "..";
    }

    private static bool IsSafeTextSearchQuery(string query)
    {
        return query.Length is >= 1 and <= 128
            && !query.Any(char.IsControl);
    }

    private static bool IsTextSearchContentType(string contentType)
    {
        return contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/xml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/javascript", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/svg+xml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDecodeUtf8(
        string contentBase64,
        out string content)
    {
        content = string.Empty;
        try
        {
            content = StrictUtf8Encoding.GetString(Convert.FromBase64String(contentBase64));
            return !content.Contains('\0');
        }
        catch (FormatException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static string[] SplitTextLines(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string TruncateLine(string line)
    {
        const int MaxLineLength = 500;
        return line.Length <= MaxLineLength
            ? line
            : line[..MaxLineLength];
    }

    private static bool IsSafeAbsoluteUnixPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Contains('\0') || path.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        return !path.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    }

    private static bool IsSafeSiteRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Contains('\0') || path.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        return !path.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    }

    private static string? ValidatePermissionPath(string path, WebPublicSite site)
    {
        if (!IsSafeAbsoluteUnixPath(site.RootPath))
        {
            return "Web public root must be a safe absolute Unix path.";
        }

        if (!IsSafeSiteRelativePath(path))
        {
            return "Requested path must be an absolute site-relative path without traversal.";
        }

        var fileName = path.Split('/').LastOrDefault() ?? string.Empty;
        if (path.Split('/').Any(part => part.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            || string.Equals(fileName, "id_rsa", StringComparison.OrdinalIgnoreCase)
            || SecretFileRegex().IsMatch(fileName))
        {
            return "Requested path is denied by web public permission safety rules.";
        }

        return null;
    }

    private static string? ValidateLinuxPrincipal(string value, string label)
    {
        var normalizedValue = value.Trim();
        if (!LinuxPrincipalRegex.IsMatch(normalizedValue))
        {
            return $"{label} must be a safe Linux user or group name.";
        }

        return string.Equals(normalizedValue, "root", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedValue, "0", StringComparison.Ordinal)
            ? $"{label} must not be root."
            : null;
    }

    private static string? ValidateMode(string mode)
    {
        if (!Regex.IsMatch(mode, "^[0-7]{3}$", RegexOptions.CultureInvariant))
        {
            return "Mode must be a 3-digit octal value.";
        }

        var otherDigit = mode[2] - '0';
        return (otherDigit & 0b010) != 0
            ? "Mode must not be world-writable."
            : null;
    }

    private static string? ValidateOwnerGroupForCheck(string owner, string group)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(group))
        {
            return "Owner and group are required to check owner changes.";
        }

        return ValidateLinuxPrincipal(owner, "Owner")
            ?? ValidateLinuxPrincipal(group, "Group");
    }

    private static WritePermissionRequest CreateWritePermissionRequest(
        string? owner,
        string? mode)
    {
        var hasOwner = !string.IsNullOrWhiteSpace(owner);
        var hasMode = !string.IsNullOrWhiteSpace(mode);
        if (!hasOwner && !hasMode)
        {
            return WritePermissionRequest.None;
        }

        var normalizedOwnerSpec = owner?.Trim() ?? string.Empty;
        var normalizedOwner = string.Empty;
        var normalizedGroup = string.Empty;
        if (hasOwner)
        {
            var ownerParts = normalizedOwnerSpec.Split(':');
            if (ownerParts.Length > 2 || string.IsNullOrWhiteSpace(ownerParts[0]))
            {
                return new WritePermissionRequest(false, normalizedOwnerSpec, string.Empty, string.Empty, string.Empty, "Owner must be in owner[:group] form.");
            }

            normalizedOwner = ownerParts[0].Trim();
            normalizedGroup = ownerParts.Length == 2 ? ownerParts[1].Trim() : string.Empty;
            var ownerError = ValidateLinuxPrincipal(normalizedOwner, "Owner");
            if (ownerError is not null)
            {
                return new WritePermissionRequest(false, normalizedOwnerSpec, normalizedOwner, normalizedGroup, string.Empty, ownerError);
            }

            if (!string.IsNullOrWhiteSpace(normalizedGroup))
            {
                var groupError = ValidateLinuxPrincipal(normalizedGroup, "Group");
                if (groupError is not null)
                {
                    return new WritePermissionRequest(false, normalizedOwnerSpec, normalizedOwner, normalizedGroup, string.Empty, groupError);
                }
            }
        }

        var normalizedMode = mode?.Trim() ?? string.Empty;
        var modeError = ValidateMode(normalizedMode);
        if (hasMode && modeError is not null)
        {
            return new WritePermissionRequest(false, normalizedOwnerSpec, normalizedOwner, normalizedGroup, normalizedMode, modeError);
        }

        return new WritePermissionRequest(true, normalizedOwnerSpec, normalizedOwner, normalizedGroup, normalizedMode, null);
    }

    private static string EncodeArgument(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private static string EncodeOptionalArgument(string value)
    {
        return EncodeArgument(value.Length == 0 ? OptionalArgumentNone : value);
    }

    private static WebPublicFileReadResult CreateReadError(
        WebPublicSite site,
        string path,
        string error)
    {
        return new WebPublicFileReadResult(
            site.SiteKey,
            site.DisplayName,
            path,
            ResolvedPath: string.Empty,
            Exists: false,
            ContentBase64: null,
            Encoding: "utf-8",
            ContentType: ResolveContentType(path, site, null),
            Size: 0,
            LastModified: null,
            Warnings: [],
            Error: error);
    }

    private static WebPublicTextSearchResult CreateTextSearchError(
        WebPublicSite site,
        string path,
        string query,
        string error)
    {
        return new WebPublicTextSearchResult(
            site.SiteKey,
            site.DisplayName,
            path,
            ResolvedPath: string.Empty,
            query,
            Exists: false,
            Matches: [],
            Truncated: false,
            Warnings: [],
            Error: error);
    }

    private static WebPublicFileWriteResult CreateWriteError(
        WebPublicSite site,
        string path,
        string error,
        string? reasonCode = null)
    {
        var failure = CreateWriteFailure(error);
        return new WebPublicFileWriteResult(
            site.SiteKey,
            site.DisplayName,
            path,
            ResolvedPath: string.Empty,
            Written: false,
            Created: false,
            Overwritten: false,
            ContentType: ResolveContentType(path, site, null),
            Size: 0,
            Warnings: [],
            Error: error,
            ReasonCode: reasonCode ?? failure.ReasonCode,
            Guidance: failure.Guidance);
    }

    private static WebPublicFileListResult CreateListError(
        WebPublicSite site,
        string path,
        string error)
    {
        return new WebPublicFileListResult(
            site.SiteKey,
            site.DisplayName,
            path,
            ResolvedPath: string.Empty,
            Exists: false,
            Entries: [],
            Truncated: false,
            Warnings: [],
            Error: error);
    }

    private static WebPublicFileStatResult CreateStatError(
        WebPublicSite site,
        string path,
        string error)
    {
        return new WebPublicFileStatResult(
            site.SiteKey,
            site.DisplayName,
            path,
            ResolvedPath: string.Empty,
            Exists: false,
            Type: string.Empty,
            Size: 0,
            Mode: string.Empty,
            Owner: string.Empty,
            Group: string.Empty,
            LastModified: null,
            IsSymlink: false,
            Warnings: [],
            Error: error);
    }

    private WebPublicFileHashResult CreateHashError(
        string profileName,
        string siteKey,
        string path,
        string algorithm,
        string code,
        string correlationId)
    {
        _logger.LogWarning(
            "web file hash failed. Profile={ProfileName}, SiteKey={SiteKey}, Path={Path}, Size={Size}, Algorithm={Algorithm}, Result={Result}, ErrorCode={ErrorCode}, CorrelationId={CorrelationId}",
            profileName,
            siteKey,
            path,
            0,
            algorithm,
            "failure",
            code,
            correlationId);
        return new WebPublicFileHashResult(
            profileName,
            siteKey,
            path,
            ResolvedPath: string.Empty,
            algorithm,
            Hash: null,
            Size: 0,
            Owner: string.Empty,
            Group: string.Empty,
            Mode: string.Empty,
            IsSymlink: false,
            Warnings: [],
            Error: new WebPublicFileHashError(code, CreateHashErrorMessage(code), correlationId));
    }

    private static bool IsHashErrorCode(string code)
    {
        return code is "invalid-path"
            or "path-outside-root"
            or "file-not-found"
            or "file-type-not-supported"
            or "file-too-large"
            or "remote-read-failed"
            or "file-changed-during-read";
    }

    private static string CreateHashErrorMessage(string code)
    {
        return code switch
        {
            "site-not-found" => "The requested web public site was not found.",
            "invalid-path" => "The requested path is invalid.",
            "path-outside-root" => "The requested path resolves outside the web public root.",
            "file-not-allowed" => "The requested file is not readable under the web public policy.",
            "file-not-found" => "The requested file was not found.",
            "file-type-not-supported" => "The requested path is not a supported regular file.",
            "file-too-large" => "The requested file exceeds MaxReadBytes.",
            "algorithm-not-supported" => "Only sha256 is supported.",
            "remote-timeout" => "The remote hash operation timed out.",
            "file-changed-during-read" => "The file changed while its hash was being calculated.",
            "invalid-provider-response" => "The remote hash provider returned an invalid response.",
            _ => "The remote file could not be hashed safely.",
        };
    }

    private static WebPublicFileWriteCheckResult CreateWriteCheckResult(
        WebPublicSite site,
        string path,
        string contentType,
        bool canWrite,
        string? reason)
    {
        var failure = canWrite
            ? WebPublicWriteFailure.None
            : CreateWriteFailure(reason);

        return new WebPublicFileWriteCheckResult(
            site.SiteKey,
            site.DisplayName,
            path,
            ResolvedPath: string.Empty,
            Exists: false,
            canWrite,
            RequiresConfirmation: canWrite,
            CreateWebFileWriteConfirmation(site.SiteKey, path),
            contentType,
            reason,
            Warnings: [],
            Error: reason,
            ReasonCode: failure.ReasonCode,
            Guidance: failure.Guidance);
    }

    private static RemoteWriteResult? DeserializeWriteResult(string standardOutput)
    {
        try
        {
            return JsonSerializer.Deserialize<RemoteWriteResult>(standardOutput, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WebPublicFileWriteResult CreateProviderWriteError(
        WebPublicSite site,
        string path,
        string providerCode)
    {
        var (reasonCode, message) = providerCode switch
        {
            "invalid-content-base64" => ("InvalidContentBase64", "Content is not valid Base64."),
            "write-size-exceeded" => ("MaxWriteBytesExceeded", "Content exceeds the configured maximum write size."),
            "invalid-path" => ("InvalidPath", "The requested path is invalid."),
            "path-outside-root" => ("PathOutsideRoot", "The resolved path is outside the configured web root."),
            "parent-not-found" => ("ParentDirectoryNotFound", "The target parent directory does not exist."),
            "symlink-rejected" => ("SymlinkRejected", "Symbolic links are not writable."),
            "file-type-not-supported" => ("FileTypeNotSupported", "The target is not a regular file."),
            "write-permission-denied" => ("RemoteFileSystemPermissionDenied", "The remote file system denied the write."),
            "remote-write-failed" => ("RemoteWriteFailed", "The remote atomic write failed."),
            _ => ("InvalidProviderResponse", "Provider returned an unsupported error code."),
        };
        return CreateWriteError(site, path, message, reasonCode);
    }

    private static WebPublicFileWriteResult CreateWriteExecutionError(
        WebPublicSite site,
        string path,
        string standardError)
    {
        var permissionDenied = standardError.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || standardError.Contains("password is required", StringComparison.OrdinalIgnoreCase)
            || standardError.Contains("not allowed", StringComparison.OrdinalIgnoreCase);
        return permissionDenied
            ? CreateWriteError(site, path, "The remote file system denied the write.", "RemoteFileSystemPermissionDenied")
            : CreateWriteError(site, path, "The remote atomic write failed.", "RemoteWriteFailed");
    }

    private static WebPublicFileWriteCheckResult CreateSecretWriteCheckResult(
        WebPublicSite site,
        string path,
        string contentType,
        bool canWrite,
        string? reason,
        string secretName,
        WritePermissionRequest permissionRequest)
    {
        var failure = canWrite
            ? WebPublicWriteFailure.None
            : CreateWriteFailure(reason);

        return new WebPublicFileWriteCheckResult(
            site.SiteKey,
            site.DisplayName,
            path,
            ResolvedPath: string.Empty,
            Exists: false,
            canWrite,
            RequiresConfirmation: canWrite,
            CreateWebSecretFileWriteConfirmation(site.SiteKey, path, secretName, permissionRequest),
            contentType,
            reason,
            Warnings: [],
            Error: reason,
            ReasonCode: failure.ReasonCode,
            Guidance: failure.Guidance);
    }

    private static WebPublicWriteFailure CreateWriteFailure(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return WebPublicWriteFailure.None;
        }

        if (reason.Contains("not writable by AllowedFiles", StringComparison.OrdinalIgnoreCase))
        {
            return new WebPublicWriteFailure(
                "AllowedFilesWritePermissionMissing",
                "The matching AllowedFiles rule does not grant Write permission. Add Write to the matching AllowedFiles rule or choose another allowed path.");
        }

        if (reason.Contains("not allowed by AllowedFiles", StringComparison.OrdinalIgnoreCase))
        {
            return new WebPublicWriteFailure(
                "AllowedFilesRuleMissing",
                "No AllowedFiles rule permits this path. Add an explicit writable AllowedFiles rule for the target file or choose another allowed path.");
        }

        if (reason.Contains("Secret file writes require an explicit writable AllowedFiles rule", StringComparison.OrdinalIgnoreCase))
        {
            return new WebPublicWriteFailure(
                "SecretAllowedFilesRuleMissing",
                "Secret file writes require an explicit AllowedFiles rule with Write permission for the target secret file.");
        }

        if (reason.Contains("file extension is denied for writing", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("file extension is denied", StringComparison.OrdinalIgnoreCase))
        {
            return new WebPublicWriteFailure(
                "WritableExecutableExtensionMissing",
                "This executable web extension is not writable by the current profile. For PHP files, add .php to WebPublicSites.<siteKey>.WritableExecutableExtensions for the target site.");
        }

        if (reason.Contains("file extension is not allowed", StringComparison.OrdinalIgnoreCase))
        {
            return new WebPublicWriteFailure(
                "AllowedExtensionMissing",
                "The file extension is not allowed for this site. Add the extension to AllowedExtensions, or for PHP files explicitly allow .php in WritableExecutableExtensions.");
        }

        if (reason.Contains("Content type is not writable", StringComparison.OrdinalIgnoreCase))
        {
            return new WebPublicWriteFailure(
                "ContentTypeWritePermissionMissing",
                "The resolved content type does not grant Write permission. Add Write to the matching AllowedContentTypes rule or pass an allowed content type.");
        }

        if (reason.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("access denied", StringComparison.OrdinalIgnoreCase))
        {
            return new WebPublicWriteFailure(
                "RemoteFileSystemPermissionDenied",
                "The remote SSH user or helper could not write the target path. Check the directory owner, group, mode, or use the owner/mode options with the configured permission helper.");
        }

        return new WebPublicWriteFailure(
            "WriteRejected",
            "The write request was rejected by KelpieSSH policy or by the remote preflight check. Review Error and Reason for the exact condition.");
    }

    private static bool IsPermissionOnlyWriteFailure(string? reason)
    {
        return reason is not null
            && (reason.Contains("not writable by the SSH user", StringComparison.OrdinalIgnoreCase)
                || reason.Contains("Parent directory is not writable by the SSH user", StringComparison.OrdinalIgnoreCase));
    }

    private static WebPublicPermissionChangeResult CreatePermissionError(
        WebPublicSite site,
        string path,
        string owner,
        string group,
        string mode,
        string error)
    {
        return new WebPublicPermissionChangeResult(
            site.SiteKey,
            site.DisplayName,
            path,
            ResolvedPath: string.Empty,
            Changed: false,
            owner,
            group,
            mode,
            Warnings: [],
            Error: error);
    }

    private static WebPublicPermissionCheckResult CreatePermissionCheckError(
        WebPublicSite site,
        string path,
        string error)
    {
        return new WebPublicPermissionCheckResult(
            site.SiteKey,
            site.DisplayName,
            path,
            ResolvedPath: string.Empty,
            Exists: false,
            Type: string.Empty,
            CurrentOwner: string.Empty,
            CurrentGroup: string.Empty,
            CurrentMode: string.Empty,
            CanChangeOwner: false,
            CanChangeMode: false,
            OwnerConfirmation: string.Empty,
            ModeConfirmation: string.Empty,
            Reason: error,
            Warnings: [],
            Error: error);
    }

    private static IReadOnlyList<string> CreateRecursiveWarnings(bool recursive)
    {
        return recursive
            ? ["Recursive permission change skips symbolic links."]
            : [];
    }

    private static string CreateSafeErrorDetail(string standardError)
    {
        var firstLine = standardError
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return "No error detail was returned.";
        }

        var lines = standardError
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return firstLine.StartsWith("Traceback", StringComparison.OrdinalIgnoreCase)
            ? lines.LastOrDefault() ?? firstLine
            : firstLine;
    }

    [GeneratedRegex(@"(?i)^(\.env(\..*)?|\.htaccess|\.htpasswd|.*\.pem|.*\.key)$", RegexOptions.CultureInvariant)]
    private static partial Regex SecretFileRegex();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    private sealed class RemoteReadResult
    {
        public string? ResolvedPath { get; set; }

        public bool Exists { get; set; }

        public string? ContentBase64 { get; set; }

        public long Size { get; set; }

        public string? LastModified { get; set; }
    }

    private sealed class RemoteListResult
    {
        public string? ResolvedPath { get; set; }

        public bool Exists { get; set; }

        public List<WebPublicFileListEntry> Entries { get; set; } = [];

        public bool Truncated { get; set; }
    }

    private sealed class RemoteStatResult
    {
        public string? ResolvedPath { get; set; }

        public bool Exists { get; set; }

        public string? Type { get; set; }

        public long Size { get; set; }

        public string? Mode { get; set; }

        public string? Owner { get; set; }

        public string? Group { get; set; }

        public string? LastModified { get; set; }

        public bool IsSymlink { get; set; }
    }

    private sealed class RemoteHashResult
    {
        public string? ResolvedPath { get; set; }

        public string? Algorithm { get; set; }

        public string? Hash { get; set; }

        public long Size { get; set; }

        public string? Owner { get; set; }

        public string? Group { get; set; }

        public string? Mode { get; set; }

        public bool IsSymlink { get; set; }

        public string? ErrorCode { get; set; }
    }

    private sealed class RemoteWriteCheckResult
    {
        public string? ResolvedPath { get; set; }

        public bool Exists { get; set; }

        public bool CanWrite { get; set; }

        public string? Reason { get; set; }
    }

    private sealed class RemoteManagedWriteCheckResult
    {
        public bool Allowed { get; set; }

        public bool CreateAllowed { get; set; }
    }

    private sealed class RemoteWriteResult
    {
        public string? ResolvedPath { get; set; }

        public bool Written { get; set; }

        public bool Created { get; set; }

        public bool Overwritten { get; set; }

        public long Size { get; set; }

        public string? Owner { get; set; }

        public string? Group { get; set; }

        public string? Mode { get; set; }

        public string? PreviousSha256 { get; set; }

        public string? Sha256 { get; set; }

        public string? BackupPath { get; set; }

        public bool PermissionsPreserved { get; set; }

        public string? ErrorCode { get; set; }
    }

    private sealed class RemotePermissionChangeResult
    {
        public string? ResolvedPath { get; set; }

        public bool Changed { get; set; }

        public string? Owner { get; set; }

        public string? Group { get; set; }

        public string? Mode { get; set; }
    }

    private sealed record WebPublicFileAccess(
        bool IsExplicitRule,
        string? Error);

    private sealed record WebPublicWriteFailure(
        string? ReasonCode,
        string? Guidance)
    {
        public static WebPublicWriteFailure None { get; } = new(null, null);
    }

    private sealed record WritePermissionRequest(
        bool HasPermissions,
        string OwnerSpec,
        string Owner,
        string Group,
        string Mode,
        string? Error)
    {
        public static WritePermissionRequest None { get; } = new(false, string.Empty, string.Empty, string.Empty, string.Empty, null);
    }
}
