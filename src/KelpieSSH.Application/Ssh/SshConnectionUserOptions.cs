using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents SSH login settings for one user in a profile.
/// </summary>
public sealed class SshConnectionUserOptions
{
    /// <summary>
    /// Gets or sets the SSH user name.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authentication method.
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the private key file name under the Kelpie keys directory.
    /// </summary>
    public string PrivateKeyFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the private key file path for private key authentication.
    /// </summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional private key passphrase.
    /// </summary>
    public string? PrivateKeyPassphrase { get; set; }

    /// <summary>
    /// Gets or sets the password secret name.
    /// </summary>
    public string? PasswordSecretName { get; set; }

    /// <summary>
    /// Gets or sets the policy mode for this user.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets role names for this user.
    /// </summary>
    public JsonElement Roles { get; set; }

    /// <summary>
    /// Gets or sets the enabled capability flags for this user.
    /// </summary>
    public JsonElement Capabilities { get; set; }

    /// <summary>
    /// Gets or sets the allowed roots for this user.
    /// </summary>
    public JsonElement AllowedRoots { get; set; }

    /// <summary>
    /// Gets or sets the special paths for this user.
    /// </summary>
    public JsonElement SpecialPaths { get; set; }

    /// <summary>
    /// Gets or sets per-environment-variable rules for this user.
    /// </summary>
    public JsonElement EnvironmentValues { get; set; }
}
