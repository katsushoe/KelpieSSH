using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Loads saved SSH profiles from profile JSON files.
/// </summary>
public static class SshConnectionProfileFileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Loads all saved SSH profiles from a directory.
    /// </summary>
    /// <param name="serversDirectory">The directory that contains profile JSON files.</param>
    /// <returns>The loaded SSH connection profiles.</returns>
    public static IReadOnlyCollection<SshConnectionProfile> LoadDirectory(string serversDirectory)
    {
        if (string.IsNullOrWhiteSpace(serversDirectory) || !Directory.Exists(serversDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(serversDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
            .Select(TryLoadFile)
            .Where(profile => profile is not null)
            .Select(profile => profile!)
            .ToArray();
    }

    /// <summary>
    /// Loads all saved SSH profiles and fails when any profile file is invalid.
    /// </summary>
    /// <param name="serversDirectory">The directory that contains profile JSON files.</param>
    /// <returns>The loaded SSH connection profiles.</returns>
    public static IReadOnlyCollection<SshConnectionProfile> LoadDirectoryStrict(string serversDirectory)
    {
        if (string.IsNullOrWhiteSpace(serversDirectory) || !Directory.Exists(serversDirectory))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(serversDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
            .Select(LoadFile)
            .ToArray();
    }

    /// <summary>
    /// Loads one saved SSH profile from a profile JSON file.
    /// </summary>
    /// <param name="filePath">The profile JSON file path.</param>
    /// <returns>The loaded SSH connection profile.</returns>
    public static SshConnectionProfile LoadFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("SSH profile file path is required.");
        }

        var profileName = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new InvalidOperationException("SSH profile file name is required.");
        }

        var json = File.ReadAllText(filePath);
        var options = JsonSerializer.Deserialize<SshConnectionProfileOptions>(json, JsonOptions)
            ?? throw new InvalidOperationException($"SSH profile file is empty: {filePath}");

        options.Name = profileName;
        return options.ToProfile(Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? AppContext.BaseDirectory);
    }

    private static SshConnectionProfile? TryLoadFile(string filePath)
    {
        try
        {
            return LoadFile(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
