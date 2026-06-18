using System.ComponentModel;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    /// <summary>
    /// Returns configuration file paths for a supported service on a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="serviceKey">The supported service key.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The discovered service configuration paths.</returns>
    [McpServerTool(Name = "service_config_paths")]
    [Description("Returns configuration file paths for a supported service on a configured SSH profile.")]
    public static async Task<ServiceConfigPathsResult> GetServiceConfigPathsAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string serviceKey,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: service_config_paths serviceKey={serviceKey}, profile={profileName}");
        if (!serviceConfigPathsProviders.TryGet(serviceKey, out var provider))
        {
            return new ServiceConfigPathsResult(
                serviceKey,
                string.Empty,
                MainConfig: null,
                ConfigFiles: [],
                IncludePatterns: [],
                Warnings: [],
                Error: $"Unsupported serviceKey: {serviceKey}");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await provider.GetConfigPathsAsync(
            sshCommandService,
            profile,
            cancellationToken);
    }

    /// <summary>
    /// Reads a configuration file for a supported service on a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="serviceKey">The supported service key.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="path">The optional full path. When omitted, the provider reads the main configuration file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The service configuration file content.</returns>
    [McpServerTool(Name = "service_config_file_read")]
    [Description("Reads one provider-approved configuration file for a supported service on a configured SSH profile.")]
    public static async Task<ServiceConfigFileReadResult> ReadServiceConfigFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string serviceKey,
        string profileName,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: service_config_file_read serviceKey={serviceKey}, profile={profileName}");
        if (!serviceConfigPathsProviders.TryGet(serviceKey, out var provider))
        {
            return new ServiceConfigFileReadResult(
                serviceKey,
                string.Empty,
                path,
                Content: string.Empty,
                Encoding: "utf-8",
                Truncated: false,
                Warnings: [],
                Error: $"Unsupported serviceKey: {serviceKey}");
        }

        if (provider is not IServiceConfigFileReader reader)
        {
            return new ServiceConfigFileReadResult(
                serviceKey,
                provider.DisplayName,
                path,
                Content: string.Empty,
                Encoding: "utf-8",
                Truncated: false,
                Warnings: [],
                Error: $"Service config file read is not supported for serviceKey: {serviceKey}");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await reader.ReadConfigFileAsync(
            sshCommandService,
            profile,
            path,
            cancellationToken);
    }

    /// <summary>
    /// Checks whether a configuration file can be read for a supported service on a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="serviceKey">The supported service key.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="path">The optional full path. When omitted, the provider checks the main configuration file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The service configuration file read access check result.</returns>
    [McpServerTool(Name = "service_config_file_check_read")]
    [Description("Checks whether one provider-approved configuration file can be read without returning its content.")]
    public static async Task<ServiceConfigFileAccessCheckResult> CanReadServiceConfigFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string serviceKey,
        string profileName,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: service_config_file_check_read serviceKey={serviceKey}, profile={profileName}");
        if (!serviceConfigPathsProviders.TryGet(serviceKey, out var provider))
        {
            return CreateAccessCheckToolError(serviceKey, string.Empty, path, $"Unsupported serviceKey: {serviceKey}");
        }

        if (provider is not IServiceConfigFileAccessChecker checker)
        {
            return CreateAccessCheckToolError(serviceKey, provider.DisplayName, path, $"Service config file access check is not supported for serviceKey: {serviceKey}");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await checker.CanReadConfigFileAsync(
            sshCommandService,
            profile,
            path,
            cancellationToken);
    }

    /// <summary>
    /// Applies a provider-limited edit to a provider-approved configuration file for a supported service on a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="serviceKey">The supported service key.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="path">The full provider-approved configuration file path.</param>
    /// <param name="targetKey">The provider-specific target key to edit.</param>
    /// <param name="method">The edit method.</param>
    /// <param name="confirmation">The required confirmation token: service_config_file_write:&lt;serviceKey&gt;:&lt;path&gt;:&lt;method&gt;:&lt;targetKey&gt;.</param>
    /// <param name="targetValue">The value or line to write. Delete does not require a value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The service configuration file write result.</returns>
    [McpServerTool(Name = "service_config_file_write")]
    [Description("Applies one provider-limited configuration edit after explicit confirmation.")]
    public static async Task<ServiceConfigFileWriteResult> WriteServiceConfigFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string serviceKey,
        string profileName,
        string path,
        string targetKey,
        string method,
        string confirmation,
        string? targetValue = null,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: service_config_file_write serviceKey={serviceKey}, profile={profileName}, path={path}, method={method}, targetKey={targetKey}");
        if (!TryGetConfirmationError("service_config_file_write", $"{serviceKey}:{path}:{method}:{targetKey}", confirmation, out var confirmationError))
        {
            return new ServiceConfigFileWriteResult(
                serviceKey,
                string.Empty,
                path,
                Encoding: "utf-8",
                BytesWritten: 0,
                Warnings: [],
                Error: confirmationError);
        }

        if (!serviceConfigPathsProviders.TryGet(serviceKey, out var provider))
        {
            return new ServiceConfigFileWriteResult(
                serviceKey,
                string.Empty,
                path,
                Encoding: "utf-8",
                BytesWritten: 0,
                Warnings: [],
                Error: $"Unsupported serviceKey: {serviceKey}");
        }

        if (provider is not IServiceConfigFileWriter writer)
        {
            return new ServiceConfigFileWriteResult(
                serviceKey,
                provider.DisplayName,
                path,
                Encoding: "utf-8",
                BytesWritten: 0,
                Warnings: [],
                Error: $"Service config file write is not supported for serviceKey: {serviceKey}");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await writer.WriteConfigFileAsync(
            sshCommandService,
            profile,
            path,
            targetKey,
            method,
            targetValue,
            cancellationToken);
    }

    /// <summary>
    /// Checks whether a provider-limited edit could be written to a provider-approved configuration file without applying it.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="serviceKey">The supported service key.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="path">The full provider-approved configuration file path.</param>
    /// <param name="targetKey">The provider-specific target key to edit.</param>
    /// <param name="method">The edit method.</param>
    /// <param name="targetValue">The value or line to write. Delete does not require a value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The service configuration file write access check result.</returns>
    [McpServerTool(Name = "service_config_file_check_write")]
    [Description("Checks whether one provider-limited configuration edit can be written without applying changes.")]
    public static async Task<ServiceConfigFileAccessCheckResult> CanWriteServiceConfigFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string serviceKey,
        string profileName,
        string path,
        string targetKey,
        string method,
        string? targetValue = null,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: service_config_file_check_write serviceKey={serviceKey}, profile={profileName}, path={path}, method={method}, targetKey={targetKey}");
        if (!serviceConfigPathsProviders.TryGet(serviceKey, out var provider))
        {
            return CreateAccessCheckToolError(serviceKey, string.Empty, path, $"Unsupported serviceKey: {serviceKey}", method, targetKey);
        }

        if (provider is not IServiceConfigFileAccessChecker checker)
        {
            return CreateAccessCheckToolError(serviceKey, provider.DisplayName, path, $"Service config file access check is not supported for serviceKey: {serviceKey}", method, targetKey);
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await checker.CanWriteConfigFileAsync(
            sshCommandService,
            profile,
            path,
            targetKey,
            method,
            targetValue,
            cancellationToken);
    }

    /// <summary>
    /// Restores a provider-approved configuration file from its Kelpie backup for a supported service.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="serviceKey">The supported service key.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="path">The full provider-approved configuration file path.</param>
    /// <param name="confirmation">The required confirmation token: service_config_file_rollback:&lt;serviceKey&gt;:&lt;path&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The service configuration file rollback result.</returns>
    [McpServerTool(Name = "service_config_file_rollback")]
    [Description("Restores one provider-approved configuration file from its Kelpie backup after explicit confirmation.")]
    public static async Task<ServiceConfigFileBackupActionResult> RollbackServiceConfigFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string serviceKey,
        string profileName,
        string path,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: service_config_file_rollback serviceKey={serviceKey}, profile={profileName}, path={path}");
        if (!TryGetConfirmationError("service_config_file_rollback", $"{serviceKey}:{path}", confirmation, out var confirmationError))
        {
            return CreateBackupActionToolError(serviceKey, string.Empty, path, confirmationError);
        }

        if (!serviceConfigPathsProviders.TryGet(serviceKey, out var provider))
        {
            return CreateBackupActionToolError(serviceKey, string.Empty, path, $"Unsupported serviceKey: {serviceKey}");
        }

        if (provider is not IServiceConfigFileBackupManager backupManager)
        {
            return CreateBackupActionToolError(serviceKey, provider.DisplayName, path, $"Service config file backup management is not supported for serviceKey: {serviceKey}");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await backupManager.RollbackConfigFileAsync(
            sshCommandService,
            profile,
            path,
            cancellationToken);
    }

    /// <summary>
    /// Commits a provider-approved configuration file edit by removing its Kelpie backup for a supported service.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="serviceKey">The supported service key.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="path">The full provider-approved configuration file path.</param>
    /// <param name="confirmation">The required confirmation token: service_config_file_commit:&lt;serviceKey&gt;:&lt;path&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The service configuration file commit result.</returns>
    [McpServerTool(Name = "service_config_file_commit")]
    [Description("Commits one provider-approved configuration file edit by removing its Kelpie backup after explicit confirmation.")]
    public static async Task<ServiceConfigFileBackupActionResult> CommitServiceConfigFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string serviceKey,
        string profileName,
        string path,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: service_config_file_commit serviceKey={serviceKey}, profile={profileName}, path={path}");
        if (!TryGetConfirmationError("service_config_file_commit", $"{serviceKey}:{path}", confirmation, out var confirmationError))
        {
            return CreateBackupActionToolError(serviceKey, string.Empty, path, confirmationError);
        }

        if (!serviceConfigPathsProviders.TryGet(serviceKey, out var provider))
        {
            return CreateBackupActionToolError(serviceKey, string.Empty, path, $"Unsupported serviceKey: {serviceKey}");
        }

        if (provider is not IServiceConfigFileBackupManager backupManager)
        {
            return CreateBackupActionToolError(serviceKey, provider.DisplayName, path, $"Service config file backup management is not supported for serviceKey: {serviceKey}");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await backupManager.CommitConfigFileAsync(
            sshCommandService,
            profile,
            path,
            cancellationToken);
    }

    /// <summary>
    /// Tests configuration files for a supported service on a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="serviceKey">The supported service key.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="confirmation">The required confirmation token: service_config_test:&lt;serviceKey&gt;.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The service configuration file test result.</returns>
    [McpServerTool(Name = "service_config_test")]
    [Description("Tests provider-managed configuration files for a supported service after explicit confirmation.")]
    public static async Task<ServiceConfigFileTestResult> TestServiceConfigFileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string serviceKey,
        string profileName,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: service_config_test serviceKey={serviceKey}, profile={profileName}");
        if (!TryGetConfirmationError("service_config_test", serviceKey, confirmation, out var confirmationError))
        {
            return new ServiceConfigFileTestResult(
                serviceKey,
                string.Empty,
                TestCommand: string.Empty,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: confirmationError,
                Stdout: [],
                Stderr: [confirmationError],
                Warnings: [],
                Error: confirmationError);
        }

        if (!serviceConfigPathsProviders.TryGet(serviceKey, out var provider))
        {
            return new ServiceConfigFileTestResult(
                serviceKey,
                string.Empty,
                TestCommand: string.Empty,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: $"Unsupported serviceKey: {serviceKey}",
                Stdout: [],
                Stderr: [$"Unsupported serviceKey: {serviceKey}"],
                Warnings: [],
                Error: $"Unsupported serviceKey: {serviceKey}");
        }

        if (provider is not IServiceConfigFileTester tester)
        {
            return new ServiceConfigFileTestResult(
                serviceKey,
                provider.DisplayName,
                TestCommand: string.Empty,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: $"Service config file test is not supported for serviceKey: {serviceKey}",
                Stdout: [],
                Stderr: [$"Service config file test is not supported for serviceKey: {serviceKey}"],
                Warnings: [],
                Error: $"Service config file test is not supported for serviceKey: {serviceKey}");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await tester.TestConfigFileAsync(
            sshCommandService,
            profile,
            cancellationToken);
    }

    /// <summary>
    /// Enables fixed-template PHP-FPM routing in one provider-approved Nginx site configuration.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="socketPath">The PHP-FPM Unix socket path.</param>
    /// <param name="confirmation">The required confirmation token: ssh_service_config_nginx_enable_php:&lt;siteKey&gt;:&lt;socketPath&gt;:&lt;extension&gt;.</param>
    /// <param name="siteKey">The provider-resolved site key. Defaults to default.</param>
    /// <param name="extension">The executable extension to route. Defaults to .php.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The PHP routing configuration result.</returns>
    [McpServerTool(Name = "ssh_service_config_nginx_enable_php")]
    [Description("Enables fixed-template Nginx PHP-FPM routing for one provider-approved site after explicit confirmation.")]
    public static async Task<NginxPhpEnableResult> EnableNginxPhpAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string profileName,
        string socketPath,
        string confirmation,
        string siteKey = "default",
        string extension = ".php",
        CancellationToken cancellationToken = default)
    {
        var normalizedSiteKey = string.IsNullOrWhiteSpace(siteKey) ? "default" : siteKey.Trim();
        var normalizedExtension = string.IsNullOrWhiteSpace(extension) ? ".php" : extension.Trim();
        var normalizedSocketPath = string.IsNullOrWhiteSpace(socketPath) ? string.Empty : socketPath.Trim();
        KpLog.Info($"MCP SSH tool called: ssh_service_config_nginx_enable_php siteKey={normalizedSiteKey}, extension={normalizedExtension}, profile={profileName}");
        if (!TryGetConfirmationError("ssh_service_config_nginx_enable_php", $"{normalizedSiteKey}:{normalizedSocketPath}:{normalizedExtension}", confirmation, out var confirmationError))
        {
            return new NginxPhpEnableResult(
                "nginx",
                string.Empty,
                normalizedSiteKey,
                Path: null,
                normalizedSocketPath,
                normalizedExtension,
                Changed: false,
                Tested: false,
                RolledBack: false,
                Committed: false,
                BytesWritten: 0,
                Warnings: [],
                Error: confirmationError);
        }

        if (!serviceConfigPathsProviders.TryGet("nginx", out var provider))
        {
            return new NginxPhpEnableResult(
                "nginx",
                string.Empty,
                normalizedSiteKey,
                Path: null,
                normalizedSocketPath,
                normalizedExtension,
                Changed: false,
                Tested: false,
                RolledBack: false,
                Committed: false,
                BytesWritten: 0,
                Warnings: [],
                Error: "Unsupported serviceKey: nginx");
        }

        if (provider is not INginxPhpConfigurator configurator)
        {
            return new NginxPhpEnableResult(
                "nginx",
                provider.DisplayName,
                normalizedSiteKey,
                Path: null,
                normalizedSocketPath,
                normalizedExtension,
                Changed: false,
                Tested: false,
                RolledBack: false,
                Committed: false,
                BytesWritten: 0,
                Warnings: [],
                Error: "Nginx PHP-FPM configuration is not supported by the registered provider.");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await configurator.EnablePhpAsync(
            sshCommandService,
            profile,
            normalizedSiteKey,
            normalizedSocketPath,
            normalizedExtension,
            cancellationToken);
    }

    /// <summary>
    /// Reads a provider-approved log file for a supported service on a configured SSH profile.
    /// </summary>
    /// <param name="sshCommandService">The SSH command service.</param>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="serviceConfigPathsProviders">The service configuration paths providers.</param>
    /// <param name="serviceKey">The supported service key.</param>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="logKey">The provider-defined log key.</param>
    /// <param name="sinceMinutes">The optional recent window in minutes.</param>
    /// <param name="lines">The maximum number of log lines to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The service log file content.</returns>
    [McpServerTool(Name = "service_logfile_read")]
    [Description("Reads one provider-approved log file for a supported service on a configured SSH profile.")]
    public static async Task<ServiceLogfileReadResult> ReadServiceLogfileAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        ServiceConfigPathsProviderCatalog serviceConfigPathsProviders,
        string serviceKey,
        string profileName,
        string logKey,
        int? sinceMinutes = null,
        int lines = 500,
        CancellationToken cancellationToken = default)
    {
        KpLog.Info($"MCP SSH tool called: service_logfile_read serviceKey={serviceKey}, logKey={logKey}, profile={profileName}");
        if (!serviceConfigPathsProviders.TryGet(serviceKey, out var provider))
        {
            return new ServiceLogfileReadResult(
                serviceKey,
                string.Empty,
                logKey,
                Path: null,
                Content: string.Empty,
                Encoding: "utf-8",
                Truncated: false,
                Warnings: [],
                Error: $"Unsupported serviceKey: {serviceKey}");
        }

        if (provider is not IServiceLogfileReader reader)
        {
            return new ServiceLogfileReadResult(
                serviceKey,
                provider.DisplayName,
                logKey,
                Path: null,
                Content: string.Empty,
                Encoding: "utf-8",
                Truncated: false,
                Warnings: [],
                Error: $"Service logfile read is not supported for serviceKey: {serviceKey}");
        }

        var profile = ResolveSshProfile(profileCatalog, profileName);
        return await reader.ReadLogfileAsync(
            sshCommandService,
            profile,
            logKey,
            sinceMinutes,
            lines,
            cancellationToken);
    }

    private static ServiceConfigFileBackupActionResult CreateBackupActionToolError(
        string serviceKey,
        string displayName,
        string path,
        string error)
    {
        return new ServiceConfigFileBackupActionResult(
            serviceKey,
            displayName,
            path,
            string.IsNullOrWhiteSpace(path) ? string.Empty : path + ".kelpiebakup",
            Changed: false,
            Warnings: [],
            Error: error);
    }

    private static ServiceConfigFileAccessCheckResult CreateAccessCheckToolError(
        string serviceKey,
        string displayName,
        string? path,
        string error,
        string? method = null,
        string? targetKey = null)
    {
        return new ServiceConfigFileAccessCheckResult(
            serviceKey,
            displayName,
            path,
            CanRead: false,
            CanWrite: false,
            RequiresConfirmation: false,
            Confirmation: null,
            Method: method,
            TargetKey: targetKey,
            Encoding: "utf-8",
            Warnings: [],
            Reason: error,
            Error: error);
    }
}
