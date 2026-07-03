using System.Collections.Concurrent;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Stores short-lived secret payloads in process memory.
/// </summary>
public sealed class InMemoryKelpieSecretStore : IKelpieSecretStore
{
    private static readonly TimeSpan MaxTtl = TimeSpan.FromHours(1);
    private const int MaxSecretBytes = 1024 * 1024;

    private readonly ConcurrentDictionary<string, SecretEntry> _secrets = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public KelpieSecretInfo Put(string name, byte[] content, TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(content);
        var normalizedName = NormalizeName(name);
        if (content.Length == 0)
        {
            throw new ArgumentException("Secret content is required.", nameof(content));
        }

        if (content.Length > MaxSecretBytes)
        {
            throw new ArgumentException($"Secret content exceeds the maximum size of {MaxSecretBytes} bytes.", nameof(content));
        }

        var normalizedTtl = ttl <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : ttl;
        if (normalizedTtl > MaxTtl)
        {
            normalizedTtl = MaxTtl;
        }

        var now = DateTimeOffset.UtcNow;
        var info = new KelpieSecretInfo(
            normalizedName,
            content.Length,
            now,
            now.Add(normalizedTtl));
        _secrets[normalizedName] = new SecretEntry(content.ToArray(), info);
        return info;
    }

    /// <inheritdoc />
    public bool TryGetContentBase64(string name, out string contentBase64, out KelpieSecretInfo? info)
    {
        contentBase64 = string.Empty;
        info = null;
        var normalizedName = NormalizeNameOrEmpty(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        RemoveExpired();
        if (!_secrets.TryGetValue(normalizedName, out var entry))
        {
            return false;
        }

        info = entry.Info;
        contentBase64 = Convert.ToBase64String(entry.Content);
        return true;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<KelpieSecretInfo> List()
    {
        RemoveExpired();
        return _secrets.Values
            .Select(entry => entry.Info)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public bool Forget(string name)
    {
        var normalizedName = NormalizeNameOrEmpty(name);
        return !string.IsNullOrWhiteSpace(normalizedName)
            && _secrets.TryRemove(normalizedName, out _);
    }

    private void RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var item in _secrets)
        {
            if (item.Value.Info.ExpiresAtUtc <= now)
            {
                _secrets.TryRemove(item.Key, out _);
            }
        }
    }

    private static string NormalizeName(string name)
    {
        var normalizedName = NormalizeNameOrEmpty(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Secret name is required.", nameof(name));
        }

        if (normalizedName.Length > 128
            || normalizedName.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ':')))
        {
            throw new ArgumentException("Secret name must be 1 to 128 characters and contain only letters, digits, dot, dash, underscore, or colon.", nameof(name));
        }

        return normalizedName;
    }

    private static string NormalizeNameOrEmpty(string name)
    {
        return name?.Trim() ?? string.Empty;
    }

    private sealed record SecretEntry(byte[] Content, KelpieSecretInfo Info);
}
