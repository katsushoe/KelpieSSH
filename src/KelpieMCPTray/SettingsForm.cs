using KelpieSSH.Application.Management;

namespace KelpieMCPTray;

internal partial class SettingsForm : Form
{
    private readonly TrayEnvironment environment;
    private TrayPreferences preferences;
    internal SettingsForm(TrayEnvironment environment, TrayPreferences preferences)
    {
        this.environment = environment; this.preferences = preferences;
        InitializeComponent();
        endpointValue.Text = "サーバー起動後に状態から取得します";
        startupServer.Checked = preferences.AutoStartServer;
        startupUpdate.Checked = preferences.CheckUpdatesOnStartup;
        startupLogon.Checked = environment.IsLogonRegistered();
        feed.Text = preferences.ReleaseApiUrl;
        save.Click += SaveClicked;
        cancel.Click += (_, _) => Close();
        logs.Click += (_, _) => { try { environment.OpenLogs(); } catch (Exception e) { environment.Report(e); } };
        Shown += async (_, _) =>
        {
            try { var result = await environment.ControlAsync("status"); status.Text = result.Output.Split('\n').FirstOrDefault()?.Trim() ?? "状態不明";
                endpointValue.Text = result.Output.Split('\n').FirstOrDefault(line => line.StartsWith("MCP URL:", StringComparison.Ordinal))?.Trim() ?? "サーバー停止中"; }
            catch (Exception e) { environment.Log("Status: " + e.GetType().Name); status.Text = "状態を取得できませんでした"; }
        };
    }

    private void SaveClicked(object? sender, EventArgs args)
    {
        var next = preferences with { AutoStartServer = startupServer.Checked, CheckUpdatesOnStartup = startupUpdate.Checked,
            StartAtLogon = startupLogon.Checked, ReleaseApiUrl = feed.Text.Trim() };
        var oldLogon = environment.IsLogonRegistered();
        try
        {
            next.Validate();
            environment.SetLogon(next.StartAtLogon);
            try { next.Save(environment.PreferencesPath); }
            catch { environment.SetLogon(oldLogon); throw; }
            preferences = next;
            status.Text = "保存しました。起動時の設定は次回のトレイ起動から有効です。";
        }
        catch (Exception e) { environment.Report(e); }
    }
}
