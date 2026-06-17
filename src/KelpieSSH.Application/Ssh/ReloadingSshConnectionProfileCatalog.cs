namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides SSH profiles from disk and reloads them on demand.
/// </summary>
public sealed class ReloadingSshConnectionProfileCatalog : ISshConnectionProfileCatalog
{
    private readonly object _gate = new();
    private readonly string _profilesDirectory;
    private readonly string? _trustStorePath;
    private readonly IReadOnlyCollection<string> _reloadProfileNames;
    private SshConnectionProfileCatalog _current;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReloadingSshConnectionProfileCatalog"/> class.
    /// </summary>
    /// <param name="profilesDirectory">The directory that contains profile JSON files.</param>
    public ReloadingSshConnectionProfileCatalog(string profilesDirectory)
        : this(profilesDirectory, trustStorePath: null, reloadProfileNames: [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReloadingSshConnectionProfileCatalog"/> class.
    /// </summary>
    /// <param name="profilesDirectory">The directory that contains profile JSON files.</param>
    /// <param name="trustStorePath">The optional protected profile trust store file path.</param>
    /// <param name="reloadProfileNames">The profile names explicitly accepted for this load.</param>
    public ReloadingSshConnectionProfileCatalog(
        string profilesDirectory,
        string? trustStorePath,
        IEnumerable<string> reloadProfileNames)
    {
        if (string.IsNullOrWhiteSpace(profilesDirectory))
        {
            throw new InvalidOperationException("SSH profiles directory is required.");
        }

        _profilesDirectory = Path.GetFullPath(profilesDirectory);
        _trustStorePath = string.IsNullOrWhiteSpace(trustStorePath) ? null : Path.GetFullPath(trustStorePath);
        _reloadProfileNames = reloadProfileNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _current = LoadCatalog(_profilesDirectory, _trustStorePath, _reloadProfileNames, out var loadErrors);
        ProfileLoadErrors = loadErrors;
    }

    /// <summary>
    /// Gets the most recent reload error. The last good catalog remains active when reload fails.
    /// </summary>
    public Exception? LastReloadError { get; private set; }

    /// <summary>
    /// Gets the most recent profile-level load errors.
    /// </summary>
    public IReadOnlyCollection<SshConnectionProfileLoadError> ProfileLoadErrors { get; private set; } = [];

    /// <summary>
    /// Gets the profiles directory.
    /// </summary>
    public string ProfilesDirectory => _profilesDirectory;

    /// <inheritdoc />
    public bool TryGet(string name, out SshConnectionProfile profile)
    {
        return _current.TryGet(name, out profile);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<SshConnectionProfile> List()
    {
        return _current.List();
    }

    /// <summary>
    /// Reloads profile JSON files from disk.
    /// </summary>
    /// <returns>The reload result.</returns>
    public SshConnectionProfileReloadResult Reload()
    {
        lock (_gate)
        {
            try
            {
                var next = LoadCatalog(_profilesDirectory, _trustStorePath, _reloadProfileNames, out var loadErrors);
                _current = next;
                ProfileLoadErrors = loadErrors;
                LastReloadError = loadErrors.Count == 0
                    ? null
                    : new InvalidOperationException(string.Join("; ", loadErrors.Select(error => error.Message)));
                var profiles = next.List();
                return new SshConnectionProfileReloadResult(
                    Success: loadErrors.Count == 0,
                    ProfilesDirectory: _profilesDirectory,
                    ProfileCount: profiles.Count,
                    ProfileNames: profiles.Select(profile => profile.Name).ToArray(),
                    ErrorMessage: LastReloadError?.Message);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or System.Text.Json.JsonException)
            {
                LastReloadError = ex;
                ProfileLoadErrors = [];
                var profiles = _current.List();
                return new SshConnectionProfileReloadResult(
                    Success: false,
                    ProfilesDirectory: _profilesDirectory,
                    ProfileCount: profiles.Count,
                    ProfileNames: profiles.Select(profile => profile.Name).ToArray(),
                    ErrorMessage: ex.Message);
            }
        }
    }

    private static SshConnectionProfileCatalog LoadCatalog(
        string profilesDirectory,
        string? trustStorePath,
        IReadOnlyCollection<string> reloadProfileNames,
        out IReadOnlyCollection<SshConnectionProfileLoadError> loadErrors)
    {
        if (string.IsNullOrWhiteSpace(trustStorePath))
        {
            loadErrors = [];
            return new SshConnectionProfileCatalog(
                SshConnectionProfileFileLoader.LoadDirectory(profilesDirectory));
        }

        return LoadTrustedCatalog(profilesDirectory, trustStorePath, reloadProfileNames, out loadErrors);
    }

    private static SshConnectionProfileCatalog LoadTrustedCatalog(
        string profilesDirectory,
        string trustStorePath,
        IReadOnlyCollection<string> reloadProfileNames,
        out IReadOnlyCollection<SshConnectionProfileLoadError> loadErrors)
    {
        var trustStore = SshProfileTrustStore.Load(trustStorePath);
        var reloadProfileSet = reloadProfileNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var profiles = new List<SshConnectionProfile>();
        var errors = new List<SshConnectionProfileLoadError>();
        var trustStoreChanged = false;

        if (Directory.Exists(profilesDirectory))
        {
            foreach (var filePath in Directory
                .EnumerateFiles(profilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase))
            {
                var profileName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrWhiteSpace(profileName))
                {
                    continue;
                }

                var currentHash = SshProfileTrustStore.ComputeFileHash(filePath);
                var explicitReload = reloadProfileSet.Contains(profileName);
                if (!explicitReload
                    && trustStore.TryGetHash(profileName, out var trustedHash)
                    && !string.Equals(currentHash, trustedHash, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new SshConnectionProfileLoadError(
                        profileName,
                        filePath,
                        "profile-hash-mismatch",
                        $"SSH profile hash does not match trusted baseline: {profileName}"));
                    continue;
                }

                try
                {
                    profiles.Add(SshConnectionProfileFileLoader.LoadFile(filePath));
                }
                catch (Exception ex) when (ex is IOException
                    or UnauthorizedAccessException
                    or InvalidOperationException
                    or System.Text.Json.JsonException)
                {
                    errors.Add(new SshConnectionProfileLoadError(
                        profileName,
                        filePath,
                        "profile-load-failed",
                        ex.Message));
                    continue;
                }

                if (explicitReload || !trustStore.TryGetHash(profileName, out _))
                {
                    trustStore.SetHash(profileName, currentHash);
                    trustStoreChanged = true;
                }
            }
        }

        foreach (var reloadProfileName in reloadProfileSet)
        {
            if (profiles.Any(profile => string.Equals(profile.Name, reloadProfileName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (errors.Any(error => string.Equals(error.ProfileName, reloadProfileName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            errors.Add(new SshConnectionProfileLoadError(
                reloadProfileName,
                string.Empty,
                "profile-not-found",
                $"SSH profile file was not found: {reloadProfileName}"));
        }

        if (trustStoreChanged || (!trustStore.FileExisted && errors.Count == 0))
        {
            trustStore.Save(trustStorePath);
        }

        loadErrors = errors;
        return new SshConnectionProfileCatalog(profiles);
    }
}

/// <summary>
/// Represents a profile catalog reload result.
/// </summary>
/// <param name="Success">A value indicating whether reload succeeded.</param>
/// <param name="ProfilesDirectory">The profile directory.</param>
/// <param name="ProfileCount">The active profile count after the reload attempt.</param>
/// <param name="ProfileNames">The active profile names after the reload attempt.</param>
/// <param name="ErrorMessage">The reload error message when reload failed.</param>
public sealed record SshConnectionProfileReloadResult(
    bool Success,
    string ProfilesDirectory,
    int ProfileCount,
    IReadOnlyCollection<string> ProfileNames,
    string? ErrorMessage);

/// <summary>
/// Represents one profile-level load error.
/// </summary>
/// <param name="ProfileName">The profile name.</param>
/// <param name="FilePath">The profile file path when available.</param>
/// <param name="Reason">The stable error reason.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record SshConnectionProfileLoadError(
    string ProfileName,
    string FilePath,
    string Reason,
    string Message);
