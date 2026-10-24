namespace KelpieSSH.Application.Ssh;

/// <summary>Administrator-owned normal reboot policy. Disabled unless explicitly enabled.</summary>
public sealed record HostRebootOptions
{
    /// <summary>Administrator-selected executable and diagnostic paths; no environment-specific defaults.</summary>
    public HostRebootPaths Paths { get; init; } = new();
    /// <summary>Allows dedicated reboot requests in Maintenance or Expert mode.</summary>
    public bool Enabled { get; init; }
    /// <summary>Optional expected kernel after reboot; empty accepts any reported kernel.</summary>
    public string ExpectedKernel { get; init; } = string.Empty;
    /// <summary>Systemd units that must be active after reboot.</summary>
    public string[] Services { get; init; } = [];
    /// <summary>HTTP(S) endpoints checked from the remote host, without redirects.</summary>
    public string[] HttpUrls { get; init; } = [];
    /// <summary>Total verification budget, measured from dispatch, in seconds (10–600).</summary>
    public int TimeoutSeconds { get; init; } = 120;
    /// <summary>Delay between observations in seconds (1–30).</summary>
    public int PollSeconds { get; init; } = 3;
}

/// <summary>Absolute remote paths reviewed by the host administrator before enabling reboot.</summary>
public sealed record HostRebootPaths
{
    /// <summary>Path to the sudo executable.</summary>
    public string Sudo { get; init; } = string.Empty;
    /// <summary>Path to the systemctl executable.</summary>
    public string Systemctl { get; init; } = string.Empty;
    /// <summary>Path to the cat executable.</summary>
    public string Cat { get; init; } = string.Empty;
    /// <summary>Path to the uname executable.</summary>
    public string Uname { get; init; } = string.Empty;
    /// <summary>Path to curl; required only for HTTP checks.</summary>
    public string Curl { get; init; } = string.Empty;
    /// <summary>Path to the Linux boot ID file.</summary>
    public string BootId { get; init; } = string.Empty;
    /// <summary>Path to the null device; required only for HTTP checks.</summary>
    public string NullDevice { get; init; } = string.Empty;
}

/// <summary>A durable, secret-free reboot operation and its most recent observation.</summary>
public sealed record HostRebootOperation(
    string OperationId, string TargetKey, string Binding, string ProfileName,
    string Host, int Port, string UserName, string HostKeyFingerprint,
    string BeforeBootId, string BeforeKernel, HostRebootOptions Checks,
    DateTimeOffset CreatedAt, DateTimeOffset? DispatchedAt = null,
    string State = "Preview", string? Error = null, string? BootId = null,
    string? Kernel = null, bool RebootObserved = false, bool Reconnected = false,
    string[]? FailedServices = null, string[]? FailedHttpUrls = null, bool DispatchRejected = false)
{
    /// <summary>Exact approval token. The caller must obtain human approval before submitting it.</summary>
    public string Confirmation => $"host_reboot:{ProfileName}:{OperationId}";
    /// <summary>All host services and SSH sessions may be interrupted.</summary>
    public string Impact => "Normal host reboot interrupts all services and SSH sessions; configured services and HTTP endpoints are checked after reconnection.";
    /// <summary>The boot loader's next kernel is not automatically inferred.</summary>
    public string? NextKernel => null;
    /// <summary>The fixed deadline is never extended by polling or retrying inspection.</summary>
    public DateTimeOffset? Deadline => DispatchedAt?.AddSeconds(Checks.TimeoutSeconds);
}

/// <summary>Durable storage and cross-process serialization for reboot operations.</summary>
public interface IHostRebootStore
{
    /// <summary>Acquires an exclusive target lock, or throws if the target is busy.</summary>
    IDisposable Acquire(string targetKey);
    /// <summary>Reads an operation by its opaque ID.</summary>
    HostRebootOperation Read(string operationId);
    /// <summary>Atomically persists the complete operation before any remote mutation.</summary>
    void Save(HostRebootOperation operation);
    /// <summary>Finds unresolved dispatched operations for the same endpoint.</summary>
    bool HasUnresolved(string targetKey, string exceptOperationId);
    /// <summary>Records a decision without overwriting the operation state.</summary>
    void Audit(HostRebootOperation operation, string decision);
}
