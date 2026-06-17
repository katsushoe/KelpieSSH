namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides SSH profiles from disk and reloads them on demand.
/// </summary>
public sealed class ReloadingSshConnectionProfileCatalog : ISshConnectionProfileCatalog
{
    private readonly object _gate = new();
    private readonly string _profilesDirectory;
    private SshConnectionProfileCatalog _current;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReloadingSshConnectionProfileCatalog"/> class.
    /// </summary>
    /// <param name="profilesDirectory">The directory that contains profile JSON files.</param>
    public ReloadingSshConnectionProfileCatalog(string profilesDirectory)
    {
        if (string.IsNullOrWhiteSpace(profilesDirectory))
        {
            throw new InvalidOperationException("SSH profiles directory is required.");
        }

        _profilesDirectory = Path.GetFullPath(profilesDirectory);
        _current = LoadCatalog(_profilesDirectory);
    }

    /// <summary>
    /// Gets the most recent reload error. The last good catalog remains active when reload fails.
    /// </summary>
    public Exception? LastReloadError { get; private set; }

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
                var next = LoadCatalog(_profilesDirectory);
                _current = next;
                LastReloadError = null;
                var profiles = next.List();
                return new SshConnectionProfileReloadResult(
                    Success: true,
                    ProfilesDirectory: _profilesDirectory,
                    ProfileCount: profiles.Count,
                    ProfileNames: profiles.Select(profile => profile.Name).ToArray(),
                    ErrorMessage: null);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or System.Text.Json.JsonException)
            {
                LastReloadError = ex;
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

    private static SshConnectionProfileCatalog LoadCatalog(string profilesDirectory)
    {
        return new SshConnectionProfileCatalog(
            SshConnectionProfileFileLoader.LoadDirectory(profilesDirectory));
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
