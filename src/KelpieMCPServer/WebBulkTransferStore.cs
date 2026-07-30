using System.Collections.Concurrent;

namespace KelpieMCPServer;

/// <summary>
/// Stores bounded bulk web transfer drafts for the lifetime of the MCP server.
/// </summary>
public sealed class WebBulkTransferStore
{
    private const int MaximumTransfers = 100;
    private const int MaximumFiles = 100;
    private const long MaximumTotalBytes = 256L * 1024 * 1024;
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<string, WebBulkTransfer> _transfers = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new bulk transfer store.
    /// </summary>
    public WebBulkTransferStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Creates a new draft.
    /// </summary>
    public WebBulkTransfer Create(string profileName, string siteKey)
    {
        RemoveExpired();
        if (_transfers.Count >= MaximumTransfers)
        {
            throw new InvalidOperationException($"At most {MaximumTransfers} bulk transfers may be retained.");
        }

        var now = _timeProvider.GetUtcNow();
        var transfer = new WebBulkTransfer(
            Guid.NewGuid().ToString("N"),
            profileName,
            siteKey,
            WebBulkTransferState.Draft,
            now,
            now.Add(Lifetime),
            [],
            []);
        if (!_transfers.TryAdd(transfer.Id, transfer))
        {
            throw new InvalidOperationException("Could not allocate a bulk transfer ID.");
        }

        return transfer;
    }

    /// <summary>
    /// Returns current non-expired transfers.
    /// </summary>
    public IReadOnlyList<WebBulkTransfer> List()
    {
        RemoveExpired();
        return _transfers.Values.OrderByDescending(transfer => transfer.CreatedAt).ToArray();
    }

    /// <summary>
    /// Returns one transfer.
    /// </summary>
    public WebBulkTransfer Get(string transferId)
    {
        RemoveExpired();
        return _transfers.TryGetValue(transferId, out var transfer)
            ? transfer
            : throw new KeyNotFoundException("Bulk transfer was not found.");
    }

    /// <summary>
    /// Adds one file to a draft.
    /// </summary>
    public WebBulkTransfer AddFile(string transferId, WebBulkTransferItem item)
    {
        while (true)
        {
            var current = Get(transferId);
            if (current.State != WebBulkTransferState.Draft)
            {
                throw new InvalidOperationException("Files can only be added to a draft bulk transfer.");
            }

            if (current.Files.Count >= MaximumFiles)
            {
                throw new InvalidOperationException($"A bulk transfer accepts at most {MaximumFiles} files.");
            }

            if (item.Size < 0 || current.Files.Sum(file => file.Size) > MaximumTotalBytes - item.Size)
            {
                throw new InvalidOperationException($"A bulk transfer accepts at most {MaximumTotalBytes} bytes.");
            }

            if (current.Files.Any(file => string.Equals(file.RemotePath, item.RemotePath, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("The remote path is already registered in this bulk transfer.");
            }

            var updated = current with { Files = [.. current.Files, item] };
            if (_transfers.TryUpdate(transferId, updated, current))
            {
                return updated;
            }
        }
    }

    /// <summary>
    /// Replaces one transfer state.
    /// </summary>
    public WebBulkTransfer SetState(
        string transferId,
        WebBulkTransferState expectedState,
        WebBulkTransferState state,
        string? manifestSha256 = null,
        string? error = null,
        IReadOnlyList<KelpieSSH.Application.Ssh.WebBulkTransferFileResult>? appliedFiles = null)
    {
        while (true)
        {
            var current = Get(transferId);
            if (current.State != expectedState)
            {
                throw new InvalidOperationException(
                    $"Bulk transfer state must be {expectedState}, but was {current.State}.");
            }

            var updated = current with
            {
                State = state,
                ManifestSha256 = manifestSha256 ?? current.ManifestSha256,
                Error = error,
                AppliedFiles = appliedFiles ?? current.AppliedFiles,
            };
            if (_transfers.TryUpdate(transferId, updated, current))
            {
                return updated;
            }
        }
    }

    /// <summary>
    /// Cancels and removes one draft.
    /// </summary>
    public void Cancel(string transferId)
    {
        var transfer = Get(transferId);
        if (transfer.State is WebBulkTransferState.Executing or WebBulkTransferState.Applied)
        {
            throw new InvalidOperationException("An executing or applied bulk transfer cannot be cancelled.");
        }

        _transfers.TryRemove(transferId, out _);
    }

    private void RemoveExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var transfer in _transfers.Values)
        {
            if (transfer.ExpiresAt <= now && transfer.State != WebBulkTransferState.Executing)
            {
                _transfers.TryRemove(transfer.Id, out _);
            }
        }
    }
}

/// <summary>
/// Identifies the state of a bulk web transfer.
/// </summary>
public enum WebBulkTransferState
{
    Draft,
    Validated,
    Executing,
    Applied,
    Committed,
    RolledBack,
    Failed,
}

/// <summary>
/// Represents one local-to-remote file mapping.
/// </summary>
public sealed record WebBulkTransferItem(
    string LocalPath,
    string RemotePath,
    long Size,
    string Sha256,
    string ContentType,
    string? Owner,
    string? Mode);

/// <summary>
/// Represents one bulk web transfer draft.
/// </summary>
public sealed record WebBulkTransfer(
    string Id,
    string ProfileName,
    string SiteKey,
    WebBulkTransferState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<WebBulkTransferItem> Files,
    IReadOnlyList<KelpieSSH.Application.Ssh.WebBulkTransferFileResult> AppliedFiles,
    string? ManifestSha256 = null,
    string? Error = null);
