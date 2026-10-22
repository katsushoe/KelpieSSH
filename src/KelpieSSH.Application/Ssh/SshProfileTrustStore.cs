using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Stores trusted MCP configuration and SSH profile hashes in an AES-GCM protected file.
/// </summary>
public sealed class SshProfileTrustStore
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int DataKeySize = 32;
    private const int CurrentFormatVersion = 4;
    private const int LegacyFileKeyFormatVersion = 2;
    private const int LegacyDpapiFormatVersion = 3;
    private const string KeyProtectionDpapiCurrentUser = "dpapi-current-user";
    private const string KeyProtectionFileAcl = "file-acl";
    private const int MutexTimeoutSeconds = 30;
    private const UnixFileMode UnixKeyFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, SshProfileTrustEntry> _profiles;
    private readonly Dictionary<string, SshProfileTrustEntry> _profileUpdates = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _profileRemovals = new(StringComparer.OrdinalIgnoreCase);
    private SshConfigTrustEntry? _config;
    private bool _configChanged;
    private string _creatorPathHashSha256;
    private bool _creatorPathHashChanged;
    private byte[]? _dataKey;

    private SshProfileTrustStore(
        string creatorPathHashSha256,
        SshConfigTrustEntry? config,
        Dictionary<string, SshProfileTrustEntry> profiles,
        byte[]? dataKey = null)
    {
        _creatorPathHashSha256 = creatorPathHashSha256;
        _config = config;
        _profiles = profiles;
        _dataKey = dataKey;
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
            using var mutex = AcquireMutex(filePath);
            var store = ReadStore(filePath, out var requiresMigration);
            if (requiresMigration)
            {
                WriteStoreAtomic(filePath, store);
                _ = ReadStore(filePath, out _);
            }

            return store;
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

        using var mutex = AcquireMutex(filePath);
        var target = File.Exists(filePath)
            ? ReadStore(filePath, out _)
            : new SshProfileTrustStore(string.Empty, null, new Dictionary<string, SshProfileTrustEntry>(StringComparer.OrdinalIgnoreCase));
        ApplyChanges(target);
        WriteStoreAtomic(filePath, target);
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
            _creatorPathHashChanged = true;
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
        _configChanged = true;
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
    /// Tries to get the trusted authorization snapshot for one profile.
    /// </summary>
    public bool TryGetAuthorizationSnapshot(string profileName, out SshProfileAuthorizationSnapshot? snapshot)
    {
        if (_profiles.TryGetValue(profileName, out var entry) && entry.AuthorizationSnapshot is not null)
        {
            snapshot = entry.AuthorizationSnapshot;
            return true;
        }

        snapshot = null;
        return false;
    }

    /// <summary>
    /// Stores the trusted hash for one profile.
    /// </summary>
    /// <param name="profileName">The profile name.</param>
    /// <param name="hashSha256">The SHA-256 hash.</param>
    public void SetHash(string profileName, string hashSha256, SshProfileAuthorizationSnapshot? authorizationSnapshot = null)
    {
        _profiles[profileName] = new SshProfileTrustEntry(profileName, hashSha256, authorizationSnapshot);
        _profileUpdates[profileName] = _profiles[profileName];
        _profileRemovals.Remove(profileName);
    }

    /// <summary>
    /// Removes the trusted hash for one profile.
    /// </summary>
    /// <param name="profileName">The profile name.</param>
    /// <returns><c>true</c> when the profile was removed.</returns>
    public bool RemoveHash(string profileName)
    {
        var removed = _profiles.Remove(profileName);
        if (removed)
        {
            _profileUpdates.Remove(profileName);
            _profileRemovals.Add(profileName);
        }

        return removed;
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

    private void ApplyChanges(SshProfileTrustStore target)
    {
        if (_creatorPathHashChanged)
        {
            target._creatorPathHashSha256 = _creatorPathHashSha256;
        }

        if (_configChanged)
        {
            target._config = _config;
        }

        foreach (var profileName in _profileRemovals)
        {
            target._profiles.Remove(profileName);
        }

        foreach (var update in _profileUpdates)
        {
            target._profiles[update.Key] = update.Value;
        }
    }

    private static SshProfileTrustStore ReadStore(
        string filePath,
        out bool requiresMigration)
    {
        var envelope = JsonSerializer.Deserialize<StoreEnvelope>(File.ReadAllText(filePath), JsonOptions)
            ?? throw new InvalidOperationException("MCP trust store is empty.");
        if (envelope.FormatVersion is not (CurrentFormatVersion or LegacyFileKeyFormatVersion or LegacyDpapiFormatVersion))
        {
            throw new InvalidOperationException("MCP trust store format version is not supported.");
        }

        var dataKey = UnprotectDataKey(filePath, envelope, out requiresMigration);
        var nonce = Convert.FromBase64String(envelope.Nonce);
        var tag = Convert.FromBase64String(envelope.Tag);
        var payload = Convert.FromBase64String(envelope.Payload);
        if (nonce.Length != NonceSize || tag.Length != TagSize)
        {
            throw new CryptographicException("MCP trust store cryptographic parameters are invalid.");
        }

        var buffer = new byte[payload.Length];
        using (var guard = new AesGcm(dataKey, TagSize))
        {
            var associatedData = envelope.FormatVersion is CurrentFormatVersion or LegacyDpapiFormatVersion
                ? CreateAssociatedData(envelope.FormatVersion, envelope.KeyProtection)
                : null;
            guard.Decrypt(nonce, payload, tag, buffer, associatedData);
        }

        var manifest = JsonSerializer.Deserialize<TrustStoreManifest>(Encoding.UTF8.GetString(buffer), JsonOptions)
            ?? throw new InvalidOperationException("MCP trust store manifest is empty.");
        var profiles = manifest.Profiles
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
            .ToDictionary(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        return new SshProfileTrustStore(manifest.CreatorPathHashSha256, manifest.Config, profiles, dataKey)
        {
            FileExisted = true,
        };
    }

    private static byte[] UnprotectDataKey(
        string filePath,
        StoreEnvelope envelope,
        out bool requiresMigration)
    {
        requiresMigration = false;
        if (envelope.FormatVersion == CurrentFormatVersion
            && string.Equals(envelope.KeyProtection, KeyProtectionFileAcl, StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(envelope.ProtectedKey))
            {
                throw new CryptographicException("MCP trust store must not embed the data key.");
            }

            requiresMigration = false;
            return ReadKeyFile(filePath);
        }

        if (envelope.FormatVersion == LegacyFileKeyFormatVersion
            && string.Equals(envelope.KeyProtection, "file", StringComparison.OrdinalIgnoreCase))
        {
            requiresMigration = true;
            return ReadKeyFile(filePath, protectLegacyKey: true);
        }

        if (envelope.FormatVersion == LegacyDpapiFormatVersion
            && string.Equals(envelope.KeyProtection, KeyProtectionDpapiCurrentUser, StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Legacy MCP trust store migration requires Windows.");
            }

            var protectedKey = Convert.FromBase64String(envelope.ProtectedKey);
            var dataKey = ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
            if (dataKey.Length != DataKeySize)
            {
                throw new CryptographicException("MCP trust store data key length is invalid.");
            }

            var keyPath = GetKeyFilePath(filePath);
            if (File.Exists(keyPath))
            {
                var existingKey = ReadKeyFile(filePath);
                if (!CryptographicOperations.FixedTimeEquals(dataKey, existingKey))
                {
                    throw new CryptographicException("MCP trust store migration key does not match the legacy key.");
                }
            }

            requiresMigration = true;
            return dataKey;
        }

        throw new CryptographicException("MCP trust store key protection is not supported.");
    }

    private static void WriteStoreAtomic(string filePath, SshProfileTrustStore store)
    {
        var manifest = new TrustStoreManifest
        {
            CreatorPathHashSha256 = store._creatorPathHashSha256,
            Config = store._config,
            Profiles = store._profiles.Values.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
        };
        var buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, JsonOptions));
        var dataKey = store._dataKey
            ?? (File.Exists(GetKeyFilePath(filePath))
                ? ReadKeyFile(filePath)
                : RandomNumberGenerator.GetBytes(DataKeySize));
        if (dataKey.Length != DataKeySize)
        {
            throw new CryptographicException("MCP trust store data key length is invalid.");
        }

        WriteKeyFileIfMissing(filePath, dataKey);
        store._dataKey = dataKey;
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var payload = new byte[buffer.Length];
        var tag = new byte[TagSize];
        using (var guard = new AesGcm(dataKey, TagSize))
        {
            guard.Encrypt(nonce, buffer, payload, tag, CreateAssociatedData(CurrentFormatVersion, KeyProtectionFileAcl));
        }

        var envelope = new StoreEnvelope
        {
            FormatVersion = CurrentFormatVersion,
            KeyProtection = KeyProtectionFileAcl,
            ProtectedKey = string.Empty,
            Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag),
            Payload = Convert.ToBase64String(payload),
        };
        var tempPath = filePath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(JsonSerializer.Serialize(envelope, JsonOptions));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static byte[] CreateAssociatedData(int formatVersion, string keyProtection)
    {
        return Encoding.UTF8.GetBytes($"kelpie-mcp-trust-store|{formatVersion}|{keyProtection}");
    }

    private static MutexLease AcquireMutex(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath).ToUpperInvariant();
        var name = "Global\\KelpieMcpTrustStore-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath)));
        var mutex = new Mutex(false, name);
        try
        {
            if (!mutex.WaitOne(TimeSpan.FromSeconds(MutexTimeoutSeconds)))
            {
                throw new TimeoutException("Timed out waiting for the MCP trust store update lock.");
            }

            return new MutexLease(mutex);
        }
        catch (AbandonedMutexException)
        {
            return new MutexLease(mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    private static string GetKeyFilePath(string trustStorePath)
    {
        return trustStorePath + ".key";
    }

    private static byte[] ReadKeyFile(string filePath, bool protectLegacyKey = false)
    {
        var keyPath = GetKeyFilePath(filePath);
        if (!File.Exists(keyPath))
        {
            throw new CryptographicException("MCP trust store key file is missing.");
        }

        if (OperatingSystem.IsWindows())
        {
            if (protectLegacyKey)
            {
                FileSystemAclExtensions.SetAccessControl(new FileInfo(keyPath), CreateKeyFileSecurity());
            }

            ValidateKeyFileAcl(keyPath);
        }
        else if (File.GetUnixFileMode(keyPath) != UnixKeyFileMode)
        {
            File.SetUnixFileMode(keyPath, UnixKeyFileMode);
        }

        var dataKey = Convert.FromBase64String(File.ReadAllText(keyPath));
        return dataKey.Length == DataKeySize
            ? dataKey
            : throw new CryptographicException("MCP trust store data key length is invalid.");
    }

    private static void WriteKeyFileIfMissing(string filePath, byte[] dataKey)
    {
        var keyPath = GetKeyFilePath(filePath);
        if (File.Exists(keyPath))
        {
            var existingKey = ReadKeyFile(filePath);
            if (!CryptographicOperations.FixedTimeEquals(dataKey, existingKey))
            {
                throw new CryptographicException("MCP trust store key file does not match the active key.");
            }

            return;
        }

        var tempPath = keyPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var bytes = Encoding.ASCII.GetBytes(Convert.ToBase64String(dataKey));
            using (var stream = CreateKeyFile(tempPath))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(tempPath, keyPath);
            }
            catch (IOException) when (File.Exists(keyPath))
            {
                var existingKey = ReadKeyFile(filePath);
                if (!CryptographicOperations.FixedTimeEquals(dataKey, existingKey))
                {
                    throw new CryptographicException("MCP trust store key file does not match the active key.");
                }
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static FileStream CreateKeyFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = UnixKeyFileMode,
            });
        }

        return CreateWindowsKeyFile(path);
    }

    [SupportedOSPlatform("windows")]
    private static FileStream CreateWindowsKeyFile(string path)
    {
        var security = CreateKeyFileSecurity();
        return new FileInfo(path).Create(
            FileMode.CreateNew,
            FileSystemRights.FullControl,
            FileShare.None,
            4096,
            FileOptions.WriteThrough,
            security);
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateKeyFileAcl(string path)
    {
        var security = FileSystemAclExtensions.GetAccessControl(new FileInfo(path));
        var descriptor = new RawSecurityDescriptor(security.GetSecurityDescriptorBinaryForm(), 0);
        if (!security.AreAccessRulesProtected || descriptor.DiscretionaryAcl is null)
        {
            throw new CryptographicException("MCP trust store key ACL must disable inheritance and restrict access.");
        }

        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow)
            {
                continue;
            }

            var sid = (SecurityIdentifier)rule.IdentityReference;
            if (sid.IsWellKnown(WellKnownSidType.WorldSid)
                || sid.IsWellKnown(WellKnownSidType.AuthenticatedUserSid)
                || sid.IsWellKnown(WellKnownSidType.BuiltinUsersSid)
                || sid.IsWellKnown(WellKnownSidType.BuiltinGuestsSid)
                || sid.IsWellKnown(WellKnownSidType.AnonymousSid)
                || sid.IsWellKnown(WellKnownSidType.NetworkSid)
                || sid.IsWellKnown(WellKnownSidType.InteractiveSid)
                || sid.Value is "S-1-15-2-1" or "S-1-15-2-2")
            {
                throw new CryptographicException("MCP trust store key ACL grants access to a broad principal.");
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static FileSecurity CreateKeyFileSecurity()
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddFullControl(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
        AddFullControl(security, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));

        var currentSid = WindowsIdentity.GetCurrent().User;
        if (currentSid is not null)
        {
            AddFullControl(security, currentSid);
        }

        return security;
    }

    [SupportedOSPlatform("windows")]
    private static void AddFullControl(FileSecurity security, SecurityIdentifier sid)
    {
        security.AddAccessRule(new FileSystemAccessRule(
            sid,
            FileSystemRights.FullControl,
            AccessControlType.Allow));
    }

    private sealed class MutexLease(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    private sealed class TrustStoreManifest
    {
        public int FormatVersion { get; init; } = 2;

        public string CreatorPathHashSha256 { get; init; } = string.Empty;

        public SshConfigTrustEntry? Config { get; init; }

        public IReadOnlyCollection<SshProfileTrustEntry> Profiles { get; init; } = [];
    }

    private sealed class StoreEnvelope
    {
        public int FormatVersion { get; init; }

        public string KeyProtection { get; init; } = string.Empty;

        public string ProtectedKey { get; init; } = string.Empty;

        public string Nonce { get; init; } = string.Empty;

        public string Tag { get; init; } = string.Empty;

        [JsonPropertyName("Ciphertext")]
        public string Payload { get; init; } = string.Empty;
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
/// <param name="AuthorizationSnapshot">The trusted normalized authorization snapshot.</param>
public sealed record SshProfileTrustEntry(
    string Name,
    string HashSha256,
    SshProfileAuthorizationSnapshot? AuthorizationSnapshot = null);
