namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides Nginx application configuration commands.
/// </summary>
public sealed class NginxServiceConfigCommandProvider : IAllowedCommandProvider
{
    private const string Base64PathPattern = "^[A-Za-z0-9+/=]{1,4096}$";
    private const string MaxBytesPattern = "^[1-9][0-9]{0,5}$";
    private const string LinesPattern = "^[1-9][0-9]{0,3}$";
    private const string SinceMinutesPattern = "^[0-9]{1,4}$";
    private const string Base64ContentPattern = "^[A-Za-z0-9+/=]+$";

    private static readonly AllowedCommandDefinition[] Commands =
    [
        new("service_config_nginx_version", "nginx -V", TimeSpan.FromSeconds(10)),
        new(
            "service_config_nginx_test_config",
            "sudo -n nginx -t",
            TimeSpan.FromSeconds(30),
            RiskLevel: SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_config_nginx_read_config",
            NginxConfigReadWire.CommandTemplate(),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedDirsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 6, Pattern: MaxBytesPattern),
            ]) { ExecutionLimits = new SshCommandExecutionLimits(1333340, 2048, 0), IgnoreEnvironmentOverrides = true },
        new(
            "service_config_nginx_helper_plan",
            "sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper nginx-plan 1",
            TimeSpan.FromSeconds(30)),
        new(
            "service_config_nginx_helper_check",
            "sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper nginx-check 1",
            TimeSpan.FromSeconds(30)),
        new(
            "service_config_nginx_helper_apply",
            "sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper nginx-apply 1",
            TimeSpan.FromSeconds(30),
            RiskLevel: SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_config_nginx_helper_status",
            "sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper nginx-status 1",
            TimeSpan.FromSeconds(30)),
        new(
            "service_config_nginx_helper_commit",
            "sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper nginx-commit 1",
            TimeSpan.FromSeconds(30),
            RiskLevel: SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_config_nginx_helper_rollback",
            "sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper nginx-rollback 1",
            TimeSpan.FromSeconds(30),
            RiskLevel: SshCommandRiskLevel.ConfirmRequired),
        new(
            "service_logfile_nginx_read",
            NginxLogWire.CommandTemplate(),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 6, Pattern: MaxBytesPattern),
                new AllowedCommandParameterDefinition("lines", MaxLength: 4, Pattern: LinesPattern),
                new AllowedCommandParameterDefinition("sinceMinutes", MaxLength: 4, Pattern: SinceMinutesPattern),
            ]) { ExecutionLimits = new SshCommandExecutionLimits(600000, 2048, 65536) },
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<string> OsFamilies { get; } = ["*"];

    /// <inheritdoc />
    public bool Supports(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return !string.IsNullOrWhiteSpace(profile.OsFamily);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AllowedCommandDefinition> GetCommands(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Commands;
    }


}
