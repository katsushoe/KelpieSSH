using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Stores trusted MCP configuration and SSH profile hashes in an AES-GCM protected file.
/// </summary>
public sealed class SshProfileTrustStore
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string KeyMaterial = "KelpieSSH.MCP.ProfileTrustStore.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Dictionary<string, SshProfileTrustEntry> _profiles;
    private SshConfigTrustEntry? _config;
    private string _creatorPathHashSha256;

    private SshProfileTrustStore(
        string creatorPathHashSha256,
        SshConfigTrustEntry? config,
        Dictionary<string, SshProfileTrustEntry> profiles)
    {
        _creatorPathHashSha256 = creatorPathHashSha256;
        _config = config;
        _profiles = profiles;
    }

    /// <summary>
    /// Gets a value indicating whether the trust store file existed when loaded.
    /// </summary>
    public bool FileExisted { get; private init; }

    /// <summary>
    /// Loads a protected trust store file.
    /// </summary>
    /// <param name="filePath">The trust store file path.</param>
    /// <returns>The loaded trust store.</returns>
    public static SshProfileTrustStore Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("MCP trust store path is required.");
        }

        if (!File.Exists(filePath))
        {
            return new SshProfileTrustStore(
                string.Empty,
                null,
                new Dictionary<string, SshProfileTrustEntry>(StringComparer.OrdinalIgnoreCase))
            {
                FileExisted = false,
            };
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<EncryptedTrustStoreEnvelope>(
                File.ReadAllText(filePath),
                JsonOptions)
                ?? throw new InvalidOperationException("MCP trust store is empty.");

            var nonce = Convert.FromBase64String(envelope.Nonce);
            var tag = Convert.FromBase64String(envelope.Tag);
            var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(CreateKey(), TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            var manifest = JsonSerializer.Deserialize<TrustStoreManifest>(
                    Encoding.UTF8.GetString(plaintext),
                    JsonOptions)
                ?? new TrustStoreManifest();

            var profiles = manifest.Profiles
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);

            return new SshProfileTrustStore(manifest.CreatorPathHashSha256, manifest.Config, profiles)
            {
                FileExisted = true,
            };
        }
        catch (Exception ex) when (ex is CryptographicException
            or FormatException
            or JsonException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            throw new InvalidOperationException("MCP trust store could not be read or verified.", ex);
        }
    }

    /// <summary>
    /// Saves the protected trust store file.
    /// </summary>
    /// <param name="filePath">The trust store file path.</param>
    public void Save(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("MCP trust store path is required.");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var manifest = new TrustStoreManifest
        {
            CreatorPathHashSha256 = _creatorPathHashSha256,
            Config = _config,
            Profiles = _profiles.Values
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };

        var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, JsonOptions));
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(CreateKey(), TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var envelope = new EncryptedTrustStoreEnvelope
        {
            FormatVersion = 1,
            Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag),
            Ciphertext = Convert.ToBase64String(ciphertext),
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(envelope, JsonOptions));
    }

    /// <summary>
    /// Tries to get the creator executable path hash.
    /// </summary>
    /// <param name="hashSha256">The trusted creator path SHA-256 hash.</param>
    /// <returns><c>true</c> when a hash exists.</returns>
    public bool TryGetCreatorPathHash(out string hashSha256)
    {
        hashSha256 = _creatorPathHashSha256;
        return !string.IsNullOrWhiteSpace(hashSha256);
    }

    /// <summary>
    /// Stores the creator executable path hash when it has not been set yet.
    /// </summary>
    /// <param name="hashSha256">The creator path SHA-256 hash.</param>
    public void SetCreatorPathHashIfMissing(string hashSha256)
    {
        if (string.IsNullOrWhiteSpace(_creatorPathHashSha256))
        {
            _creatorPathHashSha256 = hashSha256;
        }
    }

    /// <summary>
    /// Tries to get the trusted hash for the MCP server configuration file.
    /// </summary>
    /// <param name="hashSha256">The trusted SHA-256 hash.</param>
    /// <returns><c>true</c> when a trusted hash exists.</returns>
    public bool TryGetConfigHash(out string hashSha256)
    {
        if (_config is not null)
        {
            hashSha256 = _config.HashSha256;
            return true;
        }

        hashSha256 = string.Empty;
        return false;
    }

    /// <summary>
    /// Stores the trusted hash for the MCP server configuration file.
    /// </summary>
    /// <param name="hashSha256">The SHA-256 hash.</param>
    public void SetConfigHash(string hashSha256)
    {
        _config = new SshConfigTrustEntry(hashSha256);
    }

    /// <summary>
    /// Tries to get the trusted hash for one profile.
    /// </summary>
    /// <param name="profileName">The profile name.</param>
    /// <param name="hashSha256">The trusted SHA-256 hash.</param>
    /// <returns><c>true</c> when a trusted hash exists.</returns>
    public bool TryGetHash(string profileName, out string hashSha256)
    {
        if (_profiles.TryGetValue(profileName, out var entry))
        {
            hashSha256 = entry.HashSha256;
            return true;
        }

        hashSha256 = string.Empty;
        return false;
    }

    /// <summary>
    /// Stores the trusted hash for one profile.
    /// </summary>
    /// <param name="profileName">The profile name.</param>
    /// <param name="hashSha256">The SHA-256 hash.</param>
    public void SetHash(string profileName, string hashSha256)
    {
        _profiles[profileName] = new SshProfileTrustEntry(profileName, hashSha256);
    }

    /// <summary>
    /// Removes the trusted hash for one profile.
    /// </summary>
    /// <param name="profileName">The profile name.</param>
    /// <returns><c>true</c> when the profile was removed.</returns>
    public bool RemoveHash(string profileName)
    {
        return _profiles.Remove(profileName);
    }

    /// <summary>
    /// Computes a SHA-256 hash for a profile file.
    /// </summary>
    /// <param name="filePath">The profile file path.</param>
    /// <returns>The lowercase hexadecimal SHA-256 hash.</returns>
    public static string ComputeFileHash(string filePath)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath))).ToLowerInvariant();
    }

    /// <summary>
    /// Computes a SHA-256 hash for a normalized local path.
    /// </summary>
    /// <param name="path">The local path.</param>
    /// <returns>The lowercase hexadecimal SHA-256 hash.</returns>
    public static string ComputePathHash(string path)
    {
        var normalizedPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (OperatingSystem.IsWindows())
        {
            normalizedPath = normalizedPath.ToUpperInvariant();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath))).ToLowerInvariant();
    }

    private static byte[] CreateKey()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(KeyMaterial));
    }

    private sealed class TrustStoreManifest
    {
        public int FormatVersion { get; init; } = 2;

        public string CreatorPathHashSha256 { get; init; } = string.Empty;

        public SshConfigTrustEntry? Config { get; init; }

        public IReadOnlyCollection<SshProfileTrustEntry> Profiles { get; init; } = [];
    }

    private sealed class EncryptedTrustStoreEnvelope
    {
        public int FormatVersion { get; init; }

        public string Nonce { get; init; } = string.Empty;

        public string Tag { get; init; } = string.Empty;

        public string Ciphertext { get; init; } = string.Empty;
    }
}

/// <summary>
/// Represents the trusted MCP server configuration file hash.
/// </summary>
/// <param name="HashSha256">The trusted configuration file SHA-256 hash.</param>
public sealed record SshConfigTrustEntry(string HashSha256);

/// <summary>
/// Represents one trusted SSH profile hash entry.
/// </summary>
/// <param name="Name">The profile name.</param>
/// <param name="HashSha256">The trusted profile file SHA-256 hash.</param>
public sealed record SshProfileTrustEntry(
    string Name,
    string HashSha256);
