using System.Diagnostics;
using KelpieSSH.Application.Management;

namespace KelpieMCPTray;

internal sealed class TrayContext : ApplicationContext
{
    private readonly TrayEnvironment environment;
    private readonly NotifyIcon icon;
    private readonly ContextMenuStrip menu;
    private readonly System.Windows.Forms.Timer timer;
    private readonly EventWaitHandle settingsSignal;
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private SettingsForm? settingsForm;
    private AboutForm? aboutForm;
    private bool busy;
    private bool firstTick = true;
    private readonly bool showSettings;
    private readonly bool skipUpdate;
    private readonly bool skipServer;

    internal TrayContext(TrayEnvironment environment, EventWaitHandle settingsSignal, bool showSettings, bool skipUpdate, bool skipServer)
    {
        this.environment = environment; this.showSettings = showSettings; this.skipUpdate = skipUpdate; this.skipServer = skipServer;
        menu = new ContextMenuStrip();
        menu.Items.Add("状態を確認", null, async (_, _) => await Control("status"));
        menu.Items.Add("サーバーを開始", null, async (_, _) => await Control("start", "--no-tray"));
        menu.Items.Add("サーバーを停止", null, async (_, _) => await Control("stop"));
        var administrator = new ToolStripMenuItem("管理者権限で操作");
        administrator.DropDownItems.Add("サーバーを開始", null, async (_, _) => await ElevatedControl("start"));
        administrator.DropDownItems.Add("サーバーを停止", null, async (_, _) => await ElevatedControl("stop"));
        menu.Items.Add(administrator);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("設定", null, (_, _) => OpenSettings());
        menu.Items.Add("バージョン情報", null, (_, _) => OpenAbout());
        menu.Items.Add("更新を確認", null, async (_, _) => await CheckUpdate(manual: true));
        menu.Items.Add("ログフォルダーを開く", null, (_, _) => { try { environment.OpenLogs(); } catch (Exception e) { environment.Report(e); } });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("トレイを終了（サーバーは継続）", null, (_, _) => ExitThread());
        icon = new NotifyIcon { Text = "KelpieMCPServer", Icon = SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => OpenSettings();
        this.settingsSignal = settingsSignal;
        timer = new System.Windows.Forms.Timer { Interval = 500 };
        timer.Tick += async (_, _) =>
        {
            if (settingsSignal.WaitOne(0)) OpenSettings();
            if (!firstTick) return;
            firstTick = false;
            if (showSettings) OpenSettings();
            TrayPreferences preferences;
            try { preferences = TrayPreferences.Load(environment.PreferencesPath); }
            catch (Exception e) { environment.Report(e); OpenSettings(); return; }
            if (!skipUpdate && preferences.CheckUpdatesOnStartup && await CheckUpdate(manual: false)) return;
            if (!skipServer && preferences.AutoStartServer) await Control("start", "--no-tray");
        };
        timer.Start();
    }

    private void OpenAbout()
    {
        if (aboutForm is null || aboutForm.IsDisposed)
            aboutForm = new AboutForm(environment.InstallRoot);

        if (!aboutForm.Visible) aboutForm.Show();
        aboutForm.Activate();
    }

    private void OpenSettings()
    {
        try
        {
            if (settingsForm is null || settingsForm.IsDisposed)
                settingsForm = new SettingsForm(environment, TrayPreferences.Load(environment.PreferencesPath));
            if (!settingsForm.Visible) settingsForm.Show();
            if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
        }
        catch (Exception e) { environment.Report(e); }
    }

    private async Task Control(params string[] args)
    {
        if (busy) return;
        busy = true;
        try
        {
            var result = await environment.ControlAsync(args);
            environment.Log("Control " + args[0] + " exit=" + result.ExitCode);
            icon.BalloonTipTitle = "KelpieMCPServer";
            icon.BalloonTipText = result.ExitCode == 0 ? result.Output.Trim() : "操作が拒否されました。サービス操作には管理者権限が必要な場合があります。";
            if (icon.BalloonTipText.Length > 500) icon.BalloonTipText = icon.BalloonTipText[..500];
            icon.ShowBalloonTip(5000);
        }
        catch (Exception e) { environment.Report(e); }
        finally { busy = false; }
    }

    private async Task ElevatedControl(string command)
    {
        try
        {
            var info = new ProcessStartInfo(environment.Cli) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            info.ArgumentList.Add(command);
            if (command == "start") info.ArgumentList.Add("--no-tray");
            using var process = Process.Start(info) ?? throw new IOException("Could not start administrator control.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0) throw new InvalidOperationException("The administrator command did not succeed.");
        }
        catch (Exception e) { environment.Report(e); }
    }

    private async Task<bool> CheckUpdate(bool manual)
    {
        if (busy) return false;
        busy = true;
        try
        {
            var preferences = TrayPreferences.Load(environment.PreferencesPath);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var updater = new ReleaseUpdater(client);
            var release = await updater.CheckAsync(preferences.ReleaseApiUrl, environment.BundleVersion(), timeout.Token);
            if (release is null)
            {
                if (manual) MessageBox.Show("新しい更新はありません。", "KelpieMCPServer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            icon.Text = "KelpieMCPServer: 更新を取得中";
            var directory = Path.Combine(environment.UserDirectory, "updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var installer = Path.Combine(directory, "KelpieSSH-" + release.Version + "-x64.msi");
            await updater.DownloadAsync(release, installer, timeout.Token);
            UpdateHost.Start(environment, installer, release.Sha256, release.Size);
            ExitThread();
            return true;
        }
        catch (Exception e)
        {
            environment.Log("Update failed: " + e.GetType().Name + ": " + e.Message);
            if (manual) environment.Report(e);
            else { icon.BalloonTipTitle = "KelpieMCPServer"; icon.BalloonTipText = "自動更新を完了できませんでした。サーバーは通常どおり起動します。設定と管理ログを確認してください。"; icon.ShowBalloonTip(5000); }
            return false;
        }
        finally { busy = false; if (icon.Visible) icon.Text = "KelpieMCPServer"; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Stop(); timer.Dispose(); settingsForm?.Dispose(); aboutForm?.Dispose(); icon.Visible = false; icon.Dispose(); menu.Dispose(); client.Dispose(); }
        base.Dispose(disposing);
    }
}
