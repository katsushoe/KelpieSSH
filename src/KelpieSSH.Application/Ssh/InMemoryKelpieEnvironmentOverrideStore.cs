using System.Collections.Concurrent;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Stores short-lived environment overrides in memory.
/// </summary>
public sealed class InMemoryKelpieEnvironmentOverrideStore : IKelpieEnvironmentOverrideStore
{
    private const int MaxValueLength = 64 * 1024;

    private readonly ConcurrentDictionary<EnvironmentOverrideKey, EnvironmentOverrideEntry> _entries = new();

    /// <inheritdoc />
    public KelpieEnvironmentOverrideInfo Put(string profileName, string key, string value)
    {
        var normalizedProfileName = NormalizeProfileName(profileName);
        var normalizedKey = NormalizeKey(key);
        if (value.Length > MaxValueLength)
        {
            throw new ArgumentException("Environment value is too large.", nameof(value));
        }

        if (value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
        {
            throw new ArgumentException("Environment value must not contain newline characters.", nameof(value));
        }

        var entry = new EnvironmentOverrideEntry(
            normalizedProfileName,
            normalizedKey,
            value,
            DateTimeOffset.UtcNow);
        _entries[new EnvironmentOverrideKey(normalizedProfileName, normalizedKey)] = entry;
        return entry.ToInfo();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<KelpieEnvironmentOverrideInfo> List(string? profileName = null)
    {
        var normalizedProfileName = string.IsNullOrWhiteSpace(profileName)
            ? null
            : NormalizeProfileName(profileName);

        return _entries.Values
            .Where(entry => normalizedProfileName is null
                || string.Equals(entry.ProfileName, normalizedProfileName, StringComparison.Ordinal))
            .OrderBy(entry => entry.ProfileName, StringComparer.Ordinal)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => entry.ToInfo())
            .ToArray();
    }

    /// <inheritdoc />
    public bool Forget(string profileName, string key)
    {
        return _entries.TryRemove(
            new EnvironmentOverrideKey(NormalizeProfileName(profileName), NormalizeKey(key)),
            out _);
    }

    /// <inheritdoc />
    public int Clear(string profileName)
    {
        var normalizedProfileName = NormalizeProfileName(profileName);
        var keys = _entries.Keys
            .Where(key => string.Equals(key.ProfileName, normalizedProfileName, StringComparison.Ordinal))
            .ToArray();

        var removed = 0;
        foreach (var key in keys)
        {
            if (_entries.TryRemove(key, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetValues(string profileName)
    {
        var normalizedProfileName = NormalizeProfileName(profileName);
        return _entries.Values
            .Where(entry => string.Equals(entry.ProfileName, normalizedProfileName, StringComparison.Ordinal))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
    }

    private static string NormalizeProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name is required.", nameof(profileName));
        }

        return profileName.Trim();
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Environment variable key is required.", nameof(key));
        }

        return key.Trim();
    }

    private readonly record struct EnvironmentOverrideKey(string ProfileName, string Key);

    private sealed record EnvironmentOverrideEntry(
        string ProfileName,
        string Key,
        string Value,
        DateTimeOffset UpdatedAtUtc)
    {
        public KelpieEnvironmentOverrideInfo ToInfo()
        {
            return new KelpieEnvironmentOverrideInfo(ProfileName, Key, Value.Length, UpdatedAtUtc);
        }
    }
}
