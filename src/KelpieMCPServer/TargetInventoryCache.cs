using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using KelpieSSH.Application.Ssh;

namespace KelpieMCPServer;

/// <summary>
/// Caches successful target inventory probes for the current MCP server process.
/// </summary>
public sealed class TargetInventoryCache
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<KelpieTools.TargetInventoryResult>>> _probes = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TargetInventoryCache"/> class.
    /// </summary>
    public TargetInventoryCache()
        : this(TimeProvider.System)
    {
    }

    internal TargetInventoryCache(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets a current inventory result or executes one shared probe.
    /// </summary>
    public async Task<KelpieTools.TargetInventoryResult> GetOrCreateAsync(
        SshConnectionProfile profile,
        bool refresh,
        Func<Task<KelpieTools.TargetInventoryResult>> probe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(probe);

        var key = CreateKey(profile);
        var now = _timeProvider.GetUtcNow();
        if (!refresh && _entries.TryGetValue(key, out var entry) && entry.ExpiresAt > now)
        {
            return entry.Result with { Cached = true };
        }

        var lazyProbe = _probes.GetOrAdd(
            key,
            _ => new Lazy<Task<KelpieTools.TargetInventoryResult>>(
                async () =>
                {
                    var result = await probe();
                    var checkedAt = _timeProvider.GetUtcNow();
                    var current = result with { Cached = false, CheckedAt = checkedAt };
                    _entries[key] = new CacheEntry(current, checkedAt.Add(CacheLifetime));
                    return current;
                },
                LazyThreadSafetyMode.ExecutionAndPublication));

        var probeTask = lazyProbe.Value;
        _ = probeTask.ContinueWith(
            _ => _probes.TryRemove(new KeyValuePair<string, Lazy<Task<KelpieTools.TargetInventoryResult>>>(key, lazyProbe)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return await probeTask.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Removes all cached inventory snapshots.
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
    }

    private static string CreateKey(SshConnectionProfile profile)
    {
        var identity = string.Join(
            '\n',
            profile.Name,
            profile.Host,
            profile.Port,
            profile.UserName,
            profile.OsFamily,
            profile.PackageManager,
            profile.HostKeyFingerprintSha256 ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private sealed record CacheEntry(
        KelpieTools.TargetInventoryResult Result,
        DateTimeOffset ExpiresAt);
}
