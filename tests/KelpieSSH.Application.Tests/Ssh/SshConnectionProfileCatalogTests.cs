using FluentAssertions;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Application.Tests.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KelpieSSH.Application.Tests.Ssh;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class SshConnectionProfileCatalogTests
{
    [Fact]
    public void TryGet_ShouldReturnConfiguredProfile()
    {
        var profile = CreateProfile("vps01");
        var catalog = new SshConnectionProfileCatalog([profile]);

        var result = catalog.TryGet("VPS01", out var actual);

        result.Should().BeTrue();
        actual.Should().BeSameAs(profile);
    }

    [Fact]
    public void Constructor_ShouldRejectDuplicatedProfileName()
    {
        var action = () => new SshConnectionProfileCatalog(
        [
            CreateProfile("vps01"),
            CreateProfile("VPS01"),
        ]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH profile is duplicated: VPS01");
    }

    [Fact]
    public void ReloadingCatalog_ShouldLoadAddedProfileOnReload()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("deploy"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory);

        File.WriteAllText(Path.Combine(directory, "vps02.json"), CreateProfileJson("ops"));
        catalog.TryGet("vps02", out _).Should().BeFalse();

        var reload = catalog.Reload();

        var result = catalog.TryGet("vps02", out var profile);

        reload.Success.Should().BeTrue();
        reload.ProfileNames.Should().Equal("vps01", "vps02");
        result.Should().BeTrue();
        profile.UserName.Should().Be("ops");
    }

    [Fact]
    public void ReloadingCatalog_ShouldLoadUpdatedProfileOnReload()
    {
        var directory = CreateTempDirectory();
        var profilePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(profilePath, CreateProfileJson("deploy"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory);

        catalog.TryGet("vps01", out var first).Should().BeTrue();
        first.UserName.Should().Be("deploy");
        File.WriteAllText(profilePath, CreateProfileJson("ops"));
        catalog.TryGet("vps01", out var beforeReload).Should().BeTrue();
        beforeReload.UserName.Should().Be("deploy");

        var reload = catalog.Reload();

        var result = catalog.TryGet("vps01", out var second);

        reload.Success.Should().BeTrue();
        result.Should().BeTrue();
        second.UserName.Should().Be("ops");
    }

    [Fact]
    public void ReloadingCatalog_ShouldKeepLastGoodCatalogWhenReloadFails()
    {
        var directory = CreateTempDirectory();
        var profilePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(profilePath, CreateProfileJson("deploy"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory);

        File.WriteAllText(profilePath, "{ invalid json");
        var reload = catalog.Reload();

        var result = catalog.TryGet("vps01", out var profile);

        reload.Success.Should().BeFalse();
        reload.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.Should().BeTrue();
        profile.UserName.Should().Be("deploy");
        catalog.LastReloadError.Should().NotBeNull();
    }

    [Fact]
    public void ReloadingCatalog_WithoutTrustStore_ShouldSkipBrokenProfileAndReportError()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "broken.json"), "{ invalid json");
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("deploy"));

        var catalog = new ReloadingSshConnectionProfileCatalog(directory);

        catalog.TryGet("vps01", out var profile).Should().BeTrue();
        profile.UserName.Should().Be("deploy");
        catalog.ProfileLoadErrors.Should().ContainSingle(error =>
            error.ProfileName == "broken" && error.Reason == "profile-load-failed");
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldCreateBaseline()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("deploy"));

        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        catalog.TryGet("vps01", out var profile).Should().BeTrue();
        profile.UserName.Should().Be("deploy");
        catalog.ProfileLoadErrors.Should().BeEmpty();
        File.Exists(trustStorePath).Should().BeTrue();
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldRejectModifiedProfileWithoutReload()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var profilePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(profilePath, CreateProfileJson("deploy"));
        _ = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        File.WriteAllText(profilePath, CreateProfileJson("ops"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        catalog.TryGet("vps01", out _).Should().BeFalse();
        catalog.ProfileLoadErrors.Should().ContainSingle(error =>
            error.ProfileName == "vps01" && error.Reason == "profile-hash-mismatch");
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldAcceptModifiedProfileWithReload()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var profilePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(profilePath, CreateProfileJson("deploy"));
        _ = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        File.WriteAllText(profilePath, CreateProfileJson("ops"));
        var reloadedCatalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, ["vps01"]);
        var nextCatalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        reloadedCatalog.TryGet("vps01", out var reloadedProfile).Should().BeTrue();
        reloadedProfile.UserName.Should().Be("ops");
        reloadedCatalog.ProfileLoadErrors.Should().BeEmpty();
        nextCatalog.TryGet("vps01", out var nextProfile).Should().BeTrue();
        nextProfile.UserName.Should().Be("ops");
        nextCatalog.ProfileLoadErrors.Should().BeEmpty();
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldRejectUntrustedNewProfileWhenStoreExists()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("deploy"));
        _ = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        File.WriteAllText(Path.Combine(directory, "vps02.json"), CreateProfileJson("ops"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        catalog.TryGet("vps02", out _).Should().BeFalse();
        catalog.ProfileLoadErrors.Should().ContainSingle(error =>
            error.ProfileName == "vps02" && error.Reason == "profile-not-trusted");
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldAddTrustedProfile()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("deploy"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);
        File.WriteAllText(Path.Combine(directory, "vps02.json"), CreateProfileJson("ops"));

        var result = catalog.AddTrustedProfile("vps02");
        var nextCatalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("add");
        nextCatalog.TryGet("vps02", out var profile).Should().BeTrue();
        profile.UserName.Should().Be("ops");
        nextCatalog.ProfileLoadErrors.Should().BeEmpty();
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldReloadTrustedProfile()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var profilePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(profilePath, CreateProfileJson("deploy"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        File.WriteAllText(profilePath, CreateProfileJson("ops"));
        var result = catalog.ReloadTrustedProfile("vps01");
        var nextCatalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("reload");
        nextCatalog.TryGet("vps01", out var profile).Should().BeTrue();
        profile.UserName.Should().Be("ops");
        nextCatalog.ProfileLoadErrors.Should().BeEmpty();
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldRevokeTrustedProfile()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("deploy"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        var result = catalog.RevokeTrustedProfile("vps01");
        var nextCatalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        result.Success.Should().BeTrue();
        result.Status.Should().Be("revoked");
        nextCatalog.TryGet("vps01", out _).Should().BeFalse();
        nextCatalog.ProfileLoadErrors.Should().ContainSingle(error =>
            error.ProfileName == "vps01" && error.Reason == "profile-not-trusted");
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldReturnTrustCapabilities()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("deploy"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);
        File.WriteAllText(Path.Combine(directory, "vps02.json"), CreateProfileJson("ops"));

        var trusted = catalog.GetTrustCapabilities("vps01");
        var untrusted = catalog.GetTrustCapabilities("vps02");

        trusted.AddAllowed.Should().BeFalse();
        trusted.ReloadAllowed.Should().BeTrue();
        trusted.RevokeAllowed.Should().BeTrue();
        untrusted.AddAllowed.Should().BeTrue();
        untrusted.ReloadAllowed.Should().BeFalse();
        untrusted.RevokeAllowed.Should().BeFalse();
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldFailWhenTrustStoreIsCorrupted()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("deploy"));
        _ = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        File.WriteAllText(trustStorePath, "{ invalid trust store");
        var action = () => new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("MCP trust store could not be read or verified.");
    }

    [Fact]
    public void ReloadingCatalog_WithTrustStore_ShouldNotCreateTrustStoreWhenOnlyProfileIsInvalid()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        File.WriteAllText(Path.Combine(directory, "vps01.json"), "{ invalid json");

        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);

        catalog.TryGet("vps01", out _).Should().BeFalse();
        catalog.ProfileLoadErrors.Should().ContainSingle(error =>
            error.ProfileName == "vps01" && error.Reason == "profile-load-failed");
        File.Exists(trustStorePath).Should().BeFalse();
    }

    [Fact]
    public void TrustStore_ShouldStoreConfigHash()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var configPath = Path.Combine(directory, "kelpiemcp.json");
        File.WriteAllText(configPath, """{"Server":{"Port":45432}}""");
        var expectedHash = SshProfileTrustStore.ComputeFileHash(configPath);

        var trustStore = SshProfileTrustStore.Load(trustStorePath);
        trustStore.SetConfigHash(expectedHash);
        trustStore.Save(trustStorePath);
        var loaded = SshProfileTrustStore.Load(trustStorePath);

        loaded.TryGetConfigHash(out var actualHash).Should().BeTrue();
        actualHash.Should().Be(expectedHash);
    }

    [Fact]
    public void TrustStore_ShouldProtectKeyInsideEnvelopeWithoutSeparateKeyFile()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");

        var trustStore = SshProfileTrustStore.Load(trustStorePath);
        trustStore.SetConfigHash("abc123");
        trustStore.Save(trustStorePath);

        File.Exists(trustStorePath).Should().BeTrue();
        File.Exists(trustStorePath + ".key").Should().BeFalse();
        File.ReadAllText(trustStorePath).Should().Contain("\"FormatVersion\": 3");
        File.ReadAllText(trustStorePath).Should().Contain("\"KeyProtection\": \"dpapi-current-user\"");
        File.ReadAllText(trustStorePath).Should().Contain("\"ProtectedKey\":");
    }

    [Fact]
    public void TrustStore_ShouldRejectTamperedKeyProtection()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var trustStore = SshProfileTrustStore.Load(trustStorePath);
        trustStore.SetConfigHash("abc123");
        trustStore.Save(trustStorePath);
        var envelope = File.ReadAllText(trustStorePath)
            .Replace("dpapi-current-user", "dpapi-current-users", StringComparison.Ordinal);
        File.WriteAllText(trustStorePath, envelope);

        var action = () => SshProfileTrustStore.Load(trustStorePath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("MCP trust store could not be read or verified.");
    }

    [Theory]
    [InlineData("ProtectedKey")]
    [InlineData("Nonce")]
    [InlineData("Tag")]
    [InlineData("Ciphertext")]
    public void TrustStore_ShouldFailClosedWhenEnvelopeCryptographicElementIsTampered(string propertyName)
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var trustStore = SshProfileTrustStore.Load(trustStorePath);
        trustStore.SetConfigHash("abc123");
        trustStore.Save(trustStorePath);
        var envelope = JsonNode.Parse(File.ReadAllText(trustStorePath))!.AsObject();
        var value = envelope[propertyName]!.GetValue<string>();
        envelope[propertyName] = (value[0] == 'A' ? "B" : "A") + value[1..];
        File.WriteAllText(trustStorePath, envelope.ToJsonString());

        var action = () => SshProfileTrustStore.Load(trustStorePath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("MCP trust store could not be read or verified.");
    }

    [Fact]
    public async Task TrustStore_ShouldMergeConcurrentWritersWithoutLosingProfiles()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var initial = SshProfileTrustStore.Load(trustStorePath);
        initial.SetConfigHash("abc123");
        initial.Save(trustStorePath);
        var writers = Enumerable.Range(0, 12).Select(index => Task.Run(() =>
        {
            var store = SshProfileTrustStore.Load(trustStorePath);
            store.SetHash("profile-" + index, "hash-" + index);
            store.Save(trustStorePath);
        }));

        await Task.WhenAll(writers);

        var loaded = SshProfileTrustStore.Load(trustStorePath);
        foreach (var index in Enumerable.Range(0, 12))
        {
            loaded.TryGetHash("profile-" + index, out var hash).Should().BeTrue();
            hash.Should().Be("hash-" + index);
        }
    }

    [Fact]
    public void TrustStore_ShouldMigrateValidVersion2StoreAndDeleteLegacyKeyAfterVerification()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        WriteLegacyVersion2Store(trustStorePath, "vps01", "trusted-hash");

        var loaded = SshProfileTrustStore.Load(trustStorePath);

        loaded.TryGetHash("vps01", out var hash).Should().BeTrue();
        hash.Should().Be("trusted-hash");
        File.Exists(trustStorePath + ".key").Should().BeFalse();
        File.ReadAllText(trustStorePath).Should().Contain("\"FormatVersion\": 3");
        SshProfileTrustStore.Load(trustStorePath).TryGetHash("vps01", out _).Should().BeTrue();
    }

    [Fact]
    public void TrustStore_ShouldPreserveInconsistentVersion2FilesWhenMigrationFails()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        WriteLegacyVersion2Store(trustStorePath, "vps01", "trusted-hash");
        var originalStore = File.ReadAllBytes(trustStorePath);
        File.WriteAllText(trustStorePath + ".key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        var action = () => SshProfileTrustStore.Load(trustStorePath);

        action.Should().Throw<InvalidOperationException>();
        File.ReadAllBytes(trustStorePath).Should().Equal(originalStore);
        File.Exists(trustStorePath + ".key").Should().BeTrue();
    }

    private static void WriteLegacyVersion2Store(string filePath, string profileName, string hash)
    {
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            FormatVersion = 2,
            CreatorPathHashSha256 = string.Empty,
            Config = (object?)null,
            Profiles = new[] { new { Name = profileName, HashSha256 = hash } },
        }, new JsonSerializerOptions { WriteIndented = true });
        var key = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[manifest.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, 16))
        {
            aes.Encrypt(nonce, manifest, ciphertext, tag);
        }

        var envelope = new
        {
            FormatVersion = 2,
            KeyProtection = "file",
            ProtectedKey = string.Empty,
            Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag),
            Ciphertext = Convert.ToBase64String(ciphertext),
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(filePath + ".key", Convert.ToBase64String(key));
    }

    [Fact]
    public void TrustStore_ShouldStoreCreatorPathHash()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var expectedHash = SshProfileTrustStore.ComputePathHash(Path.Combine(directory, "KelpieMCPServer.exe"));

        var trustStore = SshProfileTrustStore.Load(trustStorePath);
        trustStore.SetCreatorPathHashIfMissing(expectedHash);
        trustStore.Save(trustStorePath);
        var loaded = SshProfileTrustStore.Load(trustStorePath);

        loaded.TryGetCreatorPathHash(out var actualHash).Should().BeTrue();
        actualHash.Should().Be(expectedHash);
    }

    [Fact]
    public void TrustStore_ShouldDetectConfigHashMismatch()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var configPath = Path.Combine(directory, "kelpiemcp.json");
        File.WriteAllText(configPath, """{"Server":{"Port":45432}}""");
        var trustStore = SshProfileTrustStore.Load(trustStorePath);
        trustStore.SetConfigHash(SshProfileTrustStore.ComputeFileHash(configPath));
        trustStore.Save(trustStorePath);

        File.WriteAllText(configPath, """{"Server":{"Port":45433}}""");
        var loaded = SshProfileTrustStore.Load(trustStorePath);
        var currentHash = SshProfileTrustStore.ComputeFileHash(configPath);

        loaded.TryGetConfigHash(out var trustedHash).Should().BeTrue();
        trustedHash.Should().NotBe(currentHash);
    }

    [Fact]
    public void TrustStore_ShouldAcceptConfigHashUpdate()
    {
        var directory = CreateTempDirectory();
        var trustStorePath = Path.Combine(Path.GetDirectoryName(directory)!, "profile-trust-" + Guid.NewGuid().ToString("N") + ".dat");
        var configPath = Path.Combine(directory, "kelpiemcp.json");
        File.WriteAllText(configPath, """{"Server":{"Port":45432}}""");
        var trustStore = SshProfileTrustStore.Load(trustStorePath);
        trustStore.SetConfigHash(SshProfileTrustStore.ComputeFileHash(configPath));
        trustStore.Save(trustStorePath);

        File.WriteAllText(configPath, """{"Server":{"Port":45433}}""");
        var reloadedStore = SshProfileTrustStore.Load(trustStorePath);
        reloadedStore.SetConfigHash(SshProfileTrustStore.ComputeFileHash(configPath));
        reloadedStore.Save(trustStorePath);
        var loaded = SshProfileTrustStore.Load(trustStorePath);

        loaded.TryGetConfigHash(out var trustedHash).Should().BeTrue();
        trustedHash.Should().Be(SshProfileTrustStore.ComputeFileHash(configPath));
    }

    [Fact]
    public void ToProfile_ShouldResolveRelativePrivateKeyPath()
    {
        var options = new SshConnectionProfileOptions
        {
            Name = "vps01",
            Host = new SshConnectionHostOptions
            {
                Address = "example.invalid",
            },
            Authentication = new SshConnectionAuthenticationOptions
            {
                UserName = "deploy",
                Method = "privateKey",
                PrivateKeyPath = "keys/id_ed25519",
            },
            Connection = new SshConnectionConnectionOptions
            {
                TimeoutSeconds = 15,
            },
            Platform = new SshConnectionPlatformOptions
            {
                OsFamily = "debian",
                PackageManager = "apt",
            },
            Policy = new SshConnectionPolicyOptions
            {
                Level = "readOnly",
                AllowedRoots = ["/var/www"],
            },
        };

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.AuthenticationMethod.Should().Be("privateKey");
        profile.PrivateKeyPath.Should().Be(Path.GetFullPath(Path.Combine("config", "keys/id_ed25519")));
        profile.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(15));
        profile.OsFamily.Should().Be("debian");
        profile.PackageManager.Should().Be("apt");
        profile.Host.Should().Be("example.invalid");
        profile.UserName.Should().Be("deploy");
        profile.Mode.Should().Be(KelpiePolicyMode.Safe);
        profile.Capabilities.List().Should().BeEmpty();
        profile.AllowedRoots.Should().Equal("/var/www");
    }

    [Fact]
    public void ToProfile_ShouldResolvePrivateKeyFileFromKelpieKeys()
    {
        var homeDirectory = Path.Combine(Path.GetTempPath(), "kelpie-" + Guid.NewGuid().ToString("N"));
        var profilesDirectory = Path.Combine(homeDirectory, "profiles");
        var options = CreateProfileOptions();
        options.Authentication.PrivateKeyPath = string.Empty;
        options.Authentication.PrivateKeyFile = "vps01_ed25519";

        var profile = options.ToProfile(profilesDirectory);

        profile.PrivateKeyPath.Should().Be(Path.GetFullPath(Path.Combine(homeDirectory, "keys", "vps01_ed25519")));
    }

    [Fact]
    public void ToProfile_ShouldResolvePrivateKeyFileFromProfileDirectoryParent()
    {
        var homeDirectory = Path.Combine(Path.GetTempPath(), "kelpie-" + Guid.NewGuid().ToString("N"));
        var profilesDirectory = Path.Combine(homeDirectory, "profiles");
        var options = CreateProfileOptions();
        options.Authentication.PrivateKeyPath = string.Empty;
        options.Authentication.PrivateKeyFile = "vps01_ed25519";

        var profile = options.ToProfile(profilesDirectory);

        profile.PrivateKeyPath.Should().Be(Path.GetFullPath(Path.Combine(homeDirectory, "keys", "vps01_ed25519")));
    }

    [Fact]
    public void ToProfile_ShouldLoadPolicySetFromArray()
    {
        var options = CreateProfileOptions();
        options.Capabilities = System.Text.Json.JsonDocument.Parse(
            """["AllowAlias", "AllowMoveFiles"]""").RootElement.Clone();

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.Capabilities.Allows(KelpiePolicyNames.AllowAlias).Should().BeTrue();
        profile.Capabilities.Allows(KelpiePolicyNames.AllowMoveFiles).Should().BeTrue();
    }

    [Fact]
    public void ToProfile_ShouldLoadPolicyRoleFromLegacyMode()
    {
        var options = CreateProfileOptions();
        options.Mode = "Maintenance";

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.Mode.Should().Be(KelpiePolicyMode.Maintenance);
        profile.Roles.Should().Contain(KelpieRoleNames.Maintenance);
    }

    [Fact]
    public void ToProfile_ShouldUseSafeRoleByDefault()
    {
        var options = CreateProfileOptions();
        options.Mode = string.Empty;

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.Mode.Should().Be(KelpiePolicyMode.Safe);
        profile.Roles.Should().ContainSingle().Which.Should().Be(KelpieRoleNames.Safe);
    }

    [Fact]
    public void ToProfile_ShouldResolveDefaultPackageManagerFromOsFamily()
    {
        var options = CreateProfileOptions();
        options.Platform.OsFamily = "alma";
        options.Platform.PackageManager = string.Empty;

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.PackageManager.Should().Be("dnf");
    }

    [Fact]
    public void ToProfile_ShouldResolveUbuntuPackageManagerAsApt()
    {
        var options = CreateProfileOptions();
        options.Platform.OsFamily = "ubuntu";
        options.Platform.PackageManager = string.Empty;

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.PackageManager.Should().Be("apt");
    }

    [Fact]
    public void ToProfile_ShouldRejectMissingPackageManagerWhenOsFamilyCannotBeInferred()
    {
        var options = CreateProfileOptions();
        options.Platform.OsFamily = "unknownos";
        options.Platform.PackageManager = string.Empty;

        var action = () => options.ToProfile(Path.GetFullPath("config"));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH package manager is not configured and cannot be inferred from OS family: unknownos");
    }

    [Fact]
    public void ToProfile_ShouldRejectUnknownRole()
    {
        var options = CreateProfileOptions();
        options.Mode = "Dangerous";

        var action = () => options.ToProfile(Path.GetFullPath("config"));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unknown SSH role: Dangerous");
    }

    [Fact]
    public void ToProfile_ShouldLoadPolicySetFromPipeSeparatedString()
    {
        var options = CreateProfileOptions();
        options.Capabilities = System.Text.Json.JsonDocument.Parse(
            "\"AllowAlias|AllowSudo|AllowMoveFiles\"").RootElement.Clone();

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.Capabilities.List().Should().BeEquivalentTo(
        [
            KelpiePolicyNames.AllowAlias,
            KelpiePolicyNames.AllowSudo,
            KelpiePolicyNames.AllowMoveFiles,
        ]);
    }

    [Fact]
    public void ToProfile_ShouldRejectUnknownPolicyFlag()
    {
        var options = CreateProfileOptions();
        options.Capabilities = System.Text.Json.JsonDocument.Parse(
            "\"AlowMoveFiles\"").RootElement.Clone();

        var action = () => options.ToProfile(Path.GetFullPath("config"));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unknown SSH capability flag: AlowMoveFiles");
    }

    [Fact]
    public void ToProfile_ShouldRejectMissingPlatformSettings()
    {
        var options = new SshConnectionProfileOptions
        {
            Name = "vps01",
            Ssh = new SshConnectionSshOptions
            {
                Host = "example.invalid",
                UserName = "deploy",
                Authentication = new SshConnectionAuthenticationOptions
                {
                    Method = "privateKey",
                    PrivateKeyPath = "keys/id_ed25519",
                },
            },
        };

        var action = () => options.ToProfile(Path.GetFullPath("config"));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH OS family is required.");
    }

    [Fact]
    public void ToProfile_ShouldKeepPasswordAuthenticationSecretName()
    {
        var options = new SshConnectionProfileOptions
        {
            Name = "vps01",
            Host = new SshConnectionHostOptions
            {
                Address = "example.invalid",
            },
            Authentication = new SshConnectionAuthenticationOptions
            {
                UserName = "deploy",
                Method = "password",
                PasswordSecretName = "kelpie:vps01",
            },
            Platform = new SshConnectionPlatformOptions
            {
                OsFamily = "debian",
                PackageManager = "apt",
            },
            Policy = new SshConnectionPolicyOptions
            {
                Level = "readOnly",
            },
        };

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.AuthenticationMethod.Should().Be("password");
        profile.PrivateKeyPath.Should().BeNull();
        profile.PasswordSecretName.Should().Be("kelpie:vps01");
    }

    [Fact]
    public void ToProfile_ShouldSupportAuthAlias()
    {
        var options = new SshConnectionProfileOptions
        {
            Name = "vps01",
            Host = new SshConnectionHostOptions
            {
                Address = "example.invalid",
            },
            Auth = new SshConnectionAuthenticationOptions
            {
                UserName = "deploy",
                Method = "privateKey",
                PrivateKeyPath = "keys/id_ed25519",
            },
            Platform = new SshConnectionPlatformOptions
            {
                OsFamily = "debian",
                PackageManager = "apt",
            },
        };

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.UserName.Should().Be("deploy");
        profile.AuthenticationMethod.Should().Be("privateKey");
        profile.PrivateKeyPath.Should().Be(Path.GetFullPath(Path.Combine("config", "keys/id_ed25519")));
    }

    [Fact]
    public void ToProfile_ShouldSupportLegacySshSettings()
    {
        var options = new SshConnectionProfileOptions
        {
            Name = "vps01",
            Ssh = new SshConnectionSshOptions
            {
                Host = "example.invalid",
                Port = 2222,
                UserName = "deploy",
                Authentication = new SshConnectionAuthenticationOptions
                {
                    Method = "privateKey",
                    PrivateKeyPath = "keys/id_ed25519",
                },
            },
            Platform = new SshConnectionPlatformOptions
            {
                OsFamily = "debian",
                PackageManager = "apt",
            },
        };

        var profile = options.ToProfile(Path.GetFullPath("config"));

        profile.Host.Should().Be("example.invalid");
        profile.Port.Should().Be(2222);
        profile.UserName.Should().Be("deploy");
        profile.PrivateKeyPath.Should().Be(Path.GetFullPath(Path.Combine("config", "keys/id_ed25519")));
    }

    private static SshConnectionProfile CreateProfile(string name)
    {
        return new SshConnectionProfile
        {
            Name = name,
            Host = "example.invalid",
            UserName = "deploy",
            AuthenticationMethod = "privateKey",
            PrivateKeyPath = "id_ed25519",
            OsFamily = "debian",
            PackageManager = "apt",
            Capabilities = PolicySet.Empty,
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kelpie-profiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateProfileJson(string userName)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid"
          },
          "Auth": {
            "UserName": "{{userName}}",
            "Method": "privateKey",
            "PrivateKeyFile": "id_ed25519"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          }
        }
        """;
    }

    private static SshConnectionProfileOptions CreateProfileOptions()
    {
        return new SshConnectionProfileOptions
        {
            Name = "vps01",
            Host = new SshConnectionHostOptions
            {
                Address = "example.invalid",
            },
            Authentication = new SshConnectionAuthenticationOptions
            {
                UserName = "deploy",
                Method = "privateKey",
                PrivateKeyPath = "keys/id_ed25519",
            },
            Platform = new SshConnectionPlatformOptions
            {
                OsFamily = "debian",
                PackageManager = "apt",
            },
        };
    }
}
