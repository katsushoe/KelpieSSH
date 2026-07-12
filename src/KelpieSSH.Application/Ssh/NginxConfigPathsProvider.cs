using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Discovers Nginx configuration file paths.
/// </summary>
public sealed partial class NginxConfigPathsProvider : IServiceConfigPathsProvider, IServiceConfigFileReader, IServiceConfigFileWriter, IServiceConfigFileBackupManager, IServiceConfigFileTester, IServiceLogfileReader, IServiceConfigFileAccessChecker, INginxPhpConfigurator
{
    private const int MaxReadBytes = 65536;
    private const int MaxWriteBytes = 65536;
    private const int MaxLogReadBytes = 262144;
    private const int DefaultLogLines = 500;
    private const int MaxLogLines = 5000;
    private const int MaxSinceMinutes = 1440;
    private const string NginxVersionCommandName = "service_config_nginx_version";
    private const string NginxReadConfigCommandName = "service_config_nginx_read_config";
    private const string NginxCanWriteConfigCommandName = "service_config_nginx_check_write_config";
    private const string NginxWriteConfigCommandName = "service_config_nginx_write_config";
    private const string NginxRollbackConfigCommandName = "service_config_nginx_rollback_config";
    private const string NginxCommitConfigCommandName = "service_config_nginx_commit_config";
    private const string NginxTestConfigCommandName = "service_config_nginx_test_config";
    private const string NginxDisableDefaultSitesCommandName = "service_config_nginx_disable_default_sites";
    private const string NginxRollbackDefaultSitesCommandName = "service_config_nginx_rollback_default_sites";
    private const string NginxReadLogCommandName = "service_logfile_nginx_read";

    private static readonly IReadOnlyDictionary<string, string> LogPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["access"] = "/var/log/nginx/access.log",
        ["error"] = "/var/log/nginx/error.log",
    };

    /// <inheritdoc />
    public string ServiceKey => "nginx";

    /// <inheritdoc />
    public string DisplayName => "Nginx";

    /// <inheritdoc />
    public async Task<ServiceConfigPathsResult> GetConfigPathsAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var warnings = new List<string>();
        var result = await sshCommandService.ExecuteAsync(
            profile,
            NginxVersionCommandName,
            arguments: null,
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            warnings.Add("nginx -V exited with a non-zero status.");
        }

        var versionOutput = result.StandardOutput + "\n" + result.StandardError;
        var mainConfig = ExtractConfPath(versionOutput);
        if (string.IsNullOrWhiteSpace(mainConfig))
        {
            warnings.Add("nginx -V output did not include --conf-path.");
        }

        var includePatterns = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(mainConfig) && IsSafeAbsoluteUnixPath(mainConfig))
        {
            var configReadResult = await sshCommandService.ExecuteAsync(
                profile,
                NginxReadConfigCommandName,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pathBase64"] = EncodeArgument(mainConfig),
                    ["allowedPathsBase64"] = EncodeLines([mainConfig]),
                    ["allowedDirsBase64"] = EncodeLines([]),
                    ["maxBytes"] = MaxReadBytes.ToString(CultureInfo.InvariantCulture),
                },
                channel: KelpieExecutionChannel.Mcp,
                cancellationToken: cancellationToken);

            if (configReadResult.ExitCode == 0)
            {
                includePatterns = ExtractIncludePatterns(configReadResult.StandardOutput)
                    .Where(IsSafeAbsoluteUnixPathPattern)
                    .ToArray();
            }
            else
            {
                warnings.Add("Nginx main configuration could not be read for include discovery.");
            }
        }

        return new ServiceConfigPathsResult(
            ServiceKey,
            DisplayName,
            mainConfig,
            string.IsNullOrWhiteSpace(mainConfig) ? [] : [mainConfig],
            includePatterns,
            warnings);
    }

    /// <inheritdoc />
    public async Task<ServiceConfigFileReadResult> ReadConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var paths = await GetConfigPathsAsync(sshCommandService, profile, cancellationToken);
        var requestedPath = string.IsNullOrWhiteSpace(path) ? paths.MainConfig : path.Trim();
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return CreateError(null, "Nginx main configuration path could not be discovered.", paths.Warnings);
        }

        var access = CreateConfigFileAccess(requestedPath, paths);
        var validationError = access.Error;
        if (validationError is not null)
        {
            return CreateError(requestedPath, validationError, paths.Warnings);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            NginxReadConfigCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pathBase64"] = EncodeArgument(requestedPath),
                ["allowedPathsBase64"] = EncodeLines(access.ExactPaths),
                ["allowedDirsBase64"] = EncodeLines(access.AllowedDirectories),
                ["maxBytes"] = MaxReadBytes.ToString(CultureInfo.InvariantCulture),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateError(
                requestedPath,
                $"Nginx config file read failed through provider {DisplayName}. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}",
                paths.Warnings);
        }

        var warnings = paths.Warnings.Concat(CreateReadWarnings(result.StandardOutput, result.StandardError)).ToArray();
        return new ServiceConfigFileReadResult(
            ServiceKey,
            DisplayName,
            requestedPath,
            result.StandardOutput,
            "utf-8",
            result.StandardError.Contains("KELPIE_TRUNCATED=1", StringComparison.Ordinal),
            warnings);
    }

    /// <inheritdoc />
    public async Task<ServiceConfigFileAccessCheckResult> CanReadConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var paths = await GetConfigPathsAsync(sshCommandService, profile, cancellationToken);
        var requestedPath = string.IsNullOrWhiteSpace(path) ? paths.MainConfig : path.Trim();
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return CreateAccessCheckResult(
                null,
                canRead: false,
                canWrite: false,
                reason: "Nginx main configuration path could not be discovered.",
                warnings: paths.Warnings);
        }

        var access = CreateConfigFileAccess(requestedPath, paths);
        if (access.Error is not null)
        {
            return CreateAccessCheckResult(
                requestedPath,
                canRead: false,
                canWrite: false,
                reason: access.Error,
                warnings: paths.Warnings);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            NginxReadConfigCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pathBase64"] = EncodeArgument(requestedPath),
                ["allowedPathsBase64"] = EncodeLines(access.ExactPaths),
                ["allowedDirsBase64"] = EncodeLines(access.AllowedDirectories),
                ["maxBytes"] = "1",
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateAccessCheckResult(
                requestedPath,
                canRead: false,
                canWrite: false,
                reason: $"Nginx config file read check failed through provider {DisplayName}. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}",
                warnings: paths.Warnings);
        }

        return CreateAccessCheckResult(
            requestedPath,
            canRead: true,
            canWrite: false,
            reason: null,
            warnings: paths.Warnings);
    }

    /// <inheritdoc />
    public async Task<ServiceConfigFileWriteResult> WriteConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string path,
        string targetKey,
        string method,
        string? targetValue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var paths = await GetConfigPathsAsync(sshCommandService, profile, cancellationToken);
        var requestedPath = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
        var access = CreateConfigFileAccess(requestedPath, paths);
        if (access.Error is not null)
        {
            return CreateWriteError(requestedPath, access.Error, paths.Warnings);
        }

        var readResult = await sshCommandService.ExecuteAsync(
            profile,
            NginxReadConfigCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pathBase64"] = EncodeArgument(requestedPath),
                ["allowedPathsBase64"] = EncodeLines(access.ExactPaths),
                ["allowedDirsBase64"] = EncodeLines(access.AllowedDirectories),
                ["maxBytes"] = MaxReadBytes.ToString(CultureInfo.InvariantCulture),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (readResult.ExitCode != 0)
        {
            return CreateWriteError(
                requestedPath,
                $"Nginx config file read failed through provider {DisplayName}. ExitCode={readResult.ExitCode}. {CreateSafeErrorDetail(readResult.StandardError)}",
                paths.Warnings);
        }

        var editMatcher = new NginxConfigEditMatcher();
        if (!editMatcher.TryApply(
            readResult.StandardOutput,
            requestedPath,
            targetKey,
            method,
            targetValue,
            out var updatedContent,
            out var editError))
        {
            return CreateWriteError(requestedPath, editError, paths.Warnings);
        }

        var updatedBytes = Encoding.UTF8.GetBytes(updatedContent);
        if (updatedBytes.Length > MaxWriteBytes)
        {
            return CreateWriteError(
                requestedPath,
                $"Edited content exceeds the maximum write size of {MaxWriteBytes} bytes.",
                paths.Warnings);
        }

        var contentBase64 = Convert.ToBase64String(updatedBytes);
        var result = await sshCommandService.ExecuteAsync(
            profile,
            NginxWriteConfigCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pathBase64"] = EncodeArgument(requestedPath),
                ["allowedPathsBase64"] = EncodeLines(access.ExactPaths),
                ["allowedDirsBase64"] = EncodeLines(access.AllowedDirectories),
            },
            channel: KelpieExecutionChannel.Mcp,
            standardInput: contentBase64,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateWriteError(
                requestedPath,
                $"Nginx config file write failed through provider {DisplayName}. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}",
                paths.Warnings);
        }

        return new ServiceConfigFileWriteResult(
            ServiceKey,
            DisplayName,
            requestedPath,
            "utf-8",
            updatedBytes.Length,
            paths.Warnings);
    }

    /// <inheritdoc />
    public async Task<ServiceConfigFileAccessCheckResult> CanWriteConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string path,
        string targetKey,
        string method,
        string? targetValue = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var paths = await GetConfigPathsAsync(sshCommandService, profile, cancellationToken);
        var requestedPath = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
        var confirmation = CreateWriteConfirmation(requestedPath, targetKey, method);
        var access = CreateConfigFileAccess(requestedPath, paths);
        if (access.Error is not null)
        {
            return CreateAccessCheckResult(
                requestedPath,
                canRead: false,
                canWrite: false,
                reason: access.Error,
                warnings: paths.Warnings,
                method,
                targetKey,
                confirmation);
        }

        var readResult = await sshCommandService.ExecuteAsync(
            profile,
            NginxReadConfigCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pathBase64"] = EncodeArgument(requestedPath),
                ["allowedPathsBase64"] = EncodeLines(access.ExactPaths),
                ["allowedDirsBase64"] = EncodeLines(access.AllowedDirectories),
                ["maxBytes"] = MaxReadBytes.ToString(CultureInfo.InvariantCulture),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (readResult.ExitCode != 0)
        {
            return CreateAccessCheckResult(
                requestedPath,
                canRead: false,
                canWrite: false,
                reason: $"Nginx config file read check failed through provider {DisplayName}. ExitCode={readResult.ExitCode}. {CreateSafeErrorDetail(readResult.StandardError)}",
                warnings: paths.Warnings,
                method,
                targetKey,
                confirmation);
        }

        var editMatcher = new NginxConfigEditMatcher();
        if (!editMatcher.TryApply(
            readResult.StandardOutput,
            requestedPath,
            targetKey,
            method,
            targetValue,
            out var updatedContent,
            out var editError))
        {
            return CreateAccessCheckResult(
                requestedPath,
                canRead: true,
                canWrite: false,
                reason: editError,
                warnings: paths.Warnings,
                method,
                targetKey,
                confirmation);
        }

        var updatedBytes = Encoding.UTF8.GetBytes(updatedContent);
        if (updatedBytes.Length > MaxWriteBytes)
        {
            return CreateAccessCheckResult(
                requestedPath,
                canRead: true,
                canWrite: false,
                reason: $"Edited content exceeds the maximum write size of {MaxWriteBytes} bytes.",
                warnings: paths.Warnings,
                method,
                targetKey,
                confirmation);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            NginxCanWriteConfigCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pathBase64"] = EncodeArgument(requestedPath),
                ["allowedPathsBase64"] = EncodeLines(access.ExactPaths),
                ["allowedDirsBase64"] = EncodeLines(access.AllowedDirectories),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateAccessCheckResult(
                requestedPath,
                canRead: true,
                canWrite: false,
                reason: $"Nginx config file write check failed through provider {DisplayName}. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}",
                warnings: paths.Warnings,
                method,
                targetKey,
                confirmation);
        }

        return CreateAccessCheckResult(
            requestedPath,
            canRead: true,
            canWrite: true,
            reason: null,
            warnings: paths.Warnings,
            method,
            targetKey,
            confirmation);
    }

    /// <inheritdoc />
    public async Task<ServiceConfigFileBackupActionResult> RollbackConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string path,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteBackupActionAsync(
            sshCommandService,
            profile,
            path,
            NginxRollbackConfigCommandName,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceConfigFileBackupActionResult> CommitConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string path,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteBackupActionAsync(
            sshCommandService,
            profile,
            path,
            NginxCommitConfigCommandName,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<NginxPhpEnableResult> EnablePhpAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string socketPath,
        string extension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var normalizedSiteKey = string.IsNullOrWhiteSpace(siteKey) ? "default" : siteKey.Trim();
        var normalizedSocketPath = string.IsNullOrWhiteSpace(socketPath) ? string.Empty : socketPath.Trim();
        var normalizedExtension = string.IsNullOrWhiteSpace(extension) ? ".php" : extension.Trim();
        if (!IsSafeSiteKey(normalizedSiteKey))
        {
            return CreatePhpEnableError(normalizedSiteKey, null, normalizedSocketPath, normalizedExtension, "Nginx site key is invalid.", []);
        }

        if (!IsSafePhpSocketPath(normalizedSocketPath))
        {
            return CreatePhpEnableError(normalizedSiteKey, null, normalizedSocketPath, normalizedExtension, "PHP-FPM socketPath is invalid.", []);
        }

        if (!IsSafePhpExtension(normalizedExtension))
        {
            return CreatePhpEnableError(normalizedSiteKey, null, normalizedSocketPath, normalizedExtension, "PHP extension is invalid.", []);
        }

        var paths = await GetConfigPathsAsync(sshCommandService, profile, cancellationToken);
        var requestedPath = ResolveSiteConfigPath(normalizedSiteKey, paths);
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return CreatePhpEnableError(normalizedSiteKey, null, normalizedSocketPath, normalizedExtension, "Nginx site configuration path could not be resolved.", paths.Warnings);
        }

        var access = CreateConfigFileAccess(requestedPath, paths);
        if (access.Error is not null)
        {
            return CreatePhpEnableError(normalizedSiteKey, requestedPath, normalizedSocketPath, normalizedExtension, access.Error, paths.Warnings);
        }

        var readResult = await sshCommandService.ExecuteAsync(
            profile,
            NginxReadConfigCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pathBase64"] = EncodeArgument(requestedPath),
                ["allowedPathsBase64"] = EncodeLines(access.ExactPaths),
                ["allowedDirsBase64"] = EncodeLines(access.AllowedDirectories),
                ["maxBytes"] = MaxReadBytes.ToString(CultureInfo.InvariantCulture),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        var warnings = paths.Warnings;
        var contentToEdit = readResult.StandardOutput;
        if (readResult.ExitCode != 0)
        {
            if (!IsMissingConfigFileError(readResult.StandardError))
            {
                return CreatePhpEnableError(
                    normalizedSiteKey,
                    requestedPath,
                    normalizedSocketPath,
                    normalizedExtension,
                    $"Nginx config file read failed through provider {DisplayName}. ExitCode={readResult.ExitCode}. {CreateSafeErrorDetail(readResult.StandardError)}",
                    paths.Warnings);
            }

            contentToEdit = CreateDefaultPhpSiteConfig();
            warnings = paths.Warnings
                .Concat(["Nginx site configuration file did not exist; generated a fixed default server block."])
                .ToArray();
        }

        if (!TryApplyPhpTemplate(
            contentToEdit,
            normalizedSocketPath,
            normalizedExtension,
            out var updatedContent,
            out var configChanged,
            out var editError))
        {
            return CreatePhpEnableError(normalizedSiteKey, requestedPath, normalizedSocketPath, normalizedExtension, editError, paths.Warnings);
        }

        if (configChanged)
        {
            var updatedBytesForSizeCheck = Encoding.UTF8.GetBytes(updatedContent);
            if (updatedBytesForSizeCheck.Length > MaxWriteBytes)
            {
                return CreatePhpEnableError(
                    normalizedSiteKey,
                    requestedPath,
                    normalizedSocketPath,
                    normalizedExtension,
                    $"Edited content exceeds the maximum write size of {MaxWriteBytes} bytes.",
                    warnings);
            }
        }

        var updatedBytes = configChanged ? Encoding.UTF8.GetBytes(updatedContent) : [];
        if (configChanged)
        {
            var writeResult = await sshCommandService.ExecuteAsync(
                profile,
                NginxWriteConfigCommandName,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["pathBase64"] = EncodeArgument(requestedPath),
                    ["allowedPathsBase64"] = EncodeLines(access.ExactPaths),
                    ["allowedDirsBase64"] = EncodeLines(access.AllowedDirectories),
                },
                channel: KelpieExecutionChannel.Mcp,
                standardInput: Convert.ToBase64String(updatedBytes),
                cancellationToken: cancellationToken);

            if (writeResult.ExitCode != 0)
            {
                return CreatePhpEnableError(
                    normalizedSiteKey,
                    requestedPath,
                    normalizedSocketPath,
                    normalizedExtension,
                    $"Nginx config file write failed through provider {DisplayName}. ExitCode={writeResult.ExitCode}. {CreateSafeErrorDetail(writeResult.StandardError)}",
                    warnings);
            }
        }

        var disableDefaultSitesResult = await DisableConflictingDefaultSitesAsync(sshCommandService, profile, cancellationToken);
        var disabledDefaultSitePaths = SplitOutputLines(disableDefaultSitesResult.StandardOutput)
            .Where(IsSafeSitesEnabledPath)
            .ToArray();
        if (disableDefaultSitesResult.ExitCode != 0)
        {
            var rollbackResult = configChanged
                ? await RollbackConfigFileAsync(sshCommandService, profile, requestedPath, cancellationToken)
                : null;
            var rollbackError = rollbackResult?.Error is null
                ? string.Empty
                : $" Rollback failed: {rollbackResult.Error}";
            return new NginxPhpEnableResult(
                ServiceKey,
                DisplayName,
                normalizedSiteKey,
                requestedPath,
                normalizedSocketPath,
                normalizedExtension,
                Changed: configChanged,
                Tested: false,
                RolledBack: configChanged && rollbackResult?.Error is null,
                Committed: false,
                updatedBytes.Length,
                warnings.Concat(rollbackResult?.Warnings ?? []).ToArray(),
                $"Nginx conflicting default site disable failed through provider {DisplayName}. ExitCode={disableDefaultSitesResult.ExitCode}. {CreateSafeErrorDetail(disableDefaultSitesResult.StandardError)}{rollbackError}");
        }

        if (disabledDefaultSitePaths.Length > 0)
        {
            warnings = warnings
                .Concat([$"Disabled {disabledDefaultSitePaths.Length.ToString(CultureInfo.InvariantCulture)} conflicting Nginx default_server site link(s)."])
                .ToArray();
        }

        if (!configChanged && disabledDefaultSitePaths.Length == 0)
        {
            return new NginxPhpEnableResult(
                ServiceKey,
                DisplayName,
                normalizedSiteKey,
                requestedPath,
                normalizedSocketPath,
                normalizedExtension,
                Changed: false,
                Tested: false,
                RolledBack: false,
                Committed: false,
                BytesWritten: 0,
                warnings);
        }

        var testResult = await TestConfigFileAsync(sshCommandService, profile, cancellationToken);
        if (testResult.Error is not null)
        {
            var rollbackResult = configChanged
                ? await RollbackConfigFileAsync(sshCommandService, profile, requestedPath, cancellationToken)
                : null;
            var defaultSitesRollbackResult = disabledDefaultSitePaths.Length > 0
                ? await RollbackConflictingDefaultSitesAsync(sshCommandService, profile, disabledDefaultSitePaths, cancellationToken)
                : null;
            var rollbackError = rollbackResult?.Error is null
                ? string.Empty
                : $" Rollback failed: {rollbackResult.Error}";
            var defaultSitesRollbackError = defaultSitesRollbackResult?.Error is null
                ? string.Empty
                : $" Default site rollback failed: {defaultSitesRollbackResult.Error}";
            return new NginxPhpEnableResult(
                ServiceKey,
                DisplayName,
                normalizedSiteKey,
                requestedPath,
                normalizedSocketPath,
                normalizedExtension,
                Changed: true,
                Tested: true,
                RolledBack: (rollbackResult?.Error is null) && (defaultSitesRollbackResult?.Error is null),
                Committed: false,
                updatedBytes.Length,
                warnings.Concat(testResult.Warnings).Concat(rollbackResult?.Warnings ?? []).Concat(defaultSitesRollbackResult?.Warnings ?? []).ToArray(),
                testResult.Error + rollbackError + defaultSitesRollbackError);
        }

        var commitResult = configChanged
            ? await CommitConfigFileAsync(sshCommandService, profile, requestedPath, cancellationToken)
            : null;
        return new NginxPhpEnableResult(
            ServiceKey,
            DisplayName,
            normalizedSiteKey,
            requestedPath,
            normalizedSocketPath,
            normalizedExtension,
            Changed: true,
            Tested: true,
            RolledBack: false,
            Committed: commitResult?.Error is null,
            updatedBytes.Length,
            warnings.Concat(testResult.Warnings).Concat(commitResult?.Warnings ?? []).ToArray(),
            commitResult?.Error);
    }

    private static async Task<SshCommandResult> DisableConflictingDefaultSitesAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        CancellationToken cancellationToken)
    {
        return await sshCommandService.ExecuteAsync(
            profile,
            NginxDisableDefaultSitesCommandName,
            arguments: null,
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);
    }

    private async Task<ServiceConfigFileBackupActionResult> RollbackConflictingDefaultSitesAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        IReadOnlyList<string> disabledDefaultSitePaths,
        CancellationToken cancellationToken)
    {
        var result = await sshCommandService.ExecuteAsync(
            profile,
            NginxRollbackDefaultSitesCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["disabledPathsBase64"] = EncodeLines(disabledDefaultSitePaths),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateBackupActionError(
                "/etc/nginx/sites-enabled",
                "/etc/nginx/.kelpie-disabled-sites",
                $"Nginx default site rollback failed through provider {DisplayName}. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}",
                []);
        }

        return new ServiceConfigFileBackupActionResult(
            ServiceKey,
            DisplayName,
            "/etc/nginx/sites-enabled",
            "/etc/nginx/.kelpie-disabled-sites",
            Changed: true,
            Warnings: [],
            Error: null);
    }

    private async Task<ServiceConfigFileBackupActionResult> ExecuteBackupActionAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string path,
        string commandName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var paths = await GetConfigPathsAsync(sshCommandService, profile, cancellationToken);
        var requestedPath = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
        var backupPath = GetBackupPath(requestedPath);
        var access = CreateConfigFileAccess(requestedPath, paths);
        if (access.Error is not null)
        {
            return CreateBackupActionError(requestedPath, backupPath, access.Error, paths.Warnings);
        }

        var result = await sshCommandService.ExecuteAsync(
            profile,
            commandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pathBase64"] = EncodeArgument(requestedPath),
                ["allowedPathsBase64"] = EncodeLines(access.ExactPaths),
                ["allowedDirsBase64"] = EncodeLines(access.AllowedDirectories),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            var actionName = commandName == NginxRollbackConfigCommandName ? "rollback" : "commit";
            return CreateBackupActionError(
                requestedPath,
                backupPath,
                $"Nginx config file {actionName} failed through provider {DisplayName}. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}",
                paths.Warnings);
        }

        return new ServiceConfigFileBackupActionResult(
            ServiceKey,
            DisplayName,
            requestedPath,
            backupPath,
            Changed: true,
            paths.Warnings);
    }

    /// <inheritdoc />
    public async Task<ServiceConfigFileTestResult> TestConfigFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var result = await sshCommandService.ExecuteAsync(
            profile,
            NginxTestConfigCommandName,
            arguments: null,
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        return new ServiceConfigFileTestResult(
            ServiceKey,
            DisplayName,
            "nginx -t",
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            SplitOutputLines(result.StandardOutput),
            SplitOutputLines(result.StandardError),
            Warnings: [],
            Error: result.ExitCode == 0
                ? null
                : $"Nginx config test failed through provider {DisplayName}. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
    }

    /// <inheritdoc />
    public async Task<ServiceLogfileReadResult> ReadLogfileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string logKey,
        int? sinceMinutes = null,
        int lines = DefaultLogLines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sshCommandService);
        ArgumentNullException.ThrowIfNull(profile);

        var paths = await DiscoverLogPathsAsync(sshCommandService, profile, cancellationToken);
        var normalizedLogKey = string.IsNullOrWhiteSpace(logKey) ? string.Empty : logKey.Trim();
        if (!paths.TryGetValue(normalizedLogKey, out var requestedPath))
        {
            return CreateLogError(
                normalizedLogKey,
                null,
                $"Unsupported Nginx logKey: {logKey}");
        }

        var validationError = ValidateProviderPaths(requestedPath, paths.Values.ToArray(), []);
        if (validationError is not null)
        {
            return CreateLogError(normalizedLogKey, requestedPath, validationError);
        }

        var safeLines = Math.Clamp(lines, 1, MaxLogLines);
        var safeSinceMinutes = sinceMinutes.HasValue
            ? Math.Clamp(sinceMinutes.Value, 0, MaxSinceMinutes)
            : 0;
        var result = await sshCommandService.ExecuteAsync(
            profile,
            NginxReadLogCommandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["pathBase64"] = EncodeArgument(requestedPath),
                ["allowedPathsBase64"] = EncodeLines(paths.Values),
                ["maxBytes"] = MaxLogReadBytes.ToString(CultureInfo.InvariantCulture),
                ["lines"] = safeLines.ToString(CultureInfo.InvariantCulture),
                ["sinceMinutes"] = safeSinceMinutes.ToString(CultureInfo.InvariantCulture),
            },
            channel: KelpieExecutionChannel.Mcp,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return CreateLogError(
                normalizedLogKey,
                requestedPath,
                $"Nginx log file read failed through provider {DisplayName}. ExitCode={result.ExitCode}. {CreateSafeErrorDetail(result.StandardError)}");
        }

        var warnings = CreateLogReadWarnings(result.StandardError);
        return new ServiceLogfileReadResult(
            ServiceKey,
            DisplayName,
            normalizedLogKey,
            requestedPath,
            result.StandardOutput,
            "utf-8",
            result.StandardError.Contains("KELPIE_TRUNCATED=1", StringComparison.Ordinal),
            warnings);
    }

    /// <summary>
    /// Extracts the Nginx main configuration path from nginx -V output.
    /// </summary>
    /// <param name="output">The nginx -V combined output.</param>
    /// <returns>The configured path, or <c>null</c> when unavailable.</returns>
    public static string? ExtractConfPath(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var match = ConfPathRegex().Match(output);
        return match.Success ? match.Groups["path"].Value : null;
    }

    /// <summary>
    /// Extracts include patterns from Nginx configuration text.
    /// </summary>
    /// <param name="configText">The Nginx configuration text.</param>
    /// <returns>The include patterns.</returns>
    public static IReadOnlyList<string> ExtractIncludePatterns(string configText)
    {
        if (string.IsNullOrWhiteSpace(configText))
        {
            return [];
        }

        return IncludeRegex()
            .Matches(configText)
            .Select(match => match.Groups["path"].Value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static ConfigFileAccess CreateConfigFileAccess(string path, ServiceConfigPathsResult paths)
    {
        var exactPaths = paths.ConfigFiles
            .Concat(paths.IncludePatterns.Where(pattern => !pattern.Contains('*', StringComparison.Ordinal)))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var allowedDirectories = paths.IncludePatterns
            .Where(pattern => pattern.Contains('*', StringComparison.Ordinal))
            .Select(GetDirectoryFromIncludePattern)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var validationError = ValidateProviderPaths(path, exactPaths, allowedDirectories);
        if (validationError is not null)
        {
            return new ConfigFileAccess([], [], validationError);
        }

        if (!exactPaths.Contains(path, StringComparer.Ordinal) && !IsPathAllowedByIncludePatterns(path, paths.IncludePatterns))
        {
            return new ConfigFileAccess([], [], "Requested path is not a provider-approved configuration file.");
        }

        return new ConfigFileAccess(exactPaths, allowedDirectories, Error: null);
    }

    private static string? ValidateProviderPaths(
        string path,
        IReadOnlyList<string> allowedPaths,
        IReadOnlyList<string> allowedDirectories)
    {
        if (!IsSafeAbsoluteUnixPath(path))
        {
            return "Requested path must be a safe absolute configuration path.";
        }

        if (allowedPaths.Count == 0 && allowedDirectories.Count == 0)
        {
            return "Nginx provider did not discover any readable configuration files.";
        }

        return allowedPaths.All(IsSafeAbsoluteUnixPath) && allowedDirectories.All(IsSafeAbsoluteUnixPath)
            ? null
            : "Nginx provider discovered an unsafe configuration path.";
    }

    private static bool IsSafeAbsoluteUnixPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Contains('\0', StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        return !path.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    }

    private static bool IsSafeAbsoluteUnixPathPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || !pattern.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (pattern.Contains('\0', StringComparison.Ordinal) || pattern.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        return !pattern.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal);
    }

    private static bool IsPathAllowedByIncludePatterns(string path, IReadOnlyList<string> includePatterns)
    {
        return includePatterns
            .Where(IsSafeAbsoluteUnixPathPattern)
            .Any(pattern => GlobToRegex(pattern).IsMatch(path));
    }

    private static string? GetDirectoryFromIncludePattern(string pattern)
    {
        if (!IsSafeAbsoluteUnixPathPattern(pattern))
        {
            return null;
        }

        var lastSlash = pattern.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return null;
        }

        var directory = pattern[..lastSlash];
        return directory.Contains('*', StringComparison.Ordinal)
            ? null
            : directory;
    }

    private static string? ResolveSiteConfigPath(string siteKey, ServiceConfigPathsResult paths)
    {
        var candidates = new List<string>();
        foreach (var pattern in paths.IncludePatterns
            .Where(IsSafeAbsoluteUnixPathPattern)
            .Where(IsSiteConfigIncludePattern)
            .OrderBy(GetSiteConfigIncludePatternPriority))
        {
            if (!pattern.Contains('*', StringComparison.Ordinal))
            {
                if (string.Equals(PathFileName(pattern), siteKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(PathFileNameWithoutConfExtension(pattern), siteKey, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(pattern);
                }

                continue;
            }

            var directory = GetDirectoryFromIncludePattern(pattern);
            if (directory is null)
            {
                continue;
            }

            if (pattern.EndsWith("*.conf", StringComparison.Ordinal))
            {
                candidates.Add($"{directory}/{siteKey}.conf");
            }

            candidates.Add($"{directory}/{siteKey}");
        }

        if (candidates.Count == 0
            && string.Equals(siteKey, "default", StringComparison.OrdinalIgnoreCase)
            && paths.MainConfig is not null)
        {
            candidates.Add(paths.MainConfig);
        }

        return candidates
            .Distinct(StringComparer.Ordinal)
            .FirstOrDefault(candidate => IsPathAllowedByIncludePatterns(candidate, paths.IncludePatterns)
                || paths.ConfigFiles.Contains(candidate, StringComparer.Ordinal));
    }

    private static bool IsSiteConfigIncludePattern(string pattern)
    {
        return pattern.Contains("/conf.d/", StringComparison.Ordinal)
            || pattern.Contains("/sites-enabled/", StringComparison.Ordinal)
            || pattern.Contains("/sites-available/", StringComparison.Ordinal);
    }

    private static int GetSiteConfigIncludePatternPriority(string pattern)
    {
        if (pattern.Contains("/conf.d/", StringComparison.Ordinal))
        {
            return 0;
        }

        if (pattern.Contains("/sites-enabled/", StringComparison.Ordinal))
        {
            return 1;
        }

        if (pattern.Contains("/sites-available/", StringComparison.Ordinal))
        {
            return 2;
        }

        return 3;
    }

    private static string PathFileName(string path)
    {
        var slashIndex = path.LastIndexOf('/');
        return slashIndex < 0 ? path : path[(slashIndex + 1)..];
    }

    private static string PathFileNameWithoutConfExtension(string path)
    {
        var fileName = PathFileName(path);
        return fileName.EndsWith(".conf", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^5]
            : fileName;
    }

    private static bool TryApplyPhpTemplate(
        string content,
        string socketPath,
        string extension,
        out string updatedContent,
        out bool changed,
        out string error)
    {
        updatedContent = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        changed = false;
        error = string.Empty;

        var serverStart = ServerBlockStartRegex().Match(updatedContent);
        if (!serverStart.Success)
        {
            error = "Nginx site configuration does not contain a server block.";
            return false;
        }

        var openBrace = updatedContent.IndexOf('{', serverStart.Index);
        var closeBrace = FindMatchingBrace(updatedContent, openBrace);
        if (openBrace < 0 || closeBrace < 0)
        {
            error = "Nginx server block could not be parsed.";
            return false;
        }

        var serverBlock = updatedContent.Substring(openBrace + 1, closeBrace - openBrace - 1);
        var updatedBlock = EnsureIndexPhp(serverBlock, out var indexChanged);
        updatedBlock = EnsureDefaultServerListen(updatedBlock, out var defaultServerChanged);
        updatedBlock = EnsurePhpLocation(updatedBlock, socketPath, extension, out var locationChanged);
        changed = indexChanged || defaultServerChanged || locationChanged;
        if (!changed)
        {
            return true;
        }

        updatedContent = updatedContent[..(openBrace + 1)] + updatedBlock + updatedContent[closeBrace..];
        return true;
    }

    private static string CreateDefaultPhpSiteConfig()
    {
        return """
            server {
                listen 80 default_server;
                server_name _;
                root /var/www/html;

                location / {
                    try_files $uri $uri/ =404;
                }
            }

            """;
    }

    private static bool IsMissingConfigFileError(string standardError)
    {
        return standardError.Contains("not a regular file", StringComparison.OrdinalIgnoreCase)
            || standardError.Contains("No such file", StringComparison.OrdinalIgnoreCase)
            || standardError.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureIndexPhp(string serverBlock, out bool changed)
    {
        var match = IndexDirectiveRegex().Match(serverBlock);
        if (!match.Success)
        {
            changed = true;
            return "\n    index index.php index.html index.htm;" + serverBlock;
        }

        var directive = match.Value;
        if (Regex.IsMatch(directive, @"(?i)(^|\s)index\.php(\s|;)", RegexOptions.CultureInvariant))
        {
            changed = false;
            return serverBlock;
        }

        changed = true;
        var replacement = Regex.Replace(
            directive,
            @"(?i)^\s*index\s+",
            match.Value[..(match.Value.Length - match.Value.TrimStart().Length)] + "index index.php ",
            RegexOptions.CultureInvariant);
        return serverBlock[..match.Index] + replacement + serverBlock[(match.Index + match.Length)..];
    }

    private static string EnsureDefaultServerListen(string serverBlock, out bool changed)
    {
        if (Regex.IsMatch(
            serverBlock,
            @"(?im)^[ \t]*listen\s+[^;]*(?<!\d)80(?!\d)[^;]*\bdefault_server\b[^;]*;",
            RegexOptions.CultureInvariant))
        {
            changed = false;
            return serverBlock;
        }

        var match = Listen80DirectiveRegex().Match(serverBlock);
        if (!match.Success)
        {
            changed = true;
            return "\n    listen 80 default_server;" + serverBlock;
        }

        changed = true;
        var replacement = match.Value[..^1].TrimEnd() + " default_server;";
        return serverBlock[..match.Index] + replacement + serverBlock[(match.Index + match.Length)..];
    }

    private static string EnsurePhpLocation(string serverBlock, string socketPath, string extension, out bool changed)
    {
        var escapedExtension = Regex.Escape(extension);
        if (Regex.IsMatch(
            serverBlock,
            @"location\s+~\s+\\" + escapedExtension + @"\$\s*\{[^{}]*fastcgi_pass\s+unix:" + Regex.Escape(socketPath) + @"\s*;",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            changed = false;
            return serverBlock;
        }

        changed = true;
        var block = "\n"
            + $"    location ~ \\{extension}$ {{\n"
            + "        include snippets/fastcgi-php.conf;\n"
            + $"        fastcgi_pass unix:{socketPath};\n"
            + "    }\n";
        return serverBlock.TrimEnd('\n') + block + "\n";
    }

    private static int FindMatchingBrace(string text, int openBrace)
    {
        if (openBrace < 0 || openBrace >= text.Length || text[openBrace] != '{')
        {
            return -1;
        }

        var depth = 0;
        for (var index = openBrace; index < text.Length; index++)
        {
            if (text[index] == '{')
            {
                depth++;
            }
            else if (text[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static bool IsSafeSiteKey(string siteKey)
    {
        return SafeSiteKeyRegex().IsMatch(siteKey);
    }

    private static bool IsSafePhpSocketPath(string socketPath)
    {
        return SafePhpSocketPathRegex().IsMatch(socketPath)
            && IsSafeAbsoluteUnixPath(socketPath)
            && !socketPath.Contains("//", StringComparison.Ordinal);
    }

    private static bool IsSafePhpExtension(string extension)
    {
        return SafePhpExtensionRegex().IsMatch(extension);
    }

    private static bool IsSafeSitesEnabledPath(string path)
    {
        if (!IsSafeAbsoluteUnixPath(path))
        {
            return false;
        }

        if (!path.StartsWith("/etc/nginx/sites-enabled/", StringComparison.Ordinal))
        {
            return false;
        }

        var name = PathFileName(path);
        return SafeSiteKeyRegex().IsMatch(name) && !name.EndsWith(".kelpiedisabled", StringComparison.Ordinal);
    }

    private static string EncodeArgument(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private static IReadOnlyList<string> SplitOutputLines(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return [];
        }

        return output
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string EncodeLines(IEnumerable<string> values)
    {
        var text = string.Join('\n', values.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrEmpty(text))
        {
            text = "\n";
        }

        return EncodeArgument(text);
    }

    private ServiceConfigFileReadResult CreateError(
        string? path,
        string error,
        IReadOnlyList<string> warnings)
    {
        return new ServiceConfigFileReadResult(
            ServiceKey,
            DisplayName,
            path,
            Content: string.Empty,
            Encoding: "utf-8",
            Truncated: false,
            warnings,
            Error: error);
    }

    private ServiceConfigFileWriteResult CreateWriteError(
        string? path,
        string error,
        IReadOnlyList<string> warnings)
    {
        return new ServiceConfigFileWriteResult(
            ServiceKey,
            DisplayName,
            path,
            Encoding: "utf-8",
            BytesWritten: 0,
            warnings,
            Error: error);
    }

    private ServiceConfigFileAccessCheckResult CreateAccessCheckResult(
        string? path,
        bool canRead,
        bool canWrite,
        string? reason,
        IReadOnlyList<string> warnings,
        string? method = null,
        string? targetKey = null,
        string? confirmation = null)
    {
        return new ServiceConfigFileAccessCheckResult(
            ServiceKey,
            DisplayName,
            path,
            canRead,
            canWrite,
            RequiresConfirmation: canWrite,
            Confirmation: canWrite ? confirmation : null,
            Method: method,
            TargetKey: targetKey,
            Encoding: "utf-8",
            warnings,
            reason,
            Error: null);
    }

    private string CreateWriteConfirmation(string path, string targetKey, string method)
    {
        return $"service_config_file_write:{ServiceKey}:{path}:{method}:{targetKey}";
    }

    private ServiceConfigFileBackupActionResult CreateBackupActionError(
        string? path,
        string backupPath,
        string error,
        IReadOnlyList<string> warnings)
    {
        return new ServiceConfigFileBackupActionResult(
            ServiceKey,
            DisplayName,
            path,
            backupPath,
            Changed: false,
            warnings,
            Error: error);
    }

    private NginxPhpEnableResult CreatePhpEnableError(
        string siteKey,
        string? path,
        string socketPath,
        string extension,
        string error,
        IReadOnlyList<string> warnings)
    {
        return new NginxPhpEnableResult(
            ServiceKey,
            DisplayName,
            siteKey,
            path,
            socketPath,
            extension,
            Changed: false,
            Tested: false,
            RolledBack: false,
            Committed: false,
            BytesWritten: 0,
            warnings,
            error);
    }

    private static string GetBackupPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path + ".kelpiebakup";
    }

    private static Task<IReadOnlyDictionary<string, string>> DiscoverLogPathsAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        CancellationToken cancellationToken)
    {
        _ = sshCommandService;
        _ = profile;
        _ = cancellationToken;
        return Task.FromResult(LogPaths);
    }

    private ServiceLogfileReadResult CreateLogError(
        string logKey,
        string? path,
        string error)
    {
        return new ServiceLogfileReadResult(
            ServiceKey,
            DisplayName,
            logKey,
            path,
            Content: string.Empty,
            Encoding: "utf-8",
            Truncated: false,
            Warnings: [],
            Error: error);
    }

    private static IReadOnlyList<string> CreateReadWarnings(string content, string standardError)
    {
        var warnings = new List<string>();
        if (standardError.Contains("KELPIE_TRUNCATED=1", StringComparison.Ordinal))
        {
            warnings.Add("Content was truncated by the maximum read size.");
        }

        if (SensitiveValueRegex().IsMatch(content))
        {
            warnings.Add("Content may contain sensitive values. Masking is not implemented yet.");
        }

        return warnings;
    }

    private static IReadOnlyList<string> CreateLogReadWarnings(string standardError)
    {
        var warnings = new List<string>();
        if (standardError.Contains("KELPIE_TRUNCATED=1", StringComparison.Ordinal))
        {
            warnings.Add("Content was truncated by the maximum read size.");
        }

        if (standardError.Contains("KELPIE_SINCE_FILTER_PARTIAL=1", StringComparison.Ordinal))
        {
            warnings.Add("Some log lines did not have a recognized timestamp and were excluded by sinceMinutes.");
        }

        return warnings;
    }

    private static string CreateSafeErrorDetail(string standardError)
    {
        var firstLine = standardError
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(firstLine) ? "No error detail was returned." : firstLine;
    }

    [GeneratedRegex(@"--conf-path=(?<path>\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex ConfPathRegex();

    [GeneratedRegex(@"^\s*include\s+(?<path>[^;#]+)\s*;", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex IncludeRegex();

    [GeneratedRegex(@"(?i)\b(password|secret|token|api[_-]?key|private[_-]?key)\b\s*[:=]", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveValueRegex();

    [GeneratedRegex(@"\bserver\s*\{", RegexOptions.CultureInvariant)]
    private static partial Regex ServerBlockStartRegex();

    [GeneratedRegex(@"(?im)^[ \t]*index\s+[^;]+;")]
    private static partial Regex IndexDirectiveRegex();

    [GeneratedRegex(@"(?im)^[ \t]*listen\s+[^;]*(?<!\d)80(?!\d)[^;]*;")]
    private static partial Regex Listen80DirectiveRegex();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeSiteKeyRegex();

    [GeneratedRegex(@"^/(run|var/run)/[A-Za-z0-9._/-]+\.sock$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePhpSocketPathRegex();

    [GeneratedRegex(@"^\.[A-Za-z0-9]{1,16}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafePhpExtensionRegex();

    private static Regex GlobToRegex(string pattern)
    {
        var regexText = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", @"[^/]*", StringComparison.Ordinal) + "$";
        return new Regex(regexText, RegexOptions.CultureInvariant);
    }

    private sealed record ConfigFileAccess(
        IReadOnlyList<string> ExactPaths,
        IReadOnlyList<string> AllowedDirectories,
        string? Error);
}
