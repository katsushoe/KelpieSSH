using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Stores trusted SSH profile hashes in an AES-GCM protected file.
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

    private SshProfileTrustStore(Dictionary<string, SshProfileTrustEntry> profiles)
    {
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
            throw new InvalidOperationException("SSH profile trust store path is required.");
        }

        if (!File.Exists(filePath))
        {
            return new SshProfileTrustStore(new Dictionary<string, SshProfileTrustEntry>(StringComparer.OrdinalIgnoreCase))
            {
                FileExisted = false,
            };
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<EncryptedTrustStoreEnvelope>(
                    File.ReadAllText(filePath),
                    JsonOptions)
                ?? throw new InvalidOperationException("SSH profile trust store is empty.");

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

            return new SshProfileTrustStore(profiles)
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
            throw new InvalidOperationException("SSH profile trust store could not be read or verified.", ex);
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
            throw new InvalidOperationException("SSH profile trust store path is required.");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var manifest = new TrustStoreManifest
        {
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
            Version = 1,
            Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag),
            Ciphertext = Convert.ToBase64String(ciphertext),
        };

        File.WriteAllText(filePath, JsonSerializer.Serialize(envelope, JsonOptions));
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
        _profiles[profileName] = new SshProfileTrustEntry(
            profileName,
            hashSha256,
            DateTimeOffset.UtcNow);
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

    private static byte[] CreateKey()
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(KeyMaterial));
    }

    private sealed class TrustStoreManifest
    {
        public int Version { get; init; } = 1;

        public IReadOnlyCollection<SshProfileTrustEntry> Profiles { get; init; } = [];
    }

    private sealed class EncryptedTrustStoreEnvelope
    {
        public int Version { get; init; }

        public string Nonce { get; init; } = string.Empty;

        public string Tag { get; init; } = string.Empty;

        public string Ciphertext { get; init; } = string.Empty;
    }
}

/// <summary>
/// Represents one trusted SSH profile hash entry.
/// </summary>
/// <param name="Name">The profile name.</param>
/// <param name="HashSha256">The trusted profile file SHA-256 hash.</param>
/// <param name="TrustedAtUtc">The time when the hash was trusted.</param>
public sealed record SshProfileTrustEntry(
    string Name,
    string HashSha256,
    DateTimeOffset TrustedAtUtc);
