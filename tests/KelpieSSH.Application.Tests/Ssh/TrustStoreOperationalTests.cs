using System.Security.AccessControl;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class TrustStoreOperationalTests
{
    [UnixFact]
    [UnsupportedOSPlatform("windows")]
    public void UnixKeyCreation_ShouldRestrictKeyToOwnerReadWrite()
    {
        using var fixture = new StoreFixture();

        File.GetUnixFileMode(fixture.KeyPath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        SshProfileTrustStore.Load(fixture.StorePath).TryGetHash("fixture", out var hash).Should().BeTrue();
        hash.Should().Be("original-profile");
    }

    [UnixFact]
    [UnsupportedOSPlatform("windows")]
    public void UnixKeyLoad_ShouldRestrictExistingKeyWithoutReplacingFiles()
    {
        using var fixture = new StoreFixture();
        var originalKey = File.ReadAllBytes(fixture.KeyPath);
        File.SetUnixFileMode(fixture.KeyPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        var restored = SshProfileTrustStore.Load(fixture.StorePath);

        File.GetUnixFileMode(fixture.KeyPath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.ReadAllBytes(fixture.KeyPath).Should().Equal(originalKey);
        File.ReadAllBytes(fixture.StorePath).Should().Equal(fixture.OriginalEnvelope);
        restored.TryGetHash("fixture", out var hash).Should().BeTrue();
        hash.Should().Be("original-profile");
    }

    [UnixFact]
    [UnsupportedOSPlatform("windows")]
    public void UnixKeySave_ShouldRestrictExistingKeyAndPreserveTrustedProfiles()
    {
        using var fixture = new StoreFixture();
        var pending = SshProfileTrustStore.Load(fixture.StorePath);
        var originalKey = File.ReadAllBytes(fixture.KeyPath);
        pending.SetHash("replacement", "replacement-hash");
        File.SetUnixFileMode(fixture.KeyPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite);

        pending.Save(fixture.StorePath);

        File.GetUnixFileMode(fixture.KeyPath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.ReadAllBytes(fixture.KeyPath).Should().Equal(originalKey);
        var restored = SshProfileTrustStore.Load(fixture.StorePath);
        restored.TryGetHash("fixture", out var originalHash).Should().BeTrue();
        originalHash.Should().Be("original-profile");
        restored.TryGetHash("replacement", out var changedHash).Should().BeTrue();
        changedHash.Should().Be("replacement-hash");
    }

    [Fact]
    public void MissingKey_ShouldRejectLoadAndSaveWithoutReplacingStore()
    {
        using var fixture = new StoreFixture();
        var pending = SshProfileTrustStore.Load(fixture.StorePath);
        pending.SetHash("replacement", "replacement-hash");
        File.Delete(fixture.KeyPath);

        AssertRejectedWithoutOverwrite(fixture, pending);
        File.Exists(fixture.KeyPath).Should().BeFalse();
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void InvalidOrUnrelatedKey_ShouldRejectLoadAndSaveWithoutReplacingFiles(int keySize)
    {
        using var fixture = new StoreFixture();
        var pending = SshProfileTrustStore.Load(fixture.StorePath);
        pending.SetHash("replacement", "replacement-hash");
        var wrongKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(keySize));
        File.WriteAllText(fixture.KeyPath, wrongKey);

        AssertRejectedWithoutOverwrite(fixture, pending);
        File.ReadAllText(fixture.KeyPath).Should().Be(wrongKey);
    }

    [Fact]
    public void BackupPair_ShouldRestoreOriginalTrustWithRestrictedKeyAcl()
    {
        using var fixture = new StoreFixture();
        var backupDirectory = Path.Combine(fixture.DirectoryPath, "backup");
        Directory.CreateDirectory(backupDirectory);
        var dataBackup = Path.Combine(backupDirectory, "store.dat");
        var keyBackup = dataBackup + ".key";
        File.Copy(fixture.StorePath, dataBackup);
        File.Copy(fixture.KeyPath, keyBackup);
        if (OperatingSystem.IsWindows())
        {
            var backupAcl = new FileSecurity();
            backupAcl.SetSecurityDescriptorBinaryForm(
                new FileInfo(fixture.KeyPath).GetAccessControl().GetSecurityDescriptorBinaryForm(),
                AccessControlSections.Access);
            new FileInfo(keyBackup).SetAccessControl(backupAcl);
        }
        var changed = SshProfileTrustStore.Load(fixture.StorePath);
        changed.SetConfigHash("changed-config");
        changed.SetHash("replacement", "replacement-hash");
        changed.Save(fixture.StorePath);
        File.Delete(fixture.KeyPath);
        File.Copy(dataBackup, fixture.StorePath, overwrite: true);
        File.Copy(keyBackup, fixture.KeyPath);
        if (OperatingSystem.IsWindows())
        {
            var restoredAcl = new FileSecurity();
            restoredAcl.SetSecurityDescriptorBinaryForm(
                new FileInfo(keyBackup).GetAccessControl().GetSecurityDescriptorBinaryForm(),
                AccessControlSections.Access);
            new FileInfo(fixture.KeyPath).SetAccessControl(restoredAcl);
        }

        var restored = SshProfileTrustStore.Load(fixture.StorePath);

        restored.TryGetConfigHash(out var configHash).Should().BeTrue();
        configHash.Should().Be("original-config");
        restored.TryGetHash("fixture", out var profileHash).Should().BeTrue();
        profileHash.Should().Be("original-profile");
        restored.TryGetHash("replacement", out _).Should().BeFalse();
        File.ReadAllBytes(fixture.StorePath).Should().Equal(fixture.OriginalEnvelope);
        if (OperatingSystem.IsWindows())
        {
            AssertRestrictedKeyAcl(fixture.KeyPath);
        }
    }

    [Fact]
    public void WindowsKeyLoadAndSave_ShouldPreserveReadOnlyAclWithoutChangingPermissions()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = new StoreFixture();
        var sid = WindowsIdentity.GetCurrent().User!;
        var security = new FileInfo(fixture.KeyPath).GetAccessControl();
        security.SetAccessRule(new FileSystemAccessRule(sid, FileSystemRights.Read, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier("S-1-3-4"), FileSystemRights.ReadPermissions, AccessControlType.Allow));
        new FileInfo(fixture.KeyPath).SetAccessControl(security);
        var originalAcl = new FileInfo(fixture.KeyPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All);
        var originalKey = File.ReadAllBytes(fixture.KeyPath);

        var loaded = SshProfileTrustStore.Load(fixture.StorePath);

        loaded.TryGetHash("fixture", out var hash).Should().BeTrue();
        hash.Should().Be("original-profile");
        new FileInfo(fixture.KeyPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All).Should().Be(originalAcl);
        File.ReadAllBytes(fixture.KeyPath).Should().Equal(originalKey);
        File.ReadAllBytes(fixture.StorePath).Should().Equal(fixture.OriginalEnvelope);

        loaded.SetHash("replacement", "replacement-hash");
        loaded.Save(fixture.StorePath);

        new FileInfo(fixture.KeyPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All).Should().Be(originalAcl);
        File.ReadAllBytes(fixture.KeyPath).Should().Equal(originalKey);
        SshProfileTrustStore.Load(fixture.StorePath).TryGetHash("replacement", out var savedHash).Should().BeTrue();
        savedHash.Should().Be("replacement-hash");
    }

    [Theory]
    [InlineData("S-1-1-0")]
    [InlineData("S-1-5-11")]
    [InlineData("S-1-5-32-545")]
    public void WindowsKeyLoad_ShouldRejectBroadReadAccessWithoutChangingFiles(string broadSid)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = new StoreFixture();
        var security = new FileInfo(fixture.KeyPath).GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(broadSid), FileSystemRights.Read, AccessControlType.Allow));
        new FileInfo(fixture.KeyPath).SetAccessControl(security);
        var originalAcl = new FileInfo(fixture.KeyPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All);

        var load = () => SshProfileTrustStore.Load(fixture.StorePath);

        load.Should().Throw<InvalidOperationException>().WithInnerException<CryptographicException>();
        new FileInfo(fixture.KeyPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All).Should().Be(originalAcl);
        File.ReadAllBytes(fixture.StorePath).Should().Equal(fixture.OriginalEnvelope);
    }

    [Fact]
    public void WindowsKeyLoad_ShouldRejectInheritedAclWithoutChangingFiles()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = new StoreFixture();
        var security = new FileInfo(fixture.KeyPath).GetAccessControl();
        security.SetAccessRuleProtection(false, true);
        new FileInfo(fixture.KeyPath).SetAccessControl(security);
        var originalAcl = new FileInfo(fixture.KeyPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All);

        var load = () => SshProfileTrustStore.Load(fixture.StorePath);

        load.Should().Throw<InvalidOperationException>().WithInnerException<CryptographicException>();
        new FileInfo(fixture.KeyPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All).Should().Be(originalAcl);
        File.ReadAllBytes(fixture.StorePath).Should().Equal(fixture.OriginalEnvelope);
    }

    [Fact]
    public void WindowsKeyAccessDenied_ShouldRejectLoadAndSaveWithoutReplacingStore()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var fixture = new StoreFixture();
        var pending = SshProfileTrustStore.Load(fixture.StorePath);
        pending.SetHash("replacement", "replacement-hash");
        var sid = WindowsIdentity.GetCurrent().User!;
        var security = new FileInfo(fixture.KeyPath).GetAccessControl();
        // OWNER RIGHTS removes the owner's implicit WRITE_DAC grant for this isolated key.
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier("S-1-3-4"), FileSystemRights.ReadPermissions, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            sid, FileSystemRights.ReadData | FileSystemRights.ChangePermissions, AccessControlType.Deny));
        new FileInfo(fixture.KeyPath).SetAccessControl(security);
        var readKey = () => File.ReadAllText(fixture.KeyPath);
        readKey.Should().Throw<UnauthorizedAccessException>();

        AssertRejectedWithoutOverwrite(fixture, pending);
        File.Exists(fixture.KeyPath).Should().BeTrue();
    }

    [SupportedOSPlatform("windows")]
    private static void AssertRestrictedKeyAcl(string keyPath)
    {
        var security = new FileInfo(keyPath).GetAccessControl();
        security.AreAccessRulesProtected.Should().BeTrue();
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().ToArray();
        rules.Should().OnlyContain(rule => !rule.IsInherited && rule.AccessControlType == AccessControlType.Allow);
        rules.Select(rule => rule.IdentityReference.Value).Should().Contain(["S-1-5-18", "S-1-5-32-544"]);
    }

    private static void AssertRejectedWithoutOverwrite(StoreFixture fixture, SshProfileTrustStore pending)
    {
        var load = () => SshProfileTrustStore.Load(fixture.StorePath);
        load.Should().Throw<InvalidOperationException>()
            .WithMessage("MCP trust store could not be read or verified.");
        var save = () => pending.Save(fixture.StorePath);
        var error = save.Should().Throw<Exception>().Which;
        (error is CryptographicException or UnauthorizedAccessException).Should().BeTrue();
        File.ReadAllBytes(fixture.StorePath).Should().Equal(fixture.OriginalEnvelope);
    }

    private sealed class StoreFixture : IDisposable
    {
        public StoreFixture()
        {
            DirectoryPath = Path.Combine(AppContext.BaseDirectory, "service-trust-fixtures", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            StorePath = Path.Combine(DirectoryPath, "store.dat");
            var store = SshProfileTrustStore.Load(StorePath);
            store.SetConfigHash("original-config");
            store.SetHash("fixture", "original-profile");
            store.Save(StorePath);
            OriginalEnvelope = File.ReadAllBytes(StorePath);
        }

        public string DirectoryPath { get; }
        public string StorePath { get; }
        public string KeyPath => StorePath + ".key";
        public byte[] OriginalEnvelope { get; }

        public void Dispose()
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }

    private sealed class UnixFactAttribute : FactAttribute
    {
        public UnixFactAttribute()
        {
            if (OperatingSystem.IsWindows())
            {
                Skip = "Unix file permission verification requires a Unix host.";
            }
        }
    }
}
