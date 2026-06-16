using System.Net.Sockets;
using System.Text;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace KelpieSSH.Infrastructure.Ssh;

/// <summary>
/// Holds a persistent SSH shell session using SSH.NET ShellStream.
/// </summary>
public sealed class SshNetInteractiveShellSession : IAsyncDisposable
{
    private const string TerminalName = "xterm-256color";
    private const int DefaultColumns = 120;
    private const int DefaultRows = 40;
    private const int DefaultPixelWidth = 1200;
    private const int DefaultPixelHeight = 800;

    private static readonly TimeSpan OutputIdleDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ReadInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ShellOperationTimeout = TimeSpan.FromSeconds(3);

    private readonly SshConnectionProfile _profile;
    private readonly SshNetAuthenticationFactory _authenticationFactory;
    private readonly object _syncRoot = new();
    private SshClient? _client;
    private ShellStream? _shell;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshNetInteractiveShellSession"/> class.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="passwordProvider">The SSH password provider.</param>
    public SshNetInteractiveShellSession(
        SshConnectionProfile profile,
        ISshPasswordProvider? passwordProvider = null)
    {
        _profile = profile;
        _authenticationFactory = new SshNetAuthenticationFactory(passwordProvider ?? new NullSshPasswordProvider());
    }

    /// <summary>
    /// Connects the SSH client and opens a shell stream.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The initial shell output.</returns>
    public async Task<string> ConnectAsync(
        int columns = DefaultColumns,
        int rows = DefaultRows,
        int pixelWidth = DefaultPixelWidth,
        int pixelHeight = DefaultPixelHeight,
        CancellationToken cancellationToken = default)
    {
        _profile.Validate();

        if (string.Equals(_profile.UserName, "root", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Direct root SSH login is not allowed.");
        }

        var authenticationMethod = await _authenticationFactory.CreateAsync(_profile, cancellationToken);
        var connectionInfo = new ConnectionInfo(
            _profile.Host,
            _profile.Port,
            _profile.UserName,
            authenticationMethod)
        {
            Timeout = _profile.ConnectionTimeout,
        };

        _client = new SshClient(connectionInfo);
        await ConnectClientAsync(_client, cancellationToken);
        _shell = _client.CreateShellStream(
            TerminalName,
            (uint)Math.Max(1, columns),
            (uint)Math.Max(1, rows),
            (uint)Math.Max(1, pixelWidth),
            (uint)Math.Max(1, pixelHeight),
            4096);
        KpLog.Info($"SSH interactive shell connected. profile={_profile.Name}");
        return await ReadUntilIdleAsync(TimeSpan.FromSeconds(2), cancellationToken);
    }

    private static async Task ConnectClientAsync(
        SshClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(client.Connect, cancellationToken);
        }
        catch (SocketException ex)
        {
            throw new SshConnectionException($"SSH host is unreachable: {ex.Message}", ex);
        }
        catch (SshOperationTimeoutException ex)
        {
            throw new SshConnectionException($"SSH connection timed out: {ex.Message}", ex);
        }
        catch (SshException ex)
        {
            throw new SshConnectionException($"SSH connection failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sends one line to the remote shell and returns output received until the stream becomes idle.
    /// </summary>
    /// <param name="commandText">The command text.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The shell output.</returns>
    public async Task<string> SendLineAsync(
        string commandText,
        CancellationToken cancellationToken = default)
    {
        _ = _shell ?? throw new InvalidOperationException("SSH shell is not connected.");
        KpLog.Info($"SSH interactive shell command started. profile={_profile.Name}");

        await WriteAsync(commandText + "\n", cancellationToken);

        var output = await ReadUntilIdleAsync(TimeSpan.FromSeconds(30), cancellationToken);
        KpLog.Info($"SSH interactive shell command completed. profile={_profile.Name}");
        return output;
    }

    /// <summary>
    /// Sends raw terminal input to the remote shell.
    /// </summary>
    /// <param name="input">The raw terminal input.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the input is written.</returns>
    public async Task WriteAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var shell = _shell ?? throw new InvalidOperationException("SSH shell is not connected.");
        await RunShellOperationAsync(() =>
        {
            shell.Write(input);
            shell.Flush();
        }, cancellationToken);
    }

    /// <summary>
    /// Reads currently available terminal output without waiting for idle.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The currently available shell output.</returns>
    public async Task<string> ReadAvailableOutputAsync(CancellationToken cancellationToken = default)
    {
        var shell = _shell ?? throw new InvalidOperationException("SSH shell is not connected.");
        return await RunShellOperationAsync(() =>
        {
            return shell.DataAvailable ? shell.Read() : string.Empty;
        }, cancellationToken);
    }

    /// <summary>
    /// Changes the remote PTY window size.
    /// </summary>
    /// <param name="columns">The terminal columns.</param>
    /// <param name="rows">The terminal rows.</param>
    /// <param name="pixelWidth">The terminal pixel width.</param>
    /// <param name="pixelHeight">The terminal pixel height.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the resize request is sent.</returns>
    public async Task ChangeWindowSizeAsync(
        int columns,
        int rows,
        int pixelWidth,
        int pixelHeight,
        CancellationToken cancellationToken = default)
    {
        var shell = _shell ?? throw new InvalidOperationException("SSH shell is not connected.");
        await RunShellOperationAsync(() =>
        {
            shell.ChangeWindowSize(
                (uint)Math.Max(1, columns),
                (uint)Math.Max(1, rows),
                (uint)Math.Max(1, pixelWidth),
                (uint)Math.Max(1, pixelHeight));
        }, cancellationToken);
    }

    /// <summary>
    /// Gets a value indicating whether the SSH client is currently connected.
    /// </summary>
    public bool IsConnected => _client?.IsConnected == true && _shell is not null;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _shell?.Dispose();

        if (_client is not null)
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }

            _client.Dispose();
        }

        KpLog.Info($"SSH interactive shell disconnected. profile={_profile.Name}");
        return ValueTask.CompletedTask;
    }

    private async Task<string> ReadUntilIdleAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var shell = _shell ?? throw new InvalidOperationException("SSH shell is not connected.");
        var output = new StringBuilder();
        var started = DateTimeOffset.UtcNow;
        var lastDataAt = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - started < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string currentOutput;
            lock (_syncRoot)
            {
                currentOutput = shell.DataAvailable ? shell.Read() : string.Empty;
            }

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

    private async Task RunShellOperationAsync(
        Action action,
        CancellationToken cancellationToken)
    {
        await RunShellOperationAsync(
            () =>
            {
                action();
                return true;
            },
            cancellationToken);
    }

    private async Task<T> RunShellOperationAsync<T>(
        Func<T> action,
        CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            throw new SshConnectionException("Client not connected.");
        }

        var operation = Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_syncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsConnected)
                {
                    throw new SshConnectionException("Client not connected.");
                }

                return action();
            }
        });

        var completed = await Task.WhenAny(operation, Task.Delay(ShellOperationTimeout));
        if (completed != operation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DisconnectBrokenClient();
            throw new SshConnectionException("SSH shell operation timed out.");
        }

        return await operation.WaitAsync(cancellationToken);
    }

    private void DisconnectBrokenClient()
    {
        try
        {
            _client?.Disconnect();
        }
        catch (Exception ex)
        {
            KpLog.Debug($"SSH disconnect skipped. reason={ex.GetType().Name}");
        }
    }
}
