namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents the result of enabling fixed-template PHP-FPM routing in Nginx.
/// </summary>
public sealed record NginxPhpEnableResult(
    string ServiceKey,
    string DisplayName,
    string SiteKey,
    string? Path,
    string SocketPath,
    string Extension,
    bool Changed,
    bool Tested,
    bool RolledBack,
    bool Committed,
    int BytesWritten,
    IReadOnlyList<string> Warnings,
    string? Error = null);
