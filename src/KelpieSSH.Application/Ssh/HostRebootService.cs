using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Ssh;

/// <summary>Requests a single normal reboot and observes it without ever redispatching.</summary>
public sealed class HostRebootService(ISshCommandRunner runner, IHostRebootStore store, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private static readonly Regex Unit = new("^[A-Za-z0-9_][A-Za-z0-9_@.:-]{0,127}$", RegexOptions.CultureInvariant);
    private static readonly Regex Kernel = new("^[A-Za-z0-9_.+~-]{1,128}$", RegexOptions.CultureInvariant);

    /// <summary>Whether the dedicated operation is allowed by the selected profile.</summary>
    public static bool IsAllowed(SshConnectionProfile p) => p.HostReboot.Enabled
        && p.Mode is KelpiePolicyMode.Maintenance or KelpiePolicyMode.Expert
        && !string.Equals(p.UserName, "root", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(p.HostKeyFingerprintSha256)
        && OsFamilyAliasResolver.Resolve(p.OsFamily) is "debian" or "rhel";

    /// <summary>Reads current identity and kernel; stores a ten-minute approval preview.</summary>
    public async Task<HostRebootOperation> PreviewAsync(SshConnectionProfile p, CancellationToken ct = default)
    {
        Validate(p);
        var snapshot = await Snapshot(p, TimeSpan.FromSeconds(10), ct);
        var op = new HostRebootOperation(Guid.NewGuid().ToString("N"), Target(p), Binding(p), p.Name,
            p.Host, p.Port, p.UserName, p.HostKeyFingerprintSha256!, snapshot.BootId, snapshot.Kernel,
            p.HostReboot, _clock.GetUtcNow());
        store.Save(op);
        return op;
    }

    /// <summary>Persists dispatch intent before sending exactly one normal reboot command.</summary>
    public async Task<HostRebootOperation> RequestAsync(SshConnectionProfile p, string operationId,
        string confirmation, CancellationToken ct = default)
    {
        Validate(p);
        using var guard = store.Acquire(Target(p));
        var op = Load(p, operationId);
        if (!string.Equals(confirmation, op.Confirmation, StringComparison.Ordinal))
            return Reject(op, "ApprovalRequired");
        if (op.State != "Preview") return op; // Idempotent, including uncertain/crashed dispatch.
        if (!MatchesPolicy(p, op)) return Reject(op, "ProfileChanged");
        if (_clock.GetUtcNow() - op.CreatedAt > TimeSpan.FromMinutes(10))
            return Reject(op, "PreviewExpired");
        if (store.HasUnresolved(op.TargetKey, op.OperationId)) return Reject(op, "OperationInProgress");
        var current = await Snapshot(p, TimeSpan.FromSeconds(10), ct);
        if (current.BootId != op.BeforeBootId || current.Kernel != op.BeforeKernel)
            return Reject(op, "PreviewStale");
        ct.ThrowIfCancellationRequested();
        op = op with { State = "Dispatching", DispatchedAt = _clock.GetUtcNow(), Error = null };
        store.Save(op);
        try
        {
            var paths = p.HostReboot.Paths;
            var result = await Run(p, "host_reboot", $"{Quote(paths.Sudo)} -n {Quote(paths.Systemctl)} reboot", TimeSpan.FromSeconds(10), ct);
            op = result.TimedOut || result.ExitCode < 0
                ? op with { State = "Unknown", Error = "DispatchUnknown" }
                : result.ExitCode == 0
                    ? op with { State = "Accepted" }
                    : op with { State = "Failed", DispatchRejected = true, Error = result.StandardError.Contains("sudo:", StringComparison.Ordinal)
                        ? "PermissionDenied" : "RequestRejected" };
        }
        catch (SshConnectionFailureException ex)
        {
            op = ex.FailureKind is SshConnectionFailureKind.HostKeyMismatch or SshConnectionFailureKind.AuthenticationFailed
                ? op with { State = "Failed", DispatchRejected = true, Error = ex.FailureKind.ToString() }
                : op with { State = "Unknown", Error = "DispatchUnknown" };
        }
        catch (Exception) // A lost response, cancellation or process interruption must never enable replay.
        {
            op = op with { State = "Unknown", Error = "DispatchUnknown" };
        }
        store.Save(op);
        return op;
    }

    /// <summary>Returns local status or polls until the original bounded deadline. Never sends reboot.</summary>
    public async Task<HostRebootOperation> InspectAsync(SshConnectionProfile p, string operationId,
        bool wait = false, CancellationToken ct = default)
    {
        Validate(p);
        using var guard = store.Acquire(Target(p));
        var op = Load(p, operationId);
        if (op.DispatchedAt is null || op.State == "Verified" || op.DispatchRejected) return op;
        if (!MatchesPolicy(p, op)) return Reject(op, "ProfileChanged");
        var deadline = op.DispatchedAt.Value.AddSeconds(op.Checks.TimeoutSeconds);
        do
        {
            ct.ThrowIfCancellationRequested();
            var remaining = deadline - _clock.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
                return Save(op with { State = op.RebootObserved ? "Failed" : "Unknown",
                    Error = HealthError(op) ?? "ReconnectTimeout" });
            try
            {
                using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
                budget.CancelAfter(remaining);
                var snapshot = await Snapshot(p, Min(remaining, TimeSpan.FromSeconds(10)), budget.Token);
                op = op with { BootId = snapshot.BootId, Kernel = snapshot.Kernel, Reconnected = true };
                if (snapshot.BootId != op.BeforeBootId)
                {
                    op = Save(op with { RebootObserved = true, State = "RebootObserved", Error = HealthError(op) });
                    if (op.Checks.ExpectedKernel.Length > 0 && snapshot.Kernel != op.Checks.ExpectedKernel)
                        return Save(op with { State = "Failed", Error = "KernelMismatch" });
                    op = await VerifyHealthAsync(p, op, snapshot, deadline, budget.Token);
                    Save(op);
                    if (op.State == "Verified" || !wait) return op;
                }
                else op = Save(op with { State = "Reconnected", Error = "RebootNotObserved" });
            }
            catch (SshConnectionFailureException ex)
            {
                if (ex.FailureKind is SshConnectionFailureKind.HostKeyMismatch or SshConnectionFailureKind.AuthenticationFailed)
                    return Save(op with { State = "Failed", Error = ex.FailureKind.ToString() });
                op = store.Read(operationId);
                op = Save(op with { State = op.RebootObserved ? "Reconnected" : "Unknown",
                    Error = HealthError(op) ?? "ReconnectPending" });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                op = store.Read(operationId);
                return Save(op with { State = op.RebootObserved ? "Failed" : "Unknown",
                    Error = HealthError(op) ?? "ReconnectTimeout" });
            }
            if (!wait) return op;
            var delay = Min(deadline - _clock.GetUtcNow(), TimeSpan.FromSeconds(op.Checks.PollSeconds));
            if (delay > TimeSpan.Zero) await Task.Delay(delay, _clock, ct);
        } while (true);
    }

    private async Task<HostRebootOperation> VerifyHealthAsync(SshConnectionProfile p, HostRebootOperation op,
        (string BootId, string Kernel) snapshot, DateTimeOffset deadline, CancellationToken ct)
    {
        var failedServices = new List<string>(op.FailedServices ?? []);
        var failedHttp = new List<string>(op.FailedHttpUrls ?? []);
        var paths = op.Checks.Paths;
        foreach (var unit in op.Checks.Services)
        {
            var result = await Run(p, "host_reboot_service_check", Quote(paths.Systemctl) + " is-active --quiet -- " + Quote(unit),
                Min(deadline - _clock.GetUtcNow(), TimeSpan.FromSeconds(10)), ct);
            if (result.ExitCode != 0 || result.TimedOut)
            {
                if (!failedServices.Contains(unit)) failedServices.Add(unit);
            }
            else failedServices.Remove(unit);
            op = Save(op with { FailedServices = failedServices.ToArray(), FailedHttpUrls = failedHttp.ToArray(),
                Error = failedServices.Count > 0 ? "ServiceStopped" : failedHttp.Count > 0 ? "HttpFailed" : null });
        }
        foreach (var url in op.Checks.HttpUrls)
        {
            var command = Quote(paths.Curl) + " --silent --output " + Quote(paths.NullDevice)
                + " --write-out '%{http_code}' --max-time 5 --proto '=http,https' -- " + Quote(url);
            var result = await Run(p, "host_reboot_http_check", command,
                Min(deadline - _clock.GetUtcNow(), TimeSpan.FromSeconds(6)), ct);
            if (result.ExitCode != 0 || result.TimedOut || !int.TryParse(result.StandardOutput.Trim(), out var code) || code is < 200 or >= 300)
            {
                if (!failedHttp.Contains(url)) failedHttp.Add(url);
            }
            else failedHttp.Remove(url);
            op = Save(op with { FailedServices = failedServices.ToArray(), FailedHttpUrls = failedHttp.ToArray(),
                Error = failedServices.Count > 0 ? "ServiceStopped" : failedHttp.Count > 0 ? "HttpFailed" : null });
        }
        var stableBoot = true;
        if (failedServices.Count + failedHttp.Count == 0)
        {
            var finalSnapshot = await Snapshot(p, Min(deadline - _clock.GetUtcNow(), TimeSpan.FromSeconds(10)), ct);
            stableBoot = finalSnapshot == snapshot;
        }
        ct.ThrowIfCancellationRequested();
        return op with
        {
            State = failedServices.Count + failedHttp.Count == 0 && stableBoot ? "Verified" : "Reconnected",
            Error = failedServices.Count > 0 ? "ServiceStopped" : failedHttp.Count > 0 ? "HttpFailed"
                : !stableBoot ? "BootChangedDuringVerification" : null,
            FailedServices = failedServices.ToArray(), FailedHttpUrls = failedHttp.ToArray(),
        };
    }

    private static string? HealthError(HostRebootOperation op) => !op.RebootObserved ? null
        : op.FailedServices is { Length: > 0 } ? "ServiceStopped"
        : op.FailedHttpUrls is { Length: > 0 } ? "HttpFailed" : null;

    private HostRebootOperation Save(HostRebootOperation op) { store.Save(op); return op; }
    private HostRebootOperation Reject(HostRebootOperation op, string error)
    {
        store.Audit(op, error);
        return op with { Error = error };
    }
    private HostRebootOperation Load(SshConnectionProfile p, string id)
    {
        var op = store.Read(id);
        if (op.TargetKey != Target(p) || op.ProfileName != p.Name || op.UserName != p.UserName
            || !string.Equals(op.Host, p.Host, StringComparison.OrdinalIgnoreCase) || op.Port != p.Port
            || op.HostKeyFingerprint != p.HostKeyFingerprintSha256)
            throw new InvalidOperationException("Reboot operation does not match the current profile and pinned host key.");
        return op;
    }
    private static void Validate(SshConnectionProfile p)
    {
        if (!IsAllowed(p)) throw new KelpiePolicyError("Host reboot requires an enabled profile, Maintenance/Expert mode, a non-root user, a pinned host key and a supported Linux OS.");
        ValidateConfiguration(p.HostReboot);
    }
    /// <summary>Validates administrator-owned settings when a profile is loaded.</summary>
    public static void ValidateConfiguration(HostRebootOptions c)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.TimeoutSeconds is < 10 or > 600 || c.PollSeconds is < 1 or > 30
            || c.Services is null || c.HttpUrls is null || c.Services.Length > 32 || c.HttpUrls.Length > 16
            || c.ExpectedKernel is null || (c.ExpectedKernel.Length > 0 && !Kernel.IsMatch(c.ExpectedKernel))
            || c.Services.Any(s => s is null || !Unit.IsMatch(s))
            || c.HttpUrls.Any(s => s is null || s.Length > 2048 || s.Any(char.IsControl) || !Uri.TryCreate(s, UriKind.Absolute, out var u)
                || u.Scheme is not ("http" or "https") || u.UserInfo.Length > 0 || u.Fragment.Length > 0))
            throw new InvalidOperationException("Invalid HostReboot settings.");
        if (c.Paths is null) throw new InvalidOperationException("HostReboot.Paths is required.");
        ValidatePath(c.Paths.Sudo, "sudo", c.Enabled);
        ValidatePath(c.Paths.Systemctl, "systemctl", c.Enabled);
        ValidatePath(c.Paths.Cat, "cat", c.Enabled);
        ValidatePath(c.Paths.Uname, "uname", c.Enabled);
        ValidatePath(c.Paths.BootId, "boot_id", c.Enabled);
        ValidatePath(c.Paths.Curl, "curl", c.Enabled && c.HttpUrls.Length > 0);
        ValidatePath(c.Paths.NullDevice, "null", c.Enabled && c.HttpUrls.Length > 0);
    }
    private static void ValidatePath(string path, string fileName, bool required)
    {
        if (path == string.Empty && !required) return;
        if (path is null || path.Length > 512 || !Regex.IsMatch(path, "^/[A-Za-z0-9_./-]+$", RegexOptions.CultureInvariant)
            || path.Split('/').Any(part => part is "." or "..") || path.Split('/')[^1] != fileName)
            throw new InvalidOperationException($"HostReboot.Paths requires an absolute path ending in {fileName}.");
    }
    private async Task<(string BootId, string Kernel)> Snapshot(SshConnectionProfile p, TimeSpan timeout, CancellationToken ct)
    {
        var paths = p.HostReboot.Paths;
        var r = await Run(p, "host_reboot_snapshot", $"{Quote(paths.Cat)} {Quote(paths.BootId)} && {Quote(paths.Uname)} -r", timeout, ct);
        var lines = r.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (r.ExitCode != 0 || r.TimedOut || lines.Length != 2 || !Guid.TryParseExact(lines[0], "D", out _) || !Kernel.IsMatch(lines[1]))
            throw new SshCommandConnectionException("Reboot snapshot is unavailable or malformed.", r.TimedOut);
        return (lines[0], lines[1]);
    }
    private async Task<SshCommandResult> Run(SshConnectionProfile p, string name, string text, TimeSpan timeout, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (timeout <= TimeSpan.Zero) throw new OperationCanceledException(ct);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(timeout);
        try
        {
            return await runner.ExecuteAsync(new(p, name, text, timeout, new Dictionary<string, string>()), bounded.Token)
                .WaitAsync(timeout, ct);
        }
        catch (TimeoutException ex)
        {
            bounded.Cancel();
            throw new SshCommandConnectionException(SshConnectionFailureKind.Timeout, ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new SshCommandConnectionException(SshConnectionFailureKind.Timeout, ex);
        }
    }
    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
    private static string Quote(string s) => "'" + s.Replace("'", "'\"'\"'") + "'";
    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    private static string Target(SshConnectionProfile p)
    {
        var key = p.HostKeyFingerprintSha256!.Trim();
        if (key.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase)) key = key[7..];
        return Hash(key.TrimEnd('='));
    }
    private static string Binding(SshConnectionProfile p) => Hash(JsonSerializer.Serialize(new { p.Host, p.Port, p.UserName, p.HostKeyFingerprintSha256, p.Mode, p.HostReboot }));
    private static bool MatchesPolicy(SshConnectionProfile p, HostRebootOperation op) => op.Binding == Binding(p)
        && JsonSerializer.Serialize(op.Checks) == JsonSerializer.Serialize(p.HostReboot);
}
