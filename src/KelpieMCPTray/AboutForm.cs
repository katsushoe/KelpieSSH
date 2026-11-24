using System.Diagnostics;
using System.Reflection;

namespace KelpieMCPTray;

internal sealed partial class AboutForm : Form
{
    internal AboutForm(string installRoot)
    {
        InitializeComponent();

        var assembly = typeof(AboutForm).Assembly;
        var resources = new System.ComponentModel.ComponentResourceManager(typeof(AboutForm));
        var unavailable = resources.GetString("unavailable")!;
        var trayVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var serverVersion = ReadFileVersion(Path.Combine(installRoot, "bin", "mcp", "KelpieMCPServer.exe"));
        var bundleVersion = ReadFileVersion(Path.Combine(installRoot, "bin", "kelpie.exe"));

        trayValue.Text = trayVersion ?? unavailable;
        serverValue.Text = serverVersion ?? unavailable;
        bundleValue.Text = bundleVersion ?? unavailable;
        companyValue.Text = company ?? unavailable;
    }

    private static string? ReadFileVersion(string path)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(path).FileVersion;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
