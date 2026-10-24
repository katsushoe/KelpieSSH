using System.Text.Json;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Infrastructure.Ssh;

/// <summary>Atomic local reboot journal shared by the CLI and MCP host.</summary>
public sealed class FileHostRebootStore : IHostRebootStore
{
    private readonly string _directory;
    /// <summary>Creates a store under the trusted Kelpie data directory.</summary>
    public FileHostRebootStore(string directory)
    {
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
    }
    /// <inheritdoc />
    public IDisposable Acquire(string targetKey)
    {
        if (targetKey.Length != 64 || !targetKey.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid target key.");
        try { return new FileStream(Path.Combine(_directory, targetKey + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new InvalidOperationException("Reboot target is busy. Inspect its existing operation instead of retrying dispatch.", ex); }
    }
    /// <inheritdoc />
    public HostRebootOperation Read(string operationId)
    {
        HostRebootOperation? operation;
        try { operation = JsonSerializer.Deserialize<HostRebootOperation>(File.ReadAllText(GetPath(operationId))); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid reboot journal.", ex); }
        if (operation is null || operation.Checks is null || !string.Equals(operation.OperationId, operationId, StringComparison.OrdinalIgnoreCase)
            || operation.State is not ("Preview" or "Dispatching" or "Accepted" or "Unknown" or "Reconnected" or "RebootObserved" or "Verified" or "Failed")
            || (operation.State != "Preview" && operation.DispatchedAt is null))
            throw new InvalidDataException("Invalid reboot journal.");
        HostRebootService.ValidateConfiguration(operation.Checks);
        return operation;
    }
    /// <inheritdoc />
    public void Save(HostRebootOperation operation)
    {
        var path = GetPath(operation.OperationId);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, operation);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
            Audit(operation, operation.State);
            KpLog.Info($"Host reboot journal. operation={operation.OperationId}, profile={operation.ProfileName}, state={operation.State}, error={operation.Error ?? "none"}, rebootObserved={operation.RebootObserved}");
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    /// <inheritdoc />
    public void Audit(HostRebootOperation operation, string decision)
    {
        var entry = JsonSerializer.Serialize(new { At = DateTimeOffset.UtcNow, RuntimeIdentity = Environment.UserName,
            operation.OperationId, operation.ProfileName, Decision = decision, operation.Error });
        using var stream = new FileStream(GetPath(operation.OperationId) + ".audit.jsonl", FileMode.Append, FileAccess.Write, FileShare.Read);
        var bytes = System.Text.Encoding.UTF8.GetBytes(entry + "\n");
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }
    /// <inheritdoc />
    public bool HasUnresolved(string targetKey, string exceptOperationId)
    {
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            // Fail closed on corruption: never discard a possibly dispatched request.
            var op = Read(Path.GetFileNameWithoutExtension(path));
            if (op.TargetKey == targetKey && op.OperationId != exceptOperationId && op.DispatchedAt is not null
                && op.State != "Verified" && !(op.State == "Failed" && op.RebootObserved) && !op.DispatchRejected)
                return true;
        }
        return false;
    }
    private string GetPath(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid reboot operation ID.");
        return Path.Combine(_directory, id + ".json");
    }
}
