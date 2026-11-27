using System.Text.Json;

namespace KelpieSSH.Application.Management;

/// <summary>Non-secret, per-user management preferences.</summary>
public sealed record TrayPreferences
{
    public bool AutoStartServer { get; init; } = true;
    public bool CheckUpdatesOnStartup { get; init; } = true;
    public bool StartAtLogon { get; init; }
    public string ReleaseApiUrl { get; init; } = "https://api.github.com/repos/katsushoe/KelpieSSH/releases/latest";

    /// <summary>Validates the editable public release source.</summary>
    public void Validate()
    {
        if (!Uri.TryCreate(ReleaseApiUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("The release API must be an HTTPS URL without credentials or a fragment.");
    }

    /// <summary>Loads a bounded preferences file or initial defaults when absent.</summary>
    public static TrayPreferences Load(string path)
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Management preferences exceed the limit.");
        var value = JsonSerializer.Deserialize<TrayPreferences>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Management preferences are empty.");
        value.Validate();
        return value;
    }

    /// <summary>Atomically saves validated preferences without replacing a valid file on validation failure.</summary>
    public void Save(string path)
    {
        Validate();
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, this, new JsonSerializerOptions { WriteIndented = true });
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
