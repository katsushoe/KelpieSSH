namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents SSH authentication configuration values for one profile.
/// </summary>
public sealed class SshConnectionAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the SSH user name.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SSH user name compatibility alias.
    /// </summary>
    public string UsrName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the authentication method.
    /// </summary>
    public string Method { get; set; } = "privateKey";

    /// <summary>
    /// Gets or sets the private key file name under the Kelpie keys directory.
    /// </summary>
    public string PrivateKeyFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the private key file path for private key authentication. This is retained for compatibility.
    /// </summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional private key passphrase.
    /// </summary>
    public string? PrivateKeyPassphrase { get; set; }

    /// <summary>
    /// Gets or sets the secret reference name for password authentication.
    /// </summary>
    public string? PasswordSecretName { get; set; }
}
