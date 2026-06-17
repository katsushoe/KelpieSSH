namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a selectable SSH login user for a profile.
/// </summary>
public sealed class SshConnectionUser
{
    /// <summary>
    /// Gets the SSH user name.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the SSH authentication method.
    /// </summary>
    public string AuthenticationMethod { get; init; } = "privateKey";

    /// <summary>
    /// Gets the private key file path.
    /// </summary>
    public string? PrivateKeyPath { get; init; }

    /// <summary>
    /// Gets the optional private key passphrase.
    /// </summary>
    public string? PrivateKeyPassphrase { get; init; }

    /// <summary>
    /// Gets the password secret name.
    /// </summary>
    public string? PasswordSecretName { get; init; }

    /// <summary>
    /// Gets the policy mode.
    /// </summary>
    public KelpiePolicyMode Mode { get; init; } = KelpiePolicyMode.Safe;

    /// <summary>
    /// Gets the enabled capability flags.
    /// </summary>
    public PolicySet Capabilities { get; init; } = PolicySet.Empty;

    /// <summary>
    /// Gets the high-level role names.
    /// </summary>
    public IReadOnlyCollection<string> Roles { get; init; } = [];

    /// <summary>
    /// Gets the allowed root rules.
    /// </summary>
    public IReadOnlyCollection<AllowedRootRule> AllowedRootRules { get; init; } = [];

    /// <summary>
    /// Gets the special path rules.
    /// </summary>
    public IReadOnlyCollection<SpecialPathRule> SpecialPaths { get; init; } = [];

    /// <summary>
    /// Gets the per-environment-variable rules.
    /// </summary>
    public IReadOnlyCollection<EnvironmentValueRule> EnvironmentValues { get; init; } = [];
}
