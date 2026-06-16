namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a validated SSH command preview that has not been executed.
/// </summary>
public sealed record SshCommandPreview(
    string CommandName,
    string CommandText,
    SshCommandRiskLevel RiskLevel,
    bool RequiresConfirmation,
    IReadOnlyDictionary<string, string> Arguments);
