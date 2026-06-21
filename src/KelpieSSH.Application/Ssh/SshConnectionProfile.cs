namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one saved SSH profile in the KelpieSSH library.
/// </summary>
public sealed class SshConnectionProfile
{
    /// <summary>
    /// Gets the profile name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the SSH host name or address.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Gets the SSH port.
    /// </summary>
    public int Port { get; init; } = 22;

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
    /// Gets the secret reference name for password authentication.
    /// </summary>
    public string? PasswordSecretName { get; init; }

    /// <summary>
    /// Gets the SSH connection timeout.
    /// </summary>
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the target OS family.
    /// </summary>
    public required string OsFamily { get; init; }

    /// <summary>
    /// Gets the target package manager.
    /// </summary>
    public string PackageManager { get; init; } = string.Empty;

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
    /// Gets the allowed root path or glob patterns.
    /// </summary>
    public IReadOnlyCollection<string> AllowedRoots { get; init; } = [];

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

    /// <summary>
    /// Gets the provider-approved web public sites.
    /// </summary>
    public IReadOnlyCollection<WebPublicSite> WebPublicSites { get; init; } = [];

    /// <summary>
    /// Gets service-specific settings.
    /// </summary>
    public SshConnectionServices Services { get; init; } = new();

    /// <summary>
    /// Gets the selectable users for this profile.
    /// </summary>
    public IReadOnlyCollection<SshConnectionUser> Users { get; init; } = [];

    /// <summary>
    /// Creates a copy of this profile with the specified login user selected.
    /// </summary>
    /// <param name="userName">The login user name.</param>
    /// <returns>The profile with the selected user.</returns>
    public SshConnectionProfile WithUser(string userName)
    {
        var user = Users.FirstOrDefault(candidate =>
            string.Equals(candidate.UserName, userName, StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            throw new InvalidOperationException($"SSH profile user was not found: {userName}");
        }

        return new SshConnectionProfile
        {
            Name = Name,
            Host = Host,
            Port = Port,
            UserName = user.UserName,
            AuthenticationMethod = user.AuthenticationMethod,
            PrivateKeyPath = user.PrivateKeyPath,
            PrivateKeyPassphrase = user.PrivateKeyPassphrase,
            PasswordSecretName = user.PasswordSecretName,
            ConnectionTimeout = ConnectionTimeout,
            OsFamily = OsFamily,
            PackageManager = PackageManager,
            Mode = user.Mode,
            Capabilities = user.Capabilities,
            Roles = user.Roles,
            AllowedRoots = user.AllowedRootRules.Select(rule => rule.Path).ToArray(),
            AllowedRootRules = user.AllowedRootRules,
            SpecialPaths = user.SpecialPaths,
            EnvironmentValues = user.EnvironmentValues,
            WebPublicSites = user.WebPublicSites,
            Services = Services,
            Users = Users,
        };
    }

    /// <summary>
    /// Validates the profile.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("SSH profile name is required.");
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("SSH host is required.");
        }

        if (Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("SSH port is invalid.");
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            throw new InvalidOperationException("SSH user name is required.");
        }

        if (string.IsNullOrWhiteSpace(AuthenticationMethod))
        {
            throw new InvalidOperationException("SSH authentication method is required.");
        }

        if (string.Equals(AuthenticationMethod, "privateKey", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            throw new InvalidOperationException("SSH private key path is required.");
        }

        if (string.Equals(AuthenticationMethod, "password", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(PasswordSecretName))
        {
            throw new InvalidOperationException("SSH password secret name is required.");
        }

        if (!string.Equals(AuthenticationMethod, "privateKey", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(AuthenticationMethod, "password", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SSH authentication method is not supported: {AuthenticationMethod}");
        }

        if (ConnectionTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("SSH connection timeout must be positive.");
        }

        if (string.IsNullOrWhiteSpace(OsFamily))
        {
            throw new InvalidOperationException("SSH OS family is required.");
        }

        if (string.IsNullOrWhiteSpace(PackageManager))
        {
            throw new InvalidOperationException("SSH package manager is required.");
        }

        foreach (var allowedRoot in AllowedRoots)
        {
            if (string.IsNullOrWhiteSpace(allowedRoot))
            {
                throw new InvalidOperationException("SSH allowed root must not be empty.");
            }
        }

        foreach (var allowedRoot in AllowedRootRules)
        {
            if (string.IsNullOrWhiteSpace(allowedRoot.Path))
            {
                throw new InvalidOperationException("SSH allowed root must not be empty.");
            }
        }

        foreach (var specialPath in SpecialPaths)
        {
            if (string.IsNullOrWhiteSpace(specialPath.Pattern))
            {
                throw new InvalidOperationException("SSH special path must not be empty.");
            }
        }

        var environmentKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var environmentValue in EnvironmentValues)
        {
            if (string.IsNullOrWhiteSpace(environmentValue.Key))
            {
                throw new InvalidOperationException("SSH environment value key must not be empty.");
            }

            if (!environmentKeys.Add(environmentValue.Key))
            {
                throw new InvalidOperationException($"SSH environment value key is duplicated: {environmentValue.Key}");
            }
        }

        foreach (var site in WebPublicSites)
        {
            if (string.IsNullOrWhiteSpace(site.SiteKey))
            {
                throw new InvalidOperationException("SSH web public site key must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(site.RootPath))
            {
                throw new InvalidOperationException("SSH web public site root must not be empty.");
            }

            foreach (var allowedFile in site.AllowedFiles)
            {
                if (string.IsNullOrWhiteSpace(allowedFile.Pattern))
                {
                    throw new InvalidOperationException("SSH web public allowed file pattern must not be empty.");
                }
            }
        }

        if (Services.Nginx?.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("SSH Nginx service port is invalid.");
        }

        var userNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in Users)
        {
            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                throw new InvalidOperationException("SSH user name is required.");
            }

            if (!userNames.Add(user.UserName))
            {
                throw new InvalidOperationException($"SSH profile user is duplicated: {user.UserName}");
            }
        }
    }
}
