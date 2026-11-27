using System.Diagnostics;

namespace Kelpie.Core;

/// <summary>Launches the optional Windows notification-area manager in a user session.</summary>
public static class KelpieTrayLauncher
{
    /// <summary>Starts the installed manager only in an interactive Windows session.</summary>
    public static bool TryStart(string binDirectory, bool showSettings = false, bool optional = false)
    {
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive) return false;
        using var current = Process.GetCurrentProcess();
        if (current.SessionId == 0) return false;
        var bin = Path.TrimEndingDirectorySeparator(Path.GetFullPath(binDirectory));
        var executable = Path.Combine(bin, "KelpieMCPTray.exe");
        if (!File.Exists(executable))
        {
            if (optional) return false;
            throw new FileNotFoundException("KelpieMCPTray is not installed. Install the Windows package.", executable);
        }
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = bin };
        info.ArgumentList.Add("--install-root"); info.ArgumentList.Add(Path.GetDirectoryName(bin)!);
        if (showSettings) info.ArgumentList.Add("--settings");
        using var child = Process.Start(info) ?? throw new IOException("Could not start KelpieMCPTray.");
        return true;
    }
}
