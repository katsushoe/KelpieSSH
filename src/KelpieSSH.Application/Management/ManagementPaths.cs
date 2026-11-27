using System.Security.Cryptography;
using System.Text;
namespace KelpieSSH.Application.Management;
/// <summary>Separates per-user preferences for independent installation paths.</summary>
public static class ManagementPaths
{
    /// <summary>Returns one canonical spelling of an installation root, so "root", "root\" and "root\." match.</summary>
    public static string NormalizeInstallRoot(string installRoot) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));
    /// <summary>Returns an installation-specific identifier without embedding its path in registry names.</summary>
    public static string Identity(string installRoot) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant())))[..24];
    /// <summary>Locates preferences under the supplied current-user application data root.</summary>
    public static string UserDirectory(string localApplicationData, string installRoot) =>
        Path.Combine(localApplicationData, "Akatsukisoft", "KelpieSSH", Identity(installRoot));
}
