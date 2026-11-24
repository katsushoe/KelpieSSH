using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using KelpieSSH.Application.Management;

namespace KelpieMCPTray;

internal sealed class TrayEnvironment
{
    internal string InstallRoot { get; }
    internal string Identity { get; }
    internal string UserDirectory { get; }
    internal string PreferencesPath => Path.Combine(UserDirectory, "tray.json");
    internal string Cli => Path.Combine(InstallRoot, "bin", "kelpiemcp.exe");
    internal string TrayExecutable => Path.Combine(InstallRoot, "bin", "KelpieMCPTray.exe");
    internal string LogDirectory => Path.Combine(InstallRoot, "logs");

    internal TrayEnvironment(string installRoot)
    {
        // The MSI passes "[INSTALLFOLDER]." while launchers pass the plain folder; the logon value must match for both.
        InstallRoot = ManagementPaths.NormalizeInstallRoot(installRoot);
        Identity = ManagementPaths.Identity(InstallRoot);
        UserDirectory = ManagementPaths.UserDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), InstallRoot);
        Directory.CreateDirectory(UserDirectory);
    }

    internal string LogonValue => "\"" + TrayExecutable + "\" --install-root \"" + InstallRoot + "\"";
    private string LogonName => "KelpieMCPTray-" + Identity;
    internal bool IsLogonRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
        return string.Equals(key?.GetValue(LogonName) as string, LogonValue, StringComparison.Ordinal);
    }
    internal void SetLogon(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
        if (enabled) key.SetValue(LogonName, LogonValue, RegistryValueKind.String);
        else
        {
            var current = key.GetValue(LogonName) as string;
            if (current is null) { Log("Logon registration not present."); return; }
            if (!string.Equals(current, LogonValue, StringComparison.Ordinal)) { Log("Logon registration kept: value differs from this installation. Expected=" + LogonValue + " Actual=" + current); return; }
            key.DeleteValue(LogonName, throwOnMissingValue: false);
            Log("Logon registration removed.");
        }
    }

    internal Version BundleVersion()
    {
        var version = FileVersionInfo.GetVersionInfo(Path.Combine(InstallRoot, "bin", "kelpie.exe")).FileVersion;
        return Version.TryParse(version, out var parsed) ? parsed : throw new InvalidDataException("Installed bundle version is unavailable.");
    }

    internal async Task<CommandOutcome> ControlAsync(params string[] arguments)
    {
        return await RunAsync(Cli, arguments, TimeSpan.FromSeconds(35));
    }

    internal static async Task<CommandOutcome> RunAsync(string executable, IEnumerable<string> arguments, TimeSpan timeout, bool killOnTimeout = true)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException("Could not start the management command.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout);
        try { await process.WaitForExitAsync(cancellation.Token); }
        catch (OperationCanceledException) { if (killOnTimeout) process.Kill(entireProcessTree: true); throw new TimeoutException("The operation timed out; its completion may be unknown."); }
        return new(process.ExitCode, await output, await error);
    }

    internal void OpenLogs()
    {
        Directory.CreateDirectory(LogDirectory);
        Process.Start(new ProcessStartInfo(LogDirectory) { UseShellExecute = true });
    }
    internal void Log(string text)
    {
        try { File.AppendAllText(Path.Combine(UserDirectory, "management.log"), DateTimeOffset.Now.ToString("O") + " " + text + Environment.NewLine); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Trace.TraceError("Management log failed: {0}", ex.GetType().Name); }
    }
    internal void Report(Exception ex)
    {
        Log(ex.GetType().Name + ": " + ex.Message);
        MessageBox.Show("操作を完了できませんでした。\n" + ex.Message + "\n管理ログ: " + UserDirectory, "KelpieMCPServer", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
internal sealed record CommandOutcome(int ExitCode, string Output, string Error);
