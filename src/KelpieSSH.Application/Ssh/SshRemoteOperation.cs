using System.Text.Json.Serialization;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one SSH operation request independent from any saved profile.
/// </summary>
/// <param name="Endpoint">The SSH endpoint.</param>
/// <param name="Credential">The SSH credential reference.</param>
/// <param name="Policy">The execution policy.</param>
/// <param name="Operation">The requested operation.</param>
/// <param name="Options">The optional execution options.</param>
/// <param name="Target">The optional target platform metadata.</param>
public sealed record SshRemoteOperation(
    [property: JsonPropertyName("endpoint")] SshRemoteEndpoint Endpoint,
    [property: JsonPropertyName("credential")] SshRemoteCredential Credential,
    [property: JsonPropertyName("policy")] SshRemotePolicy Policy,
    [property: JsonPropertyName("operation")] SshRemoteOperationSpec Operation,
    [property: JsonPropertyName("options")] SshRemoteOperationOptions? Options = null,
    [property: JsonPropertyName("target")] SshRemoteTarget? Target = null)
{
    /// <summary>
    /// Creates an operation request from a host-side saved profile.
    /// </summary>
    /// <param name="profile">The saved SSH profile.</param>
    /// <param name="operationKind">The operation kind.</param>
    /// <param name="operationName">The operation name.</param>
    /// <param name="arguments">The operation arguments.</param>
    /// <param name="timeout">The optional operation timeout.</param>
    /// <param name="correlationId">The optional correlation id.</param>
    /// <returns>The remote operation request.</returns>
    public static SshRemoteOperation FromProfile(
        SshConnectionProfile profile,
        string operationKind,
        string operationName,
        IReadOnlyDictionary<string, string>? arguments = null,
        TimeSpan? timeout = null,
        string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new SshRemoteOperation(
            new SshRemoteEndpoint(profile.Host, profile.Port),
            new SshRemoteCredential(
                profile.UserName,
                profile.AuthenticationMethod,
                SecretName: profile.PasswordSecretName,
                PrivateKeyPath: profile.PrivateKeyPath,
                PrivateKeyPassphrase: profile.PrivateKeyPassphrase),
            new SshRemotePolicy(
                profile.Mode.ToString(),
                profile.Roles,
                profile.AllowedRootRules.Select(rule => new SshRemoteAllowedRoot(
                    rule.Path,
                    FormatAllowedRootAccess(rule.Access))).ToArray(),
                profile.SpecialPaths.Select(rule => new SshRemoteSpecialPath(
                    rule.Pattern,
                    rule.Action.ToString())).ToArray(),
                profile.Capabilities.List()),
            new SshRemoteOperationSpec(operationKind, operationName, arguments),
            new SshRemoteOperationOptions(
                TimeoutSeconds: timeout is null ? null : (int)Math.Ceiling(timeout.Value.TotalSeconds),
                CorrelationId: correlationId),
            new SshRemoteTarget(
                profile.OsFamily,
                profile.PackageManager,
                profile.WebPublicSites,
                profile.Services));
    }

    /// <summary>
    /// Converts this operation request to the current SSH runner input shape.
    /// </summary>
    /// <returns>The connection settings for this operation.</returns>
    public SshConnectionProfile ToConnectionProfile()
    {
        Validate();

        return new SshConnectionProfile
        {
            Name = string.IsNullOrWhiteSpace(Options?.CorrelationId) ? "remote-operation" : Options.CorrelationId,
            Host = Endpoint.Host,
            Port = Endpoint.Port,
            UserName = Credential.UserName,
            AuthenticationMethod = NormalizeCredentialKind(Credential.Kind),
            PrivateKeyPath = Credential.PrivateKeyPath ?? GetPrivateKeySecretFallback(),
            PrivateKeyPassphrase = Credential.PrivateKeyPassphrase,
            PasswordSecretName = Credential.SecretName,
            OsFamily = string.IsNullOrWhiteSpace(Target?.OsFamily) ? "linux" : Target.OsFamily,
            PackageManager = string.IsNullOrWhiteSpace(Target?.PackageManager) ? "none" : Target.PackageManager,
            Mode = ParsePolicyMode(Policy.Mode),
            Capabilities = PolicySet.FromNames(Policy.Capabilities),
            Roles = Policy.Roles,
            AllowedRoots = Policy.AllowedRoots.Select(root => root.Path).ToArray(),
            AllowedRootRules = Policy.AllowedRoots.Select(root => new AllowedRootRule(
                root.Path,
                ParseAllowedRootAccess(root.Access))).ToArray(),
            SpecialPaths = Policy.SpecialPaths.Select(rule => new SpecialPathRule(
                rule.Pattern,
                ParseSpecialPathAction(rule.Action))).ToArray(),
            WebPublicSites = Target?.WebPublicSites ?? [],
            Services = Target?.Services ?? new SshConnectionServices(),
        };
    }

    /// <summary>
    /// Validates this operation request.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint.Host))
        {
            throw new InvalidOperationException("SSH remote operation endpoint host is required.");
        }

        if (Endpoint.Port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("SSH remote operation endpoint port is invalid.");
        }

        if (string.IsNullOrWhiteSpace(Credential.UserName))
        {
            throw new InvalidOperationException("SSH remote operation credential user_name is required.");
        }

        if (string.IsNullOrWhiteSpace(Credential.Kind))
        {
            throw new InvalidOperationException("SSH remote operation credential kind is required.");
        }

        if (string.IsNullOrWhiteSpace(Operation.Kind))
        {
            throw new InvalidOperationException("SSH remote operation kind is required.");
        }

        if (string.IsNullOrWhiteSpace(Operation.Name))
        {
            throw new InvalidOperationException("SSH remote operation name is required.");
        }

        if (Options?.TimeoutSeconds is <= 0)
        {
            throw new InvalidOperationException("SSH remote operation timeout_seconds must be positive.");
        }

        foreach (var allowedRoot in Policy.AllowedRoots)
        {
            if (string.IsNullOrWhiteSpace(allowedRoot.Path))
            {
                throw new InvalidOperationException("SSH remote operation allowed root path is required.");
            }
        }

        foreach (var specialPath in Policy.SpecialPaths)
        {
            if (string.IsNullOrWhiteSpace(specialPath.Pattern))
            {
                throw new InvalidOperationException("SSH remote operation special path pattern is required.");
            }
        }
    }

    private string? GetPrivateKeySecretFallback()
    {
        if (!string.Equals(NormalizeCredentialKind(Credential.Kind), "privateKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Credential.SecretName;
    }

    private static string NormalizeCredentialKind(string kind)
    {
        return kind.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "privatekey" => "privateKey",
            "password" => "password",
            _ => kind,
        };
    }

    private static KelpiePolicyMode ParsePolicyMode(string mode)
    {
        if (Enum.TryParse<KelpiePolicyMode>(mode, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"SSH remote operation policy mode is not supported: {mode}");
    }

    private static SpecialPathAction ParseSpecialPathAction(string action)
    {
        if (Enum.TryParse<SpecialPathAction>(action, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"SSH remote operation special path action is not supported: {action}");
    }

    private static AllowedRootAccess ParseAllowedRootAccess(IReadOnlyCollection<string> access)
    {
        if (access.Count == 0)
        {
            throw new InvalidOperationException("SSH remote operation allowed root access is required.");
        }

        var expression = string.Join(
            "|",
            access.Select(part => part.StartsWith('$') || part.StartsWith('@') ? part : "@" + part));
        return AllowedRootAccessText.Parse(expression);
    }

    private static IReadOnlyCollection<string> FormatAllowedRootAccess(AllowedRootAccess access)
    {
        return AllowedRootAccessText.Format(access)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.TrimStart('@').ToLowerInvariant())
            .ToArray();
    }
}

/// <summary>
/// Represents the SSH endpoint for one operation.
/// </summary>
/// <param name="Host">The SSH host name or address.</param>
/// <param name="Port">The SSH port.</param>
public sealed record SshRemoteEndpoint(
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("port")] int Port = 22);

/// <summary>
/// Represents an SSH credential reference for one operation.
/// </summary>
/// <param name="UserName">The SSH user name.</param>
/// <param name="Kind">The credential kind.</param>
/// <param name="SecretName">The secret reference name.</param>
/// <param name="PrivateKeyPath">The private key file path when file-based key auth is used.</param>
/// <param name="PrivateKeyPassphrase">The optional private key passphrase.</param>
public sealed record SshRemoteCredential(
    [property: JsonPropertyName("user_name")] string UserName,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("secret_name")] string? SecretName = null,
    [property: JsonPropertyName("private_key_path")] string? PrivateKeyPath = null,
    [property: JsonPropertyName("private_key_passphrase")] string? PrivateKeyPassphrase = null);

/// <summary>
/// Represents policy values for one operation.
/// </summary>
/// <param name="Mode">The policy mode.</param>
/// <param name="Roles">The high-level role names.</param>
/// <param name="AllowedRoots">The allowed root rules.</param>
/// <param name="SpecialPaths">The special path rules.</param>
/// <param name="Capabilities">The capability flags.</param>
public sealed class SshRemotePolicy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SshRemotePolicy"/> class.
    /// </summary>
    public SshRemotePolicy(
        string mode,
        IReadOnlyCollection<string>? roles = null,
        IReadOnlyCollection<SshRemoteAllowedRoot>? allowedRoots = null,
        IReadOnlyCollection<SshRemoteSpecialPath>? specialPaths = null,
        IReadOnlyCollection<string>? capabilities = null)
    {
        Mode = mode;
        Roles = roles ?? [];
        AllowedRoots = allowedRoots ?? [];
        SpecialPaths = specialPaths ?? [];
        Capabilities = capabilities ?? [];
    }

    /// <summary>
    /// Gets the policy mode.
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; init; }

    /// <summary>
    /// Gets the high-level role names.
    /// </summary>
    [JsonPropertyName("roles")]
    public IReadOnlyCollection<string> Roles { get; init; } = [];

    /// <summary>
    /// Gets the allowed root rules.
    /// </summary>
    [JsonPropertyName("allowed_roots")]
    public IReadOnlyCollection<SshRemoteAllowedRoot> AllowedRoots { get; init; } = [];

    /// <summary>
    /// Gets the special path rules.
    /// </summary>
    [JsonPropertyName("special_paths")]
    public IReadOnlyCollection<SshRemoteSpecialPath> SpecialPaths { get; init; } = [];

    /// <summary>
    /// Gets the capability flags.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public IReadOnlyCollection<string> Capabilities { get; init; } = [];
}

/// <summary>
/// Represents one allowed root rule in a remote operation.
/// </summary>
/// <param name="Path">The root path or glob pattern.</param>
/// <param name="Access">The access flags.</param>
public sealed record SshRemoteAllowedRoot(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("access")] IReadOnlyCollection<string> Access);

/// <summary>
/// Represents one special path rule in a remote operation.
/// </summary>
/// <param name="Pattern">The path or glob pattern.</param>
/// <param name="Action">The special path action.</param>
public sealed record SshRemoteSpecialPath(
    [property: JsonPropertyName("pattern")] string Pattern,
    [property: JsonPropertyName("action")] string Action);

/// <summary>
/// Represents the operation to execute over SSH.
/// </summary>
/// <param name="Kind">The operation kind, such as managed or raw.</param>
/// <param name="Name">The operation name.</param>
/// <param name="Arguments">The operation arguments.</param>
public sealed class SshRemoteOperationSpec
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SshRemoteOperationSpec"/> class.
    /// </summary>
    public SshRemoteOperationSpec(
        string kind,
        string name,
        IReadOnlyDictionary<string, string>? arguments = null)
    {
        Kind = kind;
        Name = name;
        Arguments = arguments ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the operation kind.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; }

    /// <summary>
    /// Gets the operation name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>
    /// Gets the operation arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public IReadOnlyDictionary<string, string> Arguments { get; init; }
}

/// <summary>
/// Represents optional execution options for one operation.
/// </summary>
/// <param name="TimeoutSeconds">The command execution timeout in seconds.</param>
/// <param name="CorrelationId">The caller-supplied correlation id.</param>
public sealed record SshRemoteOperationOptions(
    [property: JsonPropertyName("timeout_seconds")] int? TimeoutSeconds = null,
    [property: JsonPropertyName("correlation_id")] string? CorrelationId = null);

/// <summary>
/// Represents optional target metadata needed by host-side command adapters.
/// </summary>
/// <param name="OsFamily">The target OS family.</param>
/// <param name="PackageManager">The target package manager.</param>
/// <param name="WebPublicSites">The provider-approved web public sites.</param>
/// <param name="Services">The service-specific settings.</param>
public sealed class SshRemoteTarget
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SshRemoteTarget"/> class.
    /// </summary>
    public SshRemoteTarget(
        string? osFamily = null,
        string? packageManager = null,
        IReadOnlyCollection<WebPublicSite>? webPublicSites = null,
        SshConnectionServices? services = null)
    {
        OsFamily = osFamily;
        PackageManager = packageManager;
        WebPublicSites = webPublicSites ?? [];
        Services = services;
    }

    /// <summary>
    /// Gets the target OS family.
    /// </summary>
    [JsonPropertyName("os_family")]
    public string? OsFamily { get; init; }

    /// <summary>
    /// Gets the target package manager.
    /// </summary>
    [JsonPropertyName("package_manager")]
    public string? PackageManager { get; init; }

    /// <summary>
    /// Gets the provider-approved web public sites.
    /// </summary>
    [JsonPropertyName("web_public_sites")]
    public IReadOnlyCollection<WebPublicSite> WebPublicSites { get; init; }

    /// <summary>
    /// Gets service-specific settings.
    /// </summary>
    [JsonPropertyName("services")]
    public SshConnectionServices? Services { get; init; }
}
