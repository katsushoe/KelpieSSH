using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents configuration values for one SSH connection profile.
/// </summary>
public sealed class SshConnectionProfileOptions
{
    /// <summary>
    /// Gets or sets the profile name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SSH host endpoint settings.
    /// </summary>
    public SshConnectionHostOptions Host { get; set; } = new();

    /// <summary>
    /// Gets or sets the SSH authentication settings.
    /// </summary>
    public SshConnectionAuthenticationOptions Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the short SSH authentication settings alias.
    /// </summary>
    public SshConnectionAuthenticationOptions Auth { get; set; } = new();

    /// <summary>
    /// Gets or sets the legacy SSH endpoint settings.
    /// </summary>
    public SshConnectionSshOptions Ssh { get; set; } = new();

    /// <summary>
    /// Gets or sets the SSH connection behavior settings.
    /// </summary>
    public SshConnectionConnectionOptions Connection { get; set; } = new();

    /// <summary>
    /// Gets or sets the target platform settings.
    /// </summary>
    public SshConnectionPlatformOptions Platform { get; set; } = new();

    /// <summary>
    /// Gets or sets the legacy policy role expression.
    /// </summary>
    public string Mode { get; set; } = "Safe";

    /// <summary>
    /// Gets or sets the high-level profile role names.
    /// </summary>
    public JsonElement Roles { get; set; }

    /// <summary>
    /// Gets or sets the SSH execution policy settings.
    /// </summary>
    public SshConnectionPolicyOptions Policy { get; set; } = new();

    /// <summary>
    /// Gets or sets the enabled capability flags. This may be a string, an array, or an object with a Flags property.
    /// </summary>
    public JsonElement Capabilities { get; set; }

    /// <summary>
    /// Gets or sets named AllowedRoots access presets.
    /// </summary>
    public JsonElement Rights { get; set; }

    /// <summary>
    /// Gets or sets the allowed root path or glob patterns.
    /// </summary>
    public JsonElement AllowedRoots { get; set; }

    /// <summary>
    /// Gets or sets special path rules.
    /// </summary>
    public JsonElement SpecialPaths { get; set; }

    /// <summary>
    /// Gets or sets per-environment-variable rules.
    /// </summary>
    public JsonElement EnvironmentValues { get; set; }

    /// <summary>
    /// Gets or sets the default user name when Users is configured.
    /// </summary>
    public string DefaultUser { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets selectable login users.
    /// </summary>
    public JsonElement Users { get; set; }

    /// <summary>
    /// Gets or sets provider-approved web public sites.
    /// </summary>
    public JsonElement WebPublicSites { get; set; }

    /// <summary>
    /// Gets or sets service-specific settings.
    /// </summary>
    public SshConnectionServicesOptions Services { get; set; } = new();

    /// <summary>
    /// Creates a validated connection profile.
    /// </summary>
    /// <param name="baseDirectory">The base directory used to resolve relative private key paths.</param>
    /// <returns>The SSH connection profile.</returns>
    public SshConnectionProfile ToProfile(string baseDirectory)
    {
        var ssh = Ssh ?? throw new InvalidOperationException("SSH endpoint settings are required.");
        var host = Host ?? throw new InvalidOperationException("SSH host settings are required.");
        var authentication = ResolveAuthentication(Authentication, Auth, ssh.Authentication);
        var connection = Connection ?? throw new InvalidOperationException("SSH connection settings are required.");
        var platform = Platform ?? throw new InvalidOperationException("SSH platform settings are required.");
        var policy = Policy ?? throw new InvalidOperationException("SSH policy settings are required.");
        var privateKeyPath = ResolvePrivateKeyPath(baseDirectory, authentication);
        var hostAddress = ResolveHostAddress(host, ssh);
        var userName = ResolveUserName(authentication, ssh);
        var capabilities = ResolveCapabilities(Capabilities, policy);
        var rights = ReadRights(Rights);
        var allowedRootRules = ResolveAllowedRootRules(Capabilities, policy, AllowedRoots, rights);
        var specialPaths = ReadSpecialPaths(SpecialPaths);
        var environmentValues = ReadEnvironmentValues(EnvironmentValues);
        var webPublicSites = ReadWebPublicSites(WebPublicSites, rights);
        var services = ReadServices(Services);
        var profileRoles = ResolveRoles(Mode, Roles, defaultRoles: [KelpieRoleNames.Safe]);
        var profileMode = ResolveModeFromRoles(profileRoles);
        allowedRootRules = ApplyRolesToAllowedRoots(allowedRootRules, profileRoles, webPublicSites, services);
        var users = ResolveUsers(
            Users,
            baseDirectory,
            profileRoles,
            profileMode,
            capabilities,
            allowedRootRules,
            specialPaths,
            environmentValues,
            rights,
            webPublicSites,
            services,
            authentication,
            userName);
        var selectedUser = ResolveSelectedUser(
            users,
            userName,
            string.IsNullOrWhiteSpace(DefaultUser) ? userName : DefaultUser);

        var profile = new SshConnectionProfile
        {
            Name = Name,
            Host = hostAddress,
            Port = IsHostConfigured(host) ? host.Port : ssh.Port,
            UserName = selectedUser.UserName,
            AuthenticationMethod = selectedUser.AuthenticationMethod,
            PrivateKeyPath = selectedUser.PrivateKeyPath ?? privateKeyPath,
            PrivateKeyPassphrase = selectedUser.PrivateKeyPassphrase,
            PasswordSecretName = selectedUser.PasswordSecretName,
            ConnectionTimeout = TimeSpan.FromSeconds(connection.TimeoutSeconds),
            OsFamily = platform.OsFamily,
            PackageManager = PackageManagerResolver.Resolve(platform.OsFamily, platform.PackageManager),
            Mode = selectedUser.Mode,
            Capabilities = selectedUser.Capabilities,
            AllowedRoots = selectedUser.AllowedRootRules.Select(rule => rule.Path).ToArray(),
            AllowedRootRules = selectedUser.AllowedRootRules,
            SpecialPaths = selectedUser.SpecialPaths,
            EnvironmentValues = selectedUser.EnvironmentValues,
            WebPublicSites = selectedUser.WebPublicSites,
            Services = services,
            Roles = selectedUser.Roles,
            Users = users,
        };

        profile.Validate();
        return profile;
    }

    private static SshConnectionAuthenticationOptions ResolveAuthentication(
        SshConnectionAuthenticationOptions rootAuthentication,
        SshConnectionAuthenticationOptions shortAuthentication,
        SshConnectionAuthenticationOptions legacyAuthentication)
    {
        if (IsAuthenticationConfigured(rootAuthentication))
        {
            return rootAuthentication;
        }

        if (IsAuthenticationConfigured(shortAuthentication))
        {
            return shortAuthentication;
        }

        return legacyAuthentication ?? throw new InvalidOperationException("SSH authentication settings are required.");
    }

    private static string ResolveHostAddress(SshConnectionHostOptions host, SshConnectionSshOptions legacySsh)
    {
        return IsHostConfigured(host)
            ? host.Address
            : legacySsh.Host;
    }

    private static string ResolveUserName(
        SshConnectionAuthenticationOptions authentication,
        SshConnectionSshOptions legacySsh)
    {
        if (!string.IsNullOrWhiteSpace(authentication.UserName))
        {
            return authentication.UserName;
        }

        if (!string.IsNullOrWhiteSpace(authentication.UsrName))
        {
            return authentication.UsrName;
        }

        return legacySsh.UserName;
    }

    private static bool IsHostConfigured(SshConnectionHostOptions host)
    {
        return !string.IsNullOrWhiteSpace(host.Address);
    }

    private static bool IsAuthenticationConfigured(SshConnectionAuthenticationOptions authentication)
    {
        return !string.IsNullOrWhiteSpace(authentication.UserName)
            || !string.IsNullOrWhiteSpace(authentication.UsrName)
            || !string.Equals(authentication.Method, "privateKey", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(authentication.PrivateKeyFile)
            || !string.IsNullOrWhiteSpace(authentication.PrivateKeyPath)
            || !string.IsNullOrWhiteSpace(authentication.PrivateKeyPassphrase)
            || !string.IsNullOrWhiteSpace(authentication.PasswordSecretName);
    }

    private static PolicySet ResolveCapabilities(JsonElement capabilitiesElement, SshConnectionPolicyOptions legacyPolicy)
    {
        return capabilitiesElement.ValueKind == JsonValueKind.Undefined
            ? PolicySet.FromNames(SplitLegacyPolicyLevel(legacyPolicy.Level))
            : PolicySet.FromJson(capabilitiesElement);
    }

    private static IReadOnlyCollection<AllowedRootRule> ResolveAllowedRootRules(
        JsonElement capabilitiesElement,
        SshConnectionPolicyOptions legacyPolicy,
        JsonElement rootAllowedRoots,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (capabilitiesElement.ValueKind == JsonValueKind.Object
            && capabilitiesElement.TryGetProperty("AllowedRoots", out var allowedRootsElement))
        {
            return ReadAllowedRoots(allowedRootsElement, rights);
        }

        var rootRules = ReadAllowedRoots(rootAllowedRoots, rights);
        if (rootRules.Count > 0)
        {
            return rootRules;
        }

        return legacyPolicy.AllowedRoots
            .Select(root => new AllowedRootRule(root, AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.CD))
            .ToArray();
    }

    private static IReadOnlyCollection<AllowedRootRule> ReadAllowedRoots(
        JsonElement allowedRootsElement,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (allowedRootsElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (allowedRootsElement.ValueKind == JsonValueKind.Object)
        {
            return allowedRootsElement
                .EnumerateObject()
                .Select(item => new AllowedRootRule(item.Name, ReadAllowedRootAccess(item.Value, rights)))
                .ToArray();
        }

        if (allowedRootsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("SSH allowed roots must be an object or an array.");
        }

        return allowedRootsElement
            .EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? new AllowedRootRule(item.GetString() ?? string.Empty, AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.CD)
                : throw new InvalidOperationException("SSH allowed root items must be strings."))
            .ToArray();
    }

    private static AllowedRootAccess ReadAllowedRootAccess(
        JsonElement value,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("SSH allowed root access must be a string.");
        }

        var accessName = value.GetString() ?? string.Empty;
        return rights.TryGetValue(accessName, out var access)
            ? access
            : AllowedRootAccessText.Parse(accessName, rights);
    }

    private static IReadOnlyDictionary<string, AllowedRootAccess> ReadRights(JsonElement rightsElement)
    {
        if (rightsElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return AllowedRootAccessText.CreateSystemRights();
        }

        if (rightsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("SSH rights must be an object.");
        }

        var definitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var systemRights = AllowedRootAccessText.CreateSystemRights();
        foreach (var item in rightsElement.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("SSH rights values must be strings.");
            }

            if (!item.Name.StartsWith('$'))
            {
                throw new InvalidOperationException($"SSH rights names must start with '$': {item.Name}");
            }

            if (systemRights.ContainsKey(item.Name))
            {
                throw new InvalidOperationException($"SSH system right cannot be overridden: {item.Name}");
            }

            definitions.Add(item.Name, item.Value.GetString() ?? string.Empty);
        }

        var rights = systemRights;
        foreach (var name in definitions.Keys)
        {
            rights[name] = ResolveRight(name, definitions, rights, []);
        }

        return rights;
    }

    private static AllowedRootAccess ResolveRight(
        string name,
        IReadOnlyDictionary<string, string> definitions,
        Dictionary<string, AllowedRootAccess> rights,
        IReadOnlyCollection<string> resolvingNames)
    {
        if (rights.TryGetValue(name, out var existingAccess)
            && !definitions.ContainsKey(name))
        {
            return existingAccess;
        }

        if (!definitions.TryGetValue(name, out var expression))
        {
            throw new InvalidOperationException($"Unknown SSH right: {name}");
        }

        if (resolvingNames.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SSH rights reference is circular: {name}");
        }

        var access = ResolveRightExpression(expression, definitions, rights, resolvingNames.Append(name).ToArray());
        rights[name] = access;
        return access;
    }

    private static AllowedRootAccess ResolveRightExpression(
        string expression,
        IReadOnlyDictionary<string, string> definitions,
        Dictionary<string, AllowedRootAccess> rights,
        IReadOnlyCollection<string> resolvingNames)
    {
        var access = AllowedRootAccess.None;
        foreach (var part in expression.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            access |= definitions.ContainsKey(part)
                ? ResolveRight(part, definitions, rights, resolvingNames)
                : AllowedRootAccessText.Parse(part, rights);
        }

        if (access == AllowedRootAccess.None)
        {
            throw new InvalidOperationException("SSH allowed root access is required.");
        }

        return access;
    }

    private static IReadOnlyCollection<SpecialPathRule> ReadSpecialPaths(JsonElement specialPathsElement)
    {
        if (specialPathsElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (specialPathsElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("SSH special paths must be an object.");
        }

        return specialPathsElement
            .EnumerateObject()
            .Select(item => new SpecialPathRule(item.Name, ReadSpecialPathAction(item.Value)))
            .ToArray();
    }

    private static IReadOnlyCollection<EnvironmentValueRule> ReadEnvironmentValues(JsonElement environmentValuesElement)
    {
        if (environmentValuesElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (environmentValuesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("SSH environment values must be an object.");
        }

        return environmentValuesElement
            .EnumerateObject()
            .Select(item =>
            {
                if (item.Value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException("SSH environment value access must be a string.");
                }

                return new EnvironmentValueRule(
                    item.Name,
                    EnvironmentValueAccessText.Parse(item.Value.GetString()));
            })
            .ToArray();
    }

    private static IReadOnlyCollection<WebPublicSite> ReadWebPublicSites(
        JsonElement sitesElement,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (sitesElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (sitesElement.ValueKind == JsonValueKind.Object)
        {
            return sitesElement
                .EnumerateObject()
                .Select(site => ReadWebPublicSiteProperty(site, rights))
                .ToArray();
        }

        if (sitesElement.ValueKind == JsonValueKind.Array)
        {
            return sitesElement
                .EnumerateArray()
                .Select(site => ReadWebPublicSiteObject(site, rights))
                .ToArray();
        }

        throw new InvalidOperationException("SSH web public sites must be an object or an array.");
    }

    private static WebPublicSite ReadWebPublicSiteProperty(
        JsonProperty property,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (property.Value.ValueKind == JsonValueKind.String)
        {
            return CreateWebPublicSite(property.Name, property.Name, property.Value.GetString() ?? string.Empty);
        }

        var site = ReadWebPublicSiteObject(property.Value, rights);
        if (!string.IsNullOrWhiteSpace(site.SiteKey))
        {
            return site;
        }

        return new WebPublicSite
        {
            SiteKey = property.Name,
            DisplayName = string.IsNullOrWhiteSpace(site.DisplayName) ? property.Name : site.DisplayName,
            RootPath = site.RootPath,
            AllowedExtensions = site.AllowedExtensions,
            WritableExecutableExtensions = site.WritableExecutableExtensions,
            AllowedContentTypes = site.AllowedContentTypes,
            AllowedFiles = site.AllowedFiles,
            CreateDirectories = site.CreateDirectories,
            MaxReadBytes = site.MaxReadBytes,
            MaxWriteBytes = site.MaxWriteBytes,
        };
    }

    private static WebPublicSite ReadWebPublicSiteObject(
        JsonElement siteElement,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (siteElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("SSH web public site items must be objects.");
        }

        if (siteElement.TryGetProperty("AllowdFiles", out _)
            || siteElement.TryGetProperty("AllowdFoles", out _))
        {
            throw new InvalidOperationException("SSH web public site file rules must use AllowedFiles.");
        }

        var options = JsonSerializer.Deserialize<WebPublicSiteOptions>(siteElement.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("SSH web public site item is empty.");

        var allowedFiles = ReadAllowedFiles(options.AllowedFiles, rights);
        var allowedContentTypes = ReadAllowedContentTypes(options.AllowedContentTypes, rights)
            .Concat(allowedFiles.ContentTypeRules)
            .ToArray();
        var rootPath = string.IsNullOrWhiteSpace(options.RootPath) ? options.Root : options.RootPath;
        return new WebPublicSite
        {
            SiteKey = options.SiteKey,
            DisplayName = string.IsNullOrWhiteSpace(options.DisplayName) ? options.SiteKey : options.DisplayName,
            RootPath = rootPath,
            AllowedExtensions = ReadStringArray(options.AllowedExtensions),
            WritableExecutableExtensions = ReadWritableExecutableExtensions(options.WritableExecutableExtensions),
            AllowedContentTypes = allowedContentTypes,
            AllowedFiles = allowedFiles.FileRules,
            CreateDirectories = options.CreateDirectories ?? true,
            MaxReadBytes = options.MaxReadBytes ?? 5 * 1024 * 1024,
            MaxWriteBytes = options.MaxWriteBytes ?? 5 * 1024 * 1024,
        };
    }

    private static WebPublicAllowedFileRules ReadAllowedFiles(
        JsonElement allowedFilesElement,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (allowedFilesElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return new WebPublicAllowedFileRules([], []);
        }

        if (allowedFilesElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("SSH web public allowed files must be an object.");
        }

        var fileRules = new List<WebPublicFileRule>();
        var contentTypeRules = new List<WebPublicContentTypeRule>();
        foreach (var item in allowedFilesElement.EnumerateObject())
        {
            var ruleKey = item.Name.Trim();
            var access = ReadAllowedFileAccess(item.Value, rights);
            if (ruleKey.StartsWith("mime:", StringComparison.OrdinalIgnoreCase))
            {
                var contentType = ruleKey["mime:".Length..].Trim();
                if (string.IsNullOrWhiteSpace(contentType))
                {
                    throw new InvalidOperationException("SSH web public allowed file MIME type must not be empty.");
                }

                contentTypeRules.Add(new WebPublicContentTypeRule(contentType, access));
                continue;
            }

            var pattern = ruleKey.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                ? ruleKey["file:".Length..].Trim()
                : ruleKey;
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new InvalidOperationException("SSH web public allowed file pattern must not be empty.");
            }

            fileRules.Add(new WebPublicFileRule(pattern, access));
        }

        return new WebPublicAllowedFileRules(fileRules, contentTypeRules);
    }

    private static IReadOnlyCollection<WebPublicContentTypeRule> ReadAllowedContentTypes(
        JsonElement allowedContentTypesElement,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (allowedContentTypesElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (allowedContentTypesElement.ValueKind == JsonValueKind.Object)
        {
            return allowedContentTypesElement
                .EnumerateObject()
                .Select(item => new WebPublicContentTypeRule(item.Name, ReadAllowedContentTypeAccess(item.Value, rights)))
                .ToArray();
        }

        if (allowedContentTypesElement.ValueKind == JsonValueKind.Array)
        {
            return allowedContentTypesElement
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String
                    ? new WebPublicContentTypeRule(item.GetString() ?? string.Empty, AllowedRootAccess.Read | AllowedRootAccess.Write)
                    : throw new InvalidOperationException("SSH web public allowed content type items must be strings."))
                .Where(item => !string.IsNullOrWhiteSpace(item.ContentType))
                .ToArray();
        }

        throw new InvalidOperationException("SSH web public allowed content types must be an object or an array.");
    }

    private static AllowedRootAccess ReadAllowedContentTypeAccess(
        JsonElement value,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("SSH web public allowed content type access must be a string.");
        }

        return AllowedRootAccessText.Parse(value.GetString(), rights);
    }

    private static AllowedRootAccess ReadAllowedFileAccess(
        JsonElement value,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("SSH web public allowed file access must be a string.");
        }

        return AllowedRootAccessText.Parse(value.GetString(), rights);
    }

    private sealed record WebPublicAllowedFileRules(
        IReadOnlyCollection<WebPublicFileRule> FileRules,
        IReadOnlyCollection<WebPublicContentTypeRule> ContentTypeRules);

    private static WebPublicSite CreateWebPublicSite(string siteKey, string displayName, string rootPath)
    {
        return new WebPublicSite
        {
            SiteKey = siteKey,
            DisplayName = displayName,
            RootPath = rootPath,
        };
    }

    private static IReadOnlyCollection<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("SSH web public site string settings must be arrays.");
        }

        return element
            .EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? item.GetString() ?? string.Empty
                : throw new InvalidOperationException("SSH web public site string items must be strings."))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static IReadOnlyCollection<string> ReadWritableExecutableExtensions(JsonElement element)
    {
        return ReadStringArray(element)
            .Select(item => item.Trim())
            .Select(item =>
            {
                if (!item.StartsWith(".", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("SSH web public writable executable extensions must start with a dot.");
                }

                if (item == "."
                    || item.Contains('*', StringComparison.Ordinal)
                    || item.Contains('?', StringComparison.Ordinal)
                    || item.Contains('/', StringComparison.Ordinal)
                    || item.Contains('\\', StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("SSH web public writable executable extensions must be explicit extensions without wildcards.");
                }

                return item;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SpecialPathAction ReadSpecialPathAction(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("SSH special path action must be a string.");
        }

        var actionName = value.GetString();
        if (Enum.TryParse<SpecialPathAction>(actionName, ignoreCase: true, out var action))
        {
            return action;
        }

        throw new InvalidOperationException($"Unknown SSH special path action: {actionName}");
    }

    private static IReadOnlyCollection<SshConnectionUser> ResolveUsers(
        JsonElement usersElement,
        string baseDirectory,
        IReadOnlyCollection<string> profileRoles,
        KelpiePolicyMode profileMode,
        PolicySet profileCapabilities,
        IReadOnlyCollection<AllowedRootRule> profileAllowedRoots,
        IReadOnlyCollection<SpecialPathRule> profileSpecialPaths,
        IReadOnlyCollection<EnvironmentValueRule> profileEnvironmentValues,
        IReadOnlyDictionary<string, AllowedRootAccess> rights,
        IReadOnlyCollection<WebPublicSite> webPublicSites,
        SshConnectionServices services,
        SshConnectionAuthenticationOptions authentication,
        string legacyUserName)
    {
        var users = ReadUsers(usersElement);
        if (users.Count == 0)
        {
            return
            [
                new SshConnectionUser
                {
                    UserName = legacyUserName,
                    AuthenticationMethod = authentication.Method,
                    PrivateKeyPath = ResolvePrivateKeyPath(baseDirectory, authentication),
                    PrivateKeyPassphrase = authentication.PrivateKeyPassphrase,
                    PasswordSecretName = authentication.PasswordSecretName,
                    Mode = profileMode,
                    Capabilities = profileCapabilities,
                    Roles = profileRoles,
                    AllowedRootRules = profileAllowedRoots,
                    SpecialPaths = profileSpecialPaths,
                    EnvironmentValues = profileEnvironmentValues,
                    WebPublicSites = webPublicSites,
                },
            ];
        }

        return users.Select(user =>
        {
            var method = string.IsNullOrWhiteSpace(user.Method)
                ? authentication.Method
                : user.Method;

            var roles = ResolveUserRoles(user, profileRoles);
            var mode = ResolveModeFromRoles(roles);
            var userWebPublicSites = user.WebPublicSites.ValueKind == JsonValueKind.Undefined
                ? webPublicSites
                : ReadWebPublicSites(user.WebPublicSites, rights);
            var userAllowedRoots = user.AllowedRoots.ValueKind == JsonValueKind.Undefined
                ? profileAllowedRoots
                : ReadAllowedRoots(user.AllowedRoots, rights);
            userAllowedRoots = ApplyRolesToAllowedRoots(userAllowedRoots, roles, userWebPublicSites, services);

            return new SshConnectionUser
            {
                UserName = user.UserName,
                AuthenticationMethod = method,
                PrivateKeyPath = string.Equals(method, "privateKey", StringComparison.OrdinalIgnoreCase)
                    ? ResolvePrivateKeyPath(baseDirectory, user) ?? ResolvePrivateKeyPath(baseDirectory, authentication)
                    : null,
                PrivateKeyPassphrase = string.Equals(method, "privateKey", StringComparison.OrdinalIgnoreCase)
                    ? user.PrivateKeyPassphrase ?? authentication.PrivateKeyPassphrase
                    : null,
                PasswordSecretName = string.Equals(method, "password", StringComparison.OrdinalIgnoreCase)
                    ? user.PasswordSecretName ?? authentication.PasswordSecretName
                    : null,
                Mode = mode,
                Capabilities = user.Capabilities.ValueKind == JsonValueKind.Undefined
                    ? profileCapabilities
                    : PolicySet.FromJson(user.Capabilities),
                Roles = roles,
                AllowedRootRules = userAllowedRoots,
                SpecialPaths = user.SpecialPaths.ValueKind == JsonValueKind.Undefined
                    ? profileSpecialPaths
                    : ReadSpecialPaths(user.SpecialPaths),
                EnvironmentValues = user.EnvironmentValues.ValueKind == JsonValueKind.Undefined
                    ? profileEnvironmentValues
                    : ReadEnvironmentValues(user.EnvironmentValues),
                WebPublicSites = userWebPublicSites,
            };
        }).ToArray();
    }

    private static IReadOnlyCollection<SshConnectionUserOptions> ReadUsers(JsonElement usersElement)
    {
        if (usersElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (usersElement.ValueKind == JsonValueKind.Array)
        {
            return usersElement
                .EnumerateArray()
                .Select(ReadUserObject)
                .ToArray();
        }

        if (usersElement.ValueKind == JsonValueKind.Object)
        {
            return usersElement
                .EnumerateObject()
                .Select(ReadUserProperty)
                .ToArray();
        }

        throw new InvalidOperationException("SSH users must be an object or an array.");
    }

    private static SshConnectionUserOptions ReadUserProperty(JsonProperty property)
    {
        if (property.Value.ValueKind == JsonValueKind.String)
        {
            return new SshConnectionUserOptions
            {
                UserName = property.Name,
                Mode = property.Value.GetString() ?? string.Empty,
            };
        }

        if (property.Value.ValueKind == JsonValueKind.Object)
        {
            var user = ReadUserObject(property.Value);
            user.UserName = string.IsNullOrWhiteSpace(user.UserName) ? property.Name : user.UserName;
            return user;
        }

        throw new InvalidOperationException("SSH user items must be strings or objects.");
    }

    private static SshConnectionUserOptions ReadUserObject(JsonElement userElement)
    {
        if (userElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("SSH user array items must be objects.");
        }

        return new SshConnectionUserOptions
        {
            UserName = ReadOptionalString(userElement, "UserName"),
            Method = ReadOptionalString(userElement, "Method"),
            PrivateKeyFile = ReadOptionalString(userElement, "PrivateKeyFile"),
            PrivateKeyPath = ReadOptionalString(userElement, "PrivateKeyPath"),
            PrivateKeyPassphrase = ReadOptionalNullableString(userElement, "PrivateKeyPassphrase"),
            PasswordSecretName = ReadOptionalNullableString(userElement, "PasswordSecretName"),
            Mode = ReadOptionalString(userElement, "Mode"),
            Roles = ReadOptionalElement(userElement, "Roles"),
            Capabilities = ReadOptionalElement(userElement, "Capabilities"),
            AllowedRoots = ReadOptionalElement(userElement, "AllowedRoots"),
            SpecialPaths = ReadOptionalElement(userElement, "SpecialPaths"),
            EnvironmentValues = ReadOptionalElement(userElement, "EnvironmentValues"),
            WebPublicSites = ReadOptionalElement(userElement, "WebPublicSites"),
        };
    }

    private static SshConnectionServices ReadServices(SshConnectionServicesOptions? options)
    {
        if (options?.Nginx is null)
        {
            return new SshConnectionServices();
        }

        return new SshConnectionServices
        {
            Nginx = IsNginxServiceConfigured(options.Nginx)
                ? new NginxServiceSettings
                {
                    User = options.Nginx.User,
                    Group = options.Nginx.Group,
                    Port = options.Nginx.Port,
                    Root = options.Nginx.Root,
                }
                : null,
        };
    }

    private static bool IsNginxServiceConfigured(NginxServiceOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.User)
            || !string.IsNullOrWhiteSpace(options.Group)
            || options.Port.HasValue
            || !string.IsNullOrWhiteSpace(options.Root);
    }

    private static IReadOnlyCollection<string> ResolveUserRoles(
        SshConnectionUserOptions user,
        IReadOnlyCollection<string> profileRoles)
    {
        var hasUserRoleExpression = !string.IsNullOrWhiteSpace(user.Mode)
            || user.Roles.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;

        return hasUserRoleExpression
            ? ResolveRoles(user.Mode, user.Roles, defaultRoles: [KelpieRoleNames.Safe])
            : profileRoles;
    }

    private static IReadOnlyCollection<string> ResolveRoles(
        string roleExpression,
        JsonElement rolesElement,
        IReadOnlyCollection<string> defaultRoles)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in SplitRoles(roleExpression))
        {
            roles.Add(role);
        }

        foreach (var role in ReadRoles(rolesElement))
        {
            roles.Add(role);
        }

        if (!roles.Any(KelpieRoleNames.IsPolicyRole))
        {
            foreach (var role in defaultRoles)
            {
                roles.Add(role);
            }
        }

        foreach (var role in roles)
        {
            if (!KelpieRoleNames.IsKnown(role))
            {
                throw new InvalidOperationException($"Unknown SSH role: {role}");
            }
        }

        return roles.ToArray();
    }

    private static KelpiePolicyMode ResolveModeFromRoles(IReadOnlyCollection<string> roles)
    {
        if (roles.Contains(KelpieRoleNames.Expert, StringComparer.OrdinalIgnoreCase))
        {
            return KelpiePolicyMode.Expert;
        }

        if (roles.Contains(KelpieRoleNames.Maintenance, StringComparer.OrdinalIgnoreCase))
        {
            return KelpiePolicyMode.Maintenance;
        }

        if (roles.Contains(KelpieRoleNames.Safe, StringComparer.OrdinalIgnoreCase))
        {
            return KelpiePolicyMode.Safe;
        }

        if (roles.Contains(KelpieRoleNames.ReadOnly, StringComparer.OrdinalIgnoreCase))
        {
            return KelpiePolicyMode.ReadOnly;
        }

        return KelpiePolicyMode.Safe;
    }

    private static IReadOnlyCollection<string> ReadRoles(JsonElement rolesElement)
    {
        if (rolesElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return [];
        }

        if (rolesElement.ValueKind == JsonValueKind.String)
        {
            return SplitRoles(rolesElement.GetString()).ToArray();
        }

        if (rolesElement.ValueKind == JsonValueKind.Array)
        {
            return rolesElement
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? string.Empty
                    : throw new InvalidOperationException("SSH user roles must be strings."))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        throw new InvalidOperationException("SSH user roles must be a string or an array.");
    }

    private static IEnumerable<string> SplitRoles(string? roleExpression)
    {
        return string.IsNullOrWhiteSpace(roleExpression)
            ? []
            : roleExpression.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyCollection<AllowedRootRule> ApplyRolesToAllowedRoots(
        IReadOnlyCollection<AllowedRootRule> allowedRoots,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<WebPublicSite> webPublicSites,
        SshConnectionServices services)
    {
        if (!roles.Contains(KelpieRoleNames.WebUser, StringComparer.OrdinalIgnoreCase))
        {
            return allowedRoots;
        }

        var rules = new Dictionary<string, AllowedRootAccess>(StringComparer.Ordinal);
        foreach (var rule in allowedRoots)
        {
            rules[rule.Path] = rule.Access;
        }

        foreach (var root in ResolveWebRoots(webPublicSites, services))
        {
            rules[root] = rules.TryGetValue(root, out var existing)
                ? existing | AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.Write | AllowedRootAccess.CD
                : AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.Write | AllowedRootAccess.CD;
        }

        return rules.Select(item => new AllowedRootRule(item.Key, item.Value)).ToArray();
    }

    private static IReadOnlyCollection<string> ResolveWebRoots(
        IReadOnlyCollection<WebPublicSite> webPublicSites,
        SshConnectionServices services)
    {
        var roots = webPublicSites
            .Select(site => site.RootPath)
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .ToList();
        if (!string.IsNullOrWhiteSpace(services.Nginx?.Root))
        {
            roots.Add(services.Nginx.Root);
        }

        if (roots.Count == 0)
        {
            roots.Add("/var/www");
        }

        return roots.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string ReadOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
    }

    private static string? ReadOptionalNullableString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static JsonElement ReadOptionalElement(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) ? property.Clone() : default;
    }

    private static SshConnectionUser ResolveSelectedUser(
        IReadOnlyCollection<SshConnectionUser> users,
        string legacyUserName,
        string defaultUser)
    {
        if (users.Count == 0)
        {
            throw new InvalidOperationException("SSH profile user settings are required.");
        }

        var selectedName = string.IsNullOrWhiteSpace(defaultUser) ? legacyUserName : defaultUser;
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            if (users.Count == 1)
            {
                return users.First();
            }

            throw new InvalidOperationException("SSH default user is required when multiple Users are configured.");
        }

        var selectedUser = users.FirstOrDefault(user =>
            string.Equals(user.UserName, selectedName, StringComparison.OrdinalIgnoreCase));

        return selectedUser
            ?? throw new InvalidOperationException($"SSH profile user was not found: {selectedName}");
    }

    private static IEnumerable<string> SplitLegacyPolicyLevel(string level)
    {
        if (string.IsNullOrWhiteSpace(level) || string.Equals(level, "ReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return level.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? ResolvePrivateKeyPath(
        string baseDirectory,
        SshConnectionAuthenticationOptions authentication)
    {
        if (!string.Equals(authentication.Method, "privateKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(authentication.PrivateKeyFile))
        {
            return ResolvePrivateKeyFile(baseDirectory, authentication.PrivateKeyFile);
        }

        if (string.IsNullOrWhiteSpace(authentication.PrivateKeyPath))
        {
            return null;
        }

        return Path.IsPathRooted(authentication.PrivateKeyPath)
            ? authentication.PrivateKeyPath
            : Path.GetFullPath(Path.Combine(baseDirectory, authentication.PrivateKeyPath));
    }

    private static string? ResolvePrivateKeyPath(
        string baseDirectory,
        SshConnectionUserOptions user)
    {
        if (!string.Equals(user.Method, "privateKey", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(user.PrivateKeyFile))
        {
            return ResolvePrivateKeyFile(baseDirectory, user.PrivateKeyFile);
        }

        if (string.IsNullOrWhiteSpace(user.PrivateKeyPath))
        {
            return null;
        }

        return Path.IsPathRooted(user.PrivateKeyPath)
            ? user.PrivateKeyPath
            : Path.GetFullPath(Path.Combine(baseDirectory, user.PrivateKeyPath));
    }

    private static string ResolvePrivateKeyFile(string baseDirectory, string privateKeyFile)
    {
        if (Path.IsPathRooted(privateKeyFile))
        {
            return Path.GetFullPath(privateKeyFile);
        }

        var profilesDirectory = Path.GetFullPath(baseDirectory);
        var homeDirectory = Directory.GetParent(profilesDirectory)?.FullName ?? profilesDirectory;
        return Path.GetFullPath(Path.Combine(homeDirectory, "keys", privateKeyFile));
    }

}
