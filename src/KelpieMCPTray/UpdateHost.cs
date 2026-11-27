using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using KelpieSSH.Application.Management;

namespace KelpieMCPTray;

internal static class UpdateHost
{
    internal static void Start(TrayEnvironment environment, string installer, string hash, long size)
    {
        var helper = Path.Combine(Path.GetDirectoryName(installer)!, "host");
        Directory.CreateDirectory(helper);
        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
        {
            var extension = Path.GetExtension(file);
            if (extension is ".dll" or ".exe" || file.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase)) File.Copy(file, Path.Combine(helper, Path.GetFileName(file)), overwrite: false);
        }
        var info = new ProcessStartInfo(Path.Combine(helper, "KelpieMCPTray.exe")) { UseShellExecute = false, CreateNoWindow = true };
        AddArguments(info, environment, installer, hash, size, "--install-update");
        using var coordinator = Process.Start(info) ?? throw new IOException("Could not start the update coordinator.");
    }

    private static void AddArguments(ProcessStartInfo info, TrayEnvironment environment, string installer, string hash, long size, string mode)
    {
        foreach (var item in new[] { mode, "--install-root", environment.InstallRoot, "--package", installer, "--sha256", hash, "--size", size.ToString(CultureInfo.InvariantCulture) }) info.ArgumentList.Add(item);
    }

    internal static async Task<int> RunAsync(TrayEnvironment environment, string[] args)
    {
        var installer = Program.Argument(args, "--package") ?? throw new ArgumentException("Installer missing.");
        var hash = Program.Argument(args, "--sha256") ?? throw new ArgumentException("Installer digest missing.");
        if (!long.TryParse(Program.Argument(args, "--size"), NumberStyles.None, CultureInfo.InvariantCulture, out var size)) throw new ArgumentException("Installer size missing.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        await ReleaseUpdater.VerifyAsync(installer, hash, size, timeout.Token);
        if (args.Contains("--install-update-elevated")) return await InstallAsync(environment, installer, hash, size, timeout.Token);
        // This original-user coordinator remains outside the directory being replaced.
        // Its elevated child performs service control and installation; restart UI here.
        var exitCode = 1;
        try
        {
            await Task.Delay(1500, timeout.Token);
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            AddArguments(info, environment, installer, hash, size, "--install-update-elevated");
            using var process = Process.Start(info) ?? throw new IOException("Could not start the update worker.");
            await process.WaitForExitAsync(timeout.Token);
            exitCode = process.ExitCode;
        }
        finally
        {
            File.WriteAllText(Path.Combine(environment.UserDirectory, "last-update.json"), JsonSerializer.Serialize(new { exitCode, completedAt = DateTimeOffset.UtcNow, rebootRequired = exitCode == 3010 }));
            var restart = new ProcessStartInfo(environment.TrayExecutable) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var item in new[] { "--install-root", environment.InstallRoot, "--skip-update" }) restart.ArgumentList.Add(item);
            if (exitCode is not (0 or 3010)) { restart.ArgumentList.Add("--skip-server"); restart.ArgumentList.Add("--settings"); }
            Process.Start(restart)?.Dispose();
        }
        return exitCode;
    }

    private static async Task<int> InstallAsync(TrayEnvironment environment, string installer, string hash, long size, CancellationToken ct)
    {
        // Hold a non-writable shared-read handle through verification and msiexec.
        using var fileLock = new FileStream(installer, FileMode.Open, FileAccess.Read, FileShare.Read);
        await ReleaseUpdater.VerifyAsync(installer, hash, size, ct);
        var status = await environment.ControlAsync("status");
        if (status.ExitCode != 0 || !status.Output.Contains("KelpieMCPServer:", StringComparison.Ordinal))
            throw new InvalidOperationException("Server status could not be established; update was not started.");
        var wasRunning = status.Output.Contains("KelpieMCPServer: running", StringComparison.Ordinal);
        return await InstallerWorkflow.RunAsync(wasRunning,
            async () =>
            {
                var stop = await environment.ControlAsync("stop");
                if (stop.ExitCode != 0) throw new InvalidOperationException("Server shutdown was denied; update was not started.");
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (true)
                {
                    var current = await environment.ControlAsync("status");
                    if (current.ExitCode != 0 || !current.Output.Contains("KelpieMCPServer:", StringComparison.Ordinal))
                        throw new InvalidOperationException("Server shutdown could not be verified; update was not started.");
                    if (!current.Output.Contains("KelpieMCPServer: running", StringComparison.Ordinal)) break;
                    if (DateTime.UtcNow > deadline) throw new TimeoutException("Server shutdown timed out.");
                    await Task.Delay(500, ct);
                }
            },
            async () =>
            {
                try
                {
                    var result = await TrayEnvironment.RunAsync(Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                        new[] { "/i", installer, "/passive", "/norestart", "REBOOT=ReallySuppress", "INSTALLFOLDER=" + environment.InstallRoot },
                        TimeSpan.FromMinutes(10), killOnTimeout: false);
                    return result.ExitCode;
                }
                catch (TimeoutException ex) { throw new InstallerOutcomeUnknownException("Installer completion is unknown. Do not retry until Windows Installer has finished.", ex); }
            },
            async () =>
            {
                var result = await environment.ControlAsync("start", "--no-tray");
                if (result.ExitCode != 0) throw new InvalidOperationException("The server could not restart after the update. Check its management log.");
            });
    }
}

internal sealed class UpdateContext : ApplicationContext
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    internal UpdateContext(TrayEnvironment environment, string[] args)
    {
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            try { Environment.ExitCode = await UpdateHost.RunAsync(environment, args); }
            catch (Exception e) { environment.Report(e); Environment.ExitCode = 1; }
            finally { ExitThread(); }
        };
        timer.Start();
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}
