using System.Collections.Concurrent;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;

namespace KelpieMCPServer;

/// <summary>
/// Keeps interactive SSH terminal sessions and their rendered screen buffers for MCP tools.
/// </summary>
public sealed class SshTerminalSessionManager : IAsyncDisposable
{
    private const int DefaultColumns = 120;
    private const int DefaultRows = 40;
    private const int DefaultPixelWidth = 1200;
    private const int DefaultPixelHeight = 800;
    private static readonly TimeSpan SendOutputTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OutputIdleDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ReadInterval = TimeSpan.FromMilliseconds(50);

    private readonly ISshConnectionProfileCatalog _profileCatalog;
    private readonly ISshPasswordProvider _passwordProvider;
    private readonly ConcurrentDictionary<string, ManagedTerminalSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="SshTerminalSessionManager"/> class.
    /// </summary>
    /// <param name="profileCatalog">The SSH profile catalog.</param>
    /// <param name="passwordProvider">The SSH password provider.</param>
    public SshTerminalSessionManager(
        ISshConnectionProfileCatalog profileCatalog,
        ISshPasswordProvider passwordProvider)
    {
        _profileCatalog = profileCatalog;
        _passwordProvider = passwordProvider;
    }

    /// <summary>
    /// Opens a terminal session and returns the initial rendered screen snapshot.
    /// </summary>
    /// <param name="profileName">The SSH profile name.</param>
    /// <param name="columns">The terminal columns.</param>
    /// <param name="rows">The terminal rows.</param>
    /// <param name="pixelWidth">The terminal pixel width.</param>
    /// <param name="pixelHeight">The terminal pixel height.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened terminal snapshot.</returns>
    public async Task<SshTerminalSnapshotResult> OpenAsync(
        string profileName,
        int columns = DefaultColumns,
        int rows = DefaultRows,
        int pixelWidth = DefaultPixelWidth,
        int pixelHeight = DefaultPixelHeight,
        CancellationToken cancellationToken = default)
    {
        var profile = ResolveProfile(profileName);
        var safeColumns = Math.Clamp(columns, 20, 300);
        var safeRows = Math.Clamp(rows, 5, 100);
        var safePixelWidth = Math.Max(1, pixelWidth);
        var safePixelHeight = Math.Max(1, pixelHeight);
        var handle = CreateHandle();
        var session = new SshNetInteractiveShellSession(profile, _passwordProvider);
        var screen = new TerminalScreenBuffer(safeColumns, safeRows);

        try
        {
            var initialOutput = await session.ConnectAsync(
                safeColumns,
                safeRows,
                safePixelWidth,
                safePixelHeight,
                cancellationToken);
            screen.Write(initialOutput);
            var managed = new ManagedTerminalSession(
                handle,
                profile.Name,
                session,
                screen,
                DateTimeOffset.UtcNow);
            _sessions[handle] = managed;
            KpLog.Info($"MCP terminal session opened. profile={profile.Name}, handle={handle}");
            return managed.CreateSnapshot(initialOutput);
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Sends raw input to an open terminal session and returns the updated screen snapshot.
    /// </summary>
    /// <param name="handle">The terminal session handle.</param>
    /// <param name="input">The raw terminal input.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated terminal snapshot.</returns>
    public async Task<SshTerminalSnapshotResult> SendAsync(
        string handle,
        string input,
        CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(handle);
        if (string.IsNullOrEmpty(input))
        {
            return await SnapshotAsync(handle, cancellationToken);
        }

        await session.Shell.WriteAsync(input, cancellationToken);
        var output = await ReadAndApplyOutputUntilIdleAsync(session, cancellationToken);
        return session.CreateSnapshot(output);
    }

    /// <summary>
    /// Reads available output and returns the current terminal screen snapshot.
    /// </summary>
    /// <param name="handle">The terminal session handle.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The current terminal snapshot.</returns>
    public async Task<SshTerminalSnapshotResult> SnapshotAsync(
        string handle,
        CancellationToken cancellationToken = default)
    {
        var session = ResolveSession(handle);
        var output = await ReadAndApplyAvailableOutputAsync(session, cancellationToken);
        return session.CreateSnapshot(output);
    }

    /// <summary>
    /// Closes a terminal session.
    /// </summary>
    /// <param name="handle">The terminal session handle.</param>
    /// <returns>The close result.</returns>
    public async Task<SshTerminalCloseResult> CloseAsync(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            throw new InvalidOperationException("SSH terminal session handle is required.");
        }

        if (!_sessions.TryRemove(handle, out var session))
        {
            return new SshTerminalCloseResult(handle, string.Empty, false, "session-not-found");
        }

        await session.Shell.DisposeAsync();
        KpLog.Info($"MCP terminal session closed. profile={session.ProfileName}, handle={handle}");
        return new SshTerminalCloseResult(handle, session.ProfileName, true, string.Empty);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var handle in _sessions.Keys.ToArray())
        {
            await CloseAsync(handle);
        }
    }

    private async Task<string> ReadAndApplyOutputUntilIdleAsync(
        ManagedTerminalSession session,
        CancellationToken cancellationToken)
    {
        var output = new System.Text.StringBuilder();
        var started = DateTimeOffset.UtcNow;
        var lastDataAt = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < SendOutputTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentOutput = await ReadAndApplyAvailableOutputAsync(session, cancellationToken);
            if (!string.IsNullOrEmpty(currentOutput))
            {
                output.Append(currentOutput);
                lastDataAt = DateTimeOffset.UtcNow;
                continue;
            }

            if (output.Length > 0 && DateTimeOffset.UtcNow - lastDataAt >= OutputIdleDelay)
            {
                break;
            }

            await Task.Delay(ReadInterval, cancellationToken);
        }

        return output.ToString();
    }

    private async Task<string> ReadAndApplyAvailableOutputAsync(
        ManagedTerminalSession session,
        CancellationToken cancellationToken)
    {
        var output = await session.Shell.ReadAvailableOutputAsync(cancellationToken);
        session.Screen.Write(output);
        return output;
    }

    private SshConnectionProfile ResolveProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new InvalidOperationException("SSH profile name is required.");
        }

        if (_profileCatalog.TryGet(profileName, out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException($"SSH profile was not found: {profileName}");
    }

    private ManagedTerminalSession ResolveSession(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            throw new InvalidOperationException("SSH terminal session handle is required.");
        }

        if (_sessions.TryGetValue(handle, out var session))
        {
            return session;
        }

        throw new InvalidOperationException($"SSH terminal session was not found: {handle}");
    }

    private static string CreateHandle()
    {
        return "term-" + Guid.NewGuid().ToString("N")[..12];
    }

    private sealed record ManagedTerminalSession(
        string Handle,
        string ProfileName,
        SshNetInteractiveShellSession Shell,
        TerminalScreenBuffer Screen,
        DateTimeOffset StartedAtUtc)
    {
        public SshTerminalSnapshotResult CreateSnapshot(string rawOutput)
        {
            var snapshot = Screen.Capture();
            return new SshTerminalSnapshotResult(
                Handle,
                ProfileName,
                snapshot.Columns,
                snapshot.Rows,
                snapshot.CursorRow,
                snapshot.CursorColumn,
                snapshot.Lines,
                string.Join(Environment.NewLine, snapshot.Lines),
                rawOutput,
                Shell.IsConnected,
                StartedAtUtc,
                DateTimeOffset.UtcNow);
        }
    }
}

/// <summary>
/// Represents the current terminal screen returned by MCP terminal tools.
/// </summary>
public sealed record SshTerminalSnapshotResult(
    string Handle,
    string ProfileName,
    int Columns,
    int Rows,
    int CursorRow,
    int CursorColumn,
    string[] Lines,
    string Text,
    string RawOutput,
    bool Connected,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CapturedAtUtc);

/// <summary>
/// Represents the result of closing a terminal session.
/// </summary>
public sealed record SshTerminalCloseResult(
    string Handle,
    string ProfileName,
    bool Closed,
    string Error);
