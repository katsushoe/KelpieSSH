using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KelpieMCPServer;

/// <summary>
/// Holds an operating-system mutex for the configured control pipe lifetime.
/// </summary>
public sealed class ControlPipeInstanceGuard : IHostedService, IDisposable
{
    private readonly ILogger<ControlPipeInstanceGuard> _logger;
    private readonly string _mutexName;
    private Mutex? _mutex;

    /// <summary>
    /// Initializes a guard for one control pipe identity.
    /// </summary>
    public ControlPipeInstanceGuard(
        KelpieServerControlOptions options,
        ILogger<ControlPipeInstanceGuard> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _mutexName = CreateMutexName(options.PipeName);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _mutex = new Mutex(initiallyOwned: false, _mutexName, out var createdNew);
        if (createdNew)
        {
            return Task.CompletedTask;
        }

        _mutex.Dispose();
        _mutex = null;
        _logger.LogError("KelpieMCPServer control endpoint is already owned by another process.");
        throw new InvalidOperationException("KelpieMCPServer is already running for this control pipe.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Release();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Release();
    }

    internal static string CreateMutexName(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pipeName)));
        return "KelpieMCPServer.ControlPipe." + hash;
    }

    private void Release()
    {
        _mutex?.Dispose();
        _mutex = null;
    }
}
