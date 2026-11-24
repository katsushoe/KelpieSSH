using System.Security.Principal;
using KelpieSSH.Application.Management;

namespace KelpieMCPTray;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var root = Argument(args, "--install-root") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        var environment = new TrayEnvironment(root);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => environment.Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => environment.Log("Unhandled: " + (e.ExceptionObject as Exception)?.GetType().Name);
        TaskScheduler.UnobservedTaskException += (_, e) => { environment.Log("Unobserved: " + e.Exception.GetType().Name); e.SetObserved(); };
        try
        {
            if (args.Contains("--unregister-logon")) { environment.SetLogon(false); return; }
            if (args.Contains("--install-update") || args.Contains("--install-update-elevated"))
            {
                using var context = new UpdateContext(environment, args);
                Application.Run(context);
                return;
            }
            if (args.Contains("--render-settings"))
            {
                using var form = new SettingsForm(environment, TrayPreferences.Load(environment.PreferencesPath));
                form.CreateControl();
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(0, 0, form.Width, form.Height));
                image.Save(Argument(args, "--render-settings") ?? throw new ArgumentException("Image path missing."));
                form.Close();
                return;
            }
            using var user = WindowsIdentity.GetCurrent();
            var instanceName = "Local\\KelpieMCPTray-" + user.User!.Value;
            using var settingsSignal = new EventWaitHandle(false, EventResetMode.AutoReset, instanceName + "-Settings");
            using var mutex = new Mutex(true, instanceName, out var first);
            if (!first)
            {
                if (args.Contains("--settings")) settingsSignal.Set();
                return;
            }

            using var tray = new TrayContext(environment, settingsSignal, args.Contains("--settings"),
                args.Contains("--skip-update"), args.Contains("--skip-server"));
            Application.Run(tray);
        }
        catch (Exception ex) { environment.Report(ex); Environment.ExitCode = 1; }
    }

    internal static string? Argument(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
