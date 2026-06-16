namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides configured SSH connection profiles.
/// </summary>
public sealed class SshConnectionProfileCatalog : ISshConnectionProfileCatalog
{
    private readonly Dictionary<string, SshConnectionProfile> _profiles;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshConnectionProfileCatalog"/> class.
    /// </summary>
    /// <param name="profiles">The configured profiles.</param>
    public SshConnectionProfileCatalog(IEnumerable<SshConnectionProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        _profiles = new Dictionary<string, SshConnectionProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            profile.Validate();
            if (!_profiles.TryAdd(profile.Name, profile))
            {
                throw new InvalidOperationException($"SSH profile is duplicated: {profile.Name}");
            }
        }
    }

    /// <inheritdoc />
    public bool TryGet(string name, out SshConnectionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            profile = default!;
            return false;
        }

        return _profiles.TryGetValue(name, out profile!);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<SshConnectionProfile> List()
    {
        return _profiles.Values.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
