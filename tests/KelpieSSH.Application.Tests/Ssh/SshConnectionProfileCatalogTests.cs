using FluentAssertions;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Application.Tests.Logging;

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
