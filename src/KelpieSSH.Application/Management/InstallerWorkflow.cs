namespace KelpieSSH.Application.Management;

/// <summary>An installer may still be running; do not restart services or repeat it automatically.</summary>
public sealed class InstallerOutcomeUnknownException(string message, Exception inner) : IOException(message, inner);

/// <summary>Orders service shutdown, one installer invocation and recovery without hiding failures.</summary>
public static class InstallerWorkflow
{
    /// <summary>Installs once, restores a stopped server for known outcomes, and preserves unknown outcomes.</summary>
    public static async Task<int> RunAsync(bool serverWasRunning, Func<Task> stopAndWait,
        Func<Task<int>> install, Func<Task> restart)
    {
        var stopped = false; var unknown = false;
        try
        {
            if (serverWasRunning) { await stopAndWait(); stopped = true; }
            var result = await install();
            if (result is not (0 or 3010)) throw new InvalidOperationException("Windows Installer failed with exit code " + result);
            return result;
        }
        catch (InstallerOutcomeUnknownException) { unknown = true; throw; }
        finally { if (stopped && !unknown) await restart(); }
    }
}
