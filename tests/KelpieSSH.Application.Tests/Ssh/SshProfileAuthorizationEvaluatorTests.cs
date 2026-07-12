using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshProfileAuthorizationEvaluatorTests
{
    [Theory]
    [InlineData(KelpiePolicyMode.ReadOnly, KelpiePolicyMode.Safe)]
    [InlineData(KelpiePolicyMode.Safe, KelpiePolicyMode.Maintenance)]
    [InlineData(KelpiePolicyMode.Maintenance, KelpiePolicyMode.Expert)]
    public void Compare_WhenModeIsRaised_ShouldReturnPrivilegeExpansion(
        KelpiePolicyMode baselineMode,
        KelpiePolicyMode proposedMode)
    {
        var result = SshProfileAuthorizationEvaluator.Compare(
            CreateSnapshot(mode: baselineMode),
            CreateSnapshot(mode: proposedMode));

        result.Kind.Should().Be(SshProfileAuthorizationChangeKind.PrivilegeExpansion);
        result.ChangedFields.Should().Contain("Mode");
    }

    [Fact]
    public void Compare_WhenAllowedRootAccessIsRaised_ShouldReturnPrivilegeExpansion()
    {
        var result = SshProfileAuthorizationEvaluator.Compare(
            CreateSnapshot(roots: [new("/var/www", AllowedRootAccess.Read)]),
            CreateSnapshot(roots: [new("/var/www", AllowedRootAccess.Read | AllowedRootAccess.Write)]));

        result.Kind.Should().Be(SshProfileAuthorizationChangeKind.PrivilegeExpansion);
        result.ChangedFields.Should().Contain("AllowedRoots");
    }

    [Fact]
    public void Compare_WhenLinuxPathCaseChanges_ShouldFailClosedAsPrivilegeExpansion()
    {
        var result = SshProfileAuthorizationEvaluator.Compare(
            CreateSnapshot(roots: [new("/var/www/App", AllowedRootAccess.Read)]),
            CreateSnapshot(roots: [new("/var/www/app", AllowedRootAccess.Read)]));

        result.Kind.Should().Be(SshProfileAuthorizationChangeKind.PrivilegeExpansion);
    }

    [Fact]
    public void Compare_WhenSpecialPathDenyIsRelaxed_ShouldReturnPrivilegeExpansion()
    {
        var result = SshProfileAuthorizationEvaluator.Compare(
            CreateSnapshot(specialPaths: [new("**/.env", SpecialPathAction.Deny)]),
            CreateSnapshot(specialPaths: [new("**/.env", SpecialPathAction.Confirm)]));

        result.Kind.Should().Be(SshProfileAuthorizationChangeKind.PrivilegeExpansion);
        result.ChangedFields.Should().Contain("SpecialPaths");
    }

    [Fact]
    public void Compare_WhenCapabilitiesAreRemoved_ShouldReturnPrivilegeReduction()
    {
        var result = SshProfileAuthorizationEvaluator.Compare(
            CreateSnapshot(capabilities: ["allow-a", "allow-b"]),
            CreateSnapshot(capabilities: ["allow-a"]));

        result.Kind.Should().Be(SshProfileAuthorizationChangeKind.PrivilegeReduction);
    }

    [Fact]
    public void Compare_WhenOnlyCollectionOrderDiffers_ShouldReturnNone()
    {
        var result = SshProfileAuthorizationEvaluator.Compare(
            CreateSnapshot(capabilities: ["allow-a", "allow-b"], roles: ["safe", "web"]),
            CreateSnapshot(capabilities: ["allow-b", "allow-a"], roles: ["web", "safe"]));

        result.Kind.Should().Be(SshProfileAuthorizationChangeKind.None);
    }

    [Fact]
    public void ReloadTrustedProfile_WhenPrivilegeExpansionIsNotApproved_ShouldKeepLastGoodCatalog()
    {
        var directory = CreateTempDirectory();
        var profilePath = Path.Combine(directory, "vps01.json");
        var trustStorePath = Path.Combine(directory, "mcp_trusted_store.dat");
        File.WriteAllText(profilePath, CreateProfileJson("ReadOnly"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);
        File.WriteAllText(profilePath, CreateProfileJson("Expert"));

        var result = catalog.ReloadTrustedProfile("vps01");

        result.Success.Should().BeFalse();
        result.Status.Should().Be("profile-privilege-expansion");
        result.AuthorizationChange.Should().Be(SshProfileAuthorizationChangeKind.PrivilegeExpansion);
        result.ChangedFields.Should().Contain("Mode");
        catalog.TryGet("vps01", out var active).Should().BeTrue();
        active.Mode.Should().Be(KelpiePolicyMode.ReadOnly);
    }

    [Fact]
    public void ReloadTrustedProfile_WhenPrivilegeExpansionIsApproved_ShouldUpdateCatalogAndBaseline()
    {
        var directory = CreateTempDirectory();
        var profilePath = Path.Combine(directory, "vps01.json");
        var trustStorePath = Path.Combine(directory, "mcp_trusted_store.dat");
        File.WriteAllText(profilePath, CreateProfileJson("ReadOnly"));
        var catalog = new ReloadingSshConnectionProfileCatalog(directory, trustStorePath, []);
        File.WriteAllText(profilePath, CreateProfileJson("Expert"));

        var result = catalog.ReloadTrustedProfile("vps01", approvePrivilegeExpansion: true);

        result.Success.Should().BeTrue();
        result.AuthorizationChange.Should().Be(SshProfileAuthorizationChangeKind.PrivilegeExpansion);
        catalog.TryGet("vps01", out var active).Should().BeTrue();
        active.Mode.Should().Be(KelpiePolicyMode.Expert);
    }

    private static SshProfileAuthorizationSnapshot CreateSnapshot(
        KelpiePolicyMode mode = KelpiePolicyMode.Safe,
        IReadOnlyCollection<string>? capabilities = null,
        IReadOnlyCollection<string>? roles = null,
        IReadOnlyCollection<SshProfileAllowedRootSnapshot>? roots = null,
        IReadOnlyCollection<SshProfileSpecialPathSnapshot>? specialPaths = null)
    {
        return new SshProfileAuthorizationSnapshot(
            "example.com",
            22,
            "deploy",
            "privatekey",
            "key:id_ed25519",
            mode,
            capabilities ?? [],
            roles ?? [],
            roots ?? [],
            specialPaths ?? [],
            []);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "kelpie-auth-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateProfileJson(string mode)
    {
        return $$"""
        {
          "Host": { "Address": "example.com", "Port": 22 },
          "Auth": { "Method": "privateKey", "PrivateKeyFile": "id_ed25519" },
          "DefaultUser": "deploy",
          "Users": { "deploy": "{{mode}}" },
          "Platform": { "OsFamily": "debian" }
        }
        """;
    }
}
