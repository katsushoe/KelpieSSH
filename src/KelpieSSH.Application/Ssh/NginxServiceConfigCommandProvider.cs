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
    private const string ReadLogScriptBase64 = "aW1wb3J0IGJhc2U2NCwgZGF0ZXRpbWUsIG9zLCByZSwgc3lzCnAgPSBiYXNlNjQuYjY0ZGVjb2RlKHN5cy5hcmd2WzFdKS5kZWNvZGUoJ3V0Zi04JykKYWxsb3dlZCA9IGJhc2U2NC5iNjRkZWNvZGUoc3lzLmFyZ3ZbMl0pLmRlY29kZSgndXRmLTgnKS5zcGxpdGxpbmVzKCkKbWF4YiA9IGludChzeXMuYXJndlszXSkKbGluZV9saW1pdCA9IGludChzeXMuYXJndls0XSkKc2luY2UgPSBpbnQoc3lzLmFyZ3ZbNV0pCnJwID0gb3MucGF0aC5yZWFscGF0aChwKQphbGxvd2VkX3JlYWwgPSBsaXN0KG1hcChvcy5wYXRoLnJlYWxwYXRoLCBbeCBmb3IgeCBpbiBhbGxvd2VkIGlmIHhdKSkKaWYgcnAgbm90IGluIGFsbG93ZWRfcmVhbDoKICAgIHN5cy5leGl0KCdFUlJPUjogcGF0aCBpcyBub3QgYW4gYWxsb3dlZCBhcHAgbG9nIGZpbGUnKQppZiBub3Qgb3MucGF0aC5pc2ZpbGUocnApOgogICAgc3lzLmV4aXQoJ0VSUk9SOiBsb2cgcGF0aCBpcyBub3QgYSByZWd1bGFyIGZpbGUnKQpzaXplID0gb3MucGF0aC5nZXRzaXplKHJwKQp3aXRoIG9wZW4ocnAsICdyYicpIGFzIGY6CiAgICBmLnNlZWsobWF4KDAsIHNpemUgLSAobWF4YiArIDEpKSkKICAgIGRhdGEgPSBmLnJlYWQobWF4YiArIDEpCmlmIGInXHgwMCcuZGVjb2RlKCd1bmljb2RlX2VzY2FwZScpLmVuY29kZSgnbGF0aW4xJykgaW4gZGF0YToKICAgIHN5cy5leGl0KCdFUlJPUjogYmluYXJ5IGxvZyBmaWxlIGlzIG5vdCBhbGxvd2VkJykKdGV4dCA9IGRhdGFbLW1heGI6XS5kZWNvZGUoJ3V0Zi04JywgJ3JlcGxhY2UnKQppZiBzaXplID4gbWF4YjoKICAgIHN5cy5zdGRlcnIud3JpdGUoJ0tFTFBJRV9UUlVOQ0FURUQ9MVxuJykKcm93cyA9IHRleHQuc3BsaXRsaW5lcygpWy1saW5lX2xpbWl0Ol0KY3V0b2ZmID0gZGF0ZXRpbWUuZGF0ZXRpbWUubm93KGRhdGV0aW1lLnRpbWV6b25lLnV0YykgLSBkYXRldGltZS50aW1lZGVsdGEobWludXRlcz1zaW5jZSkKbW9udGhfbmFtZXMgPSBbJ0phbicsICdGZWInLCAnTWFyJywgJ0FwcicsICdNYXknLCAnSnVuJywgJ0p1bCcsICdBdWcnLCAnU2VwJywgJ09jdCcsICdOb3YnLCAnRGVjJ10KYmFkID0gMApvdXQgPSBbXQpmb3Igcm93IGluIHJvd3M6CiAgICBrZWVwID0gVHJ1ZQogICAgaWYgc2luY2UgPiAwOgogICAgICAgIGtlZXAgPSBGYWxzZQogICAgICAgIG0gPSByZS5zZWFyY2gocidcWyhbMC05XVswLTldKS8oW0EtWmEtel1bQS1aYS16XVtBLVphLXpdKS8oWzAtOV1bMC05XVswLTldWzAtOV0pOihbMC05XVswLTldKTooWzAtOV1bMC05XSk6KFswLTldWzAtOV0pIChbKy1dWzAtOV1bMC05XVswLTldWzAtOV0pXF0nLCByb3cpCiAgICAgICAgaWYgbToKICAgICAgICAgICAgdHogPSBtLmdyb3VwKDcpCiAgICAgICAgICAgIG9mZiA9IGRhdGV0aW1lLnRpbWVkZWx0YShob3Vycz1pbnQodHpbMTozXSksIG1pbnV0ZXM9aW50KHR6WzM6NV0pKQogICAgICAgICAgICBpZiB0elswXSA9PSAnLSc6CiAgICAgICAgICAgICAgICBvZmYgPSAtb2ZmCiAgICAgICAgICAgIG1vbnRoID0gbW9udGhfbmFtZXMuaW5kZXgobS5ncm91cCgyKSkgKyAxIGlmIG0uZ3JvdXAoMikgaW4gbW9udGhfbmFtZXMgZWxzZSAxCiAgICAgICAgICAgIGR0ID0gZGF0ZXRpbWUuZGF0ZXRpbWUoaW50KG0uZ3JvdXAoMykpLCBtb250aCwgaW50KG0uZ3JvdXAoMSkpLCBpbnQobS5ncm91cCg0KSksIGludChtLmdyb3VwKDUpKSwgaW50KG0uZ3JvdXAoNikpLCB0emluZm89ZGF0ZXRpbWUudGltZXpvbmUob2ZmKSkKICAgICAgICAgICAga2VlcCA9IGR0LmFzdGltZXpvbmUoZGF0ZXRpbWUudGltZXpvbmUudXRjKSA+PSBjdXRvZmYKICAgICAgICBlbHNlOgogICAgICAgICAgICBtID0gcmUubWF0Y2gocicoWzAtOV1bMC05XVswLTldWzAtOV0pLyhbMC05XVswLTldKS8oWzAtOV1bMC05XSkgKFswLTldWzAtOV0pOihbMC05XVswLTldKTooWzAtOV1bMC05XSknLCByb3cpCiAgICAgICAgICAgIGlmIG06CiAgICAgICAgICAgICAgICBkdCA9IGRhdGV0aW1lLmRhdGV0aW1lKGludChtLmdyb3VwKDEpKSwgaW50KG0uZ3JvdXAoMikpLCBpbnQobS5ncm91cCgzKSksIGludChtLmdyb3VwKDQpKSwgaW50KG0uZ3JvdXAoNSkpLCBpbnQobS5ncm91cCg2KSkpLmFzdGltZXpvbmUoKQogICAgICAgICAgICAgICAga2VlcCA9IGR0LmFzdGltZXpvbmUoZGF0ZXRpbWUudGltZXpvbmUudXRjKSA+PSBjdXRvZmYKICAgICAgICAgICAgZWxzZToKICAgICAgICAgICAgICAgIGJhZCArPSAxCiAgICBpZiBrZWVwOgogICAgICAgIG91dC5hcHBlbmQocm93KQppZiBzaW5jZSA+IDAgYW5kIGJhZCA+IDA6CiAgICBzeXMuc3RkZXJyLndyaXRlKCdLRUxQSUVfU0lOQ0VfRklMVEVSX1BBUlRJQUw9MVxuJykKc3lzLnN0ZG91dC53cml0ZSgnXG4nLmpvaW4ob3V0KSArICgnXG4nIGlmIG91dCBlbHNlICcnKSk=";

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
            CreateEncodedPythonStdinCommand(ReadLogScriptBase64, "{pathBase64} {allowedPathsBase64} {maxBytes} {lines} {sinceMinutes}"),
            TimeSpan.FromSeconds(10),
            [
                new AllowedCommandParameterDefinition("pathBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("allowedPathsBase64", MaxLength: 4096, Pattern: Base64PathPattern),
                new AllowedCommandParameterDefinition("maxBytes", MaxLength: 6, Pattern: MaxBytesPattern),
                new AllowedCommandParameterDefinition("lines", MaxLength: 4, Pattern: LinesPattern),
                new AllowedCommandParameterDefinition("sinceMinutes", MaxLength: 4, Pattern: SinceMinutesPattern),
            ]),
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

    private static string CreateEncodedPythonStdinCommand(string encodedScript, string arguments)
    {
        var argumentSuffix = string.IsNullOrWhiteSpace(arguments) ? string.Empty : " " + arguments;
        var command = $"sh -c \"printf %s '{encodedScript}' | base64 -d | python3 -{argumentSuffix}\"";
        return command;
    }

}
