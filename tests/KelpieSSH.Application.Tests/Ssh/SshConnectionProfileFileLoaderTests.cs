using FluentAssertions;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshConnectionProfileFileLoaderTests
{
    [Fact]
    public void LoadFile_ShouldUseFileNameAsProfileName()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "172.53.54.1.json");
        File.WriteAllText(filePath, CreateProfileJson("keys/id_ed25519"));

        var profile = SshConnectionProfileFileLoader.LoadFile(filePath);

        profile.Name.Should().Be("172.53.54.1");
        profile.PrivateKeyPath.Should().Be(Path.GetFullPath(Path.Combine(directory, "keys/id_ed25519")));
        profile.Mode.Should().Be(KelpiePolicyMode.Maintenance);
        profile.Capabilities.Allows(KelpiePolicyNames.AllowListPackage).Should().BeTrue();
        profile.AllowedRoots.Should().Equal("/var/www");
    }

    [Fact]
    public void LoadDirectory_ShouldLoadJsonProfilesInDirectory()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(Path.Combine(directory, "vps02.json"), CreateProfileJson("keys/vps02"));
        File.WriteAllText(Path.Combine(directory, "vps01.json"), CreateProfileJson("keys/vps01"));

        var profiles = SshConnectionProfileFileLoader.LoadDirectory(directory);

        profiles.Select(profile => profile.Name).Should().Equal("vps01", "vps02");
    }

    [Fact]
    public void LoadDirectory_ShouldReturnEmptyCollectionWhenDirectoryDoesNotExist()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var profiles = SshConnectionProfileFileLoader.LoadDirectory(directory);

        profiles.Should().BeEmpty();
    }

    [Fact]
    public void LoadFile_ShouldReadAllowedRootAccessAndSpecialPaths()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithPathCapabilities("keys/id_ed25519"));

        var profile = SshConnectionProfileFileLoader.LoadFile(filePath);

        profile.AllowedRootRules.Should().ContainSingle(rule =>
            rule.Path == "/var/www" && rule.Access == (AllowedRootAccess.Read | AllowedRootAccess.Write));
        profile.AllowedRootRules.Should().ContainSingle(rule =>
            rule.Path == "/var/log" && rule.Access == AllowedRootAccess.Read);
        profile.AllowedRootRules.Should().ContainSingle(rule =>
            rule.Path == "/tmp" && rule.Access == AllowedRootAccess.All);
        profile.SpecialPaths.Should().ContainSingle(rule =>
            rule.Pattern == "**/.env" && rule.Action == SpecialPathAction.Deny);
    }

    [Fact]
    public void LoadFile_ShouldReadNamedRightsForAllowedRoots()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithNamedRights("keys/id_ed25519"));

        var profile = SshConnectionProfileFileLoader.LoadFile(filePath);

        profile.AllowedRootRules.Should().ContainSingle(rule =>
            rule.Path == "/var/www" && rule.Access == (AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.Write | AllowedRootAccess.Import | AllowedRootAccess.CD));
        profile.AllowedRootRules.Should().ContainSingle(rule =>
            rule.Path == "/var/log" && rule.Access == (AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.CD));
        profile.AllowedRootRules.Should().ContainSingle(rule =>
            rule.Path == "/tmp" && rule.Access == AllowedRootAccess.All);
    }

    [Fact]
    public void LoadFile_ShouldRejectCircularNamedRights()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithCircularNamedRights("keys/id_ed25519"));

        var action = () => SshConnectionProfileFileLoader.LoadFile(filePath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH rights reference is circular: $WebDeploy");
    }

    [Fact]
    public void LoadFile_ShouldRejectSystemRightOverride()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithSystemRightOverride("keys/id_ed25519"));

        var action = () => SshConnectionProfileFileLoader.LoadFile(filePath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH system right cannot be overridden: $ReadOnly");
    }

    [Fact]
    public void LoadFile_ShouldRejectUnprefixedRightName()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithUnprefixedRightName("keys/id_ed25519"));

        var action = () => SshConnectionProfileFileLoader.LoadFile(filePath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH rights names must start with '$': WebDeploy");
    }

    [Fact]
    public void LoadFile_ShouldRejectUnprefixedRightReference()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithUnprefixedRightReference("keys/id_ed25519"));

        var action = () => SshConnectionProfileFileLoader.LoadFile(filePath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unknown SSH allowed root access: ReadWrite");
    }

    [Fact]
    public void LoadFile_ShouldRejectUnprefixedAccessFlag()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithUnprefixedAccessFlag("keys/id_ed25519"));

        var action = () => SshConnectionProfileFileLoader.LoadFile(filePath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Unknown SSH allowed root access: Read");
    }

    [Fact]
    public void LoadFile_ShouldSelectDefaultUserFromUsers()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithUsers("keys/shared"));

        var profile = SshConnectionProfileFileLoader.LoadFile(filePath);

        profile.UserName.Should().Be("readonly");
        profile.PrivateKeyPath.Should().Be(Path.GetFullPath(Path.Combine(directory, "keys/shared")));
        profile.Mode.Should().Be(KelpiePolicyMode.ReadOnly);
        profile.Users.Select(user => user.UserName).Should().Equal("deploy", "readonly");
        profile.AllowedRootRules.Should().ContainSingle(rule =>
            rule.Path == "/var/log" && rule.Access == AllowedRootAccess.Read);
    }

    [Fact]
    public void LoadFile_ShouldReadLegacyUserArray()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithUserArray("keys/shared"));

        var profile = SshConnectionProfileFileLoader.LoadFile(filePath);

        profile.UserName.Should().Be("readonly");
        profile.Users.Select(user => user.UserName).Should().Equal("deploy", "readonly");
    }

    [Fact]
    public void LoadFile_ShouldReadUserRolesAndServiceSettings()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithUserRolesAndServices("keys/shared"));

        var profile = SshConnectionProfileFileLoader.LoadFile(filePath);

        profile.UserName.Should().Be("hoge");
        profile.Mode.Should().Be(KelpiePolicyMode.Safe);
        profile.Roles.Should().Contain(KelpieRoleNames.Safe);
        profile.Roles.Should().Contain(KelpieRoleNames.WebAdmin);
        profile.Services.Nginx.Should().NotBeNull();
        profile.Services.Nginx!.User.Should().Be("user01");
        profile.Services.Nginx.Group.Should().Be("group01");
        profile.Services.Nginx.Port.Should().Be(8081);
        profile.Services.Nginx.Root.Should().Be("/var/www/myRoot");

        var webUser = profile.Users.Single(user => user.UserName == "alma");
        webUser.Mode.Should().Be(KelpiePolicyMode.Expert);
        webUser.Roles.Should().Contain(KelpieRoleNames.Expert);
        webUser.Roles.Should().Contain(KelpieRoleNames.WebUser);
        webUser.AllowedRootRules.Should().ContainSingle(rule =>
            rule.Path == "/var/www/myRoot"
            && rule.Access.HasFlag(AllowedRootAccess.Read)
            && rule.Access.HasFlag(AllowedRootAccess.Write)
            && rule.Access.HasFlag(AllowedRootAccess.List)
            && rule.Access.HasFlag(AllowedRootAccess.CD));
    }

    [Fact]
    public void LoadFile_ShouldReadWebPublicSites()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithWebPublicSites("keys/id_ed25519"));

        var profile = SshConnectionProfileFileLoader.LoadFile(filePath);

        profile.WebPublicSites.Should().ContainSingle(site =>
            site.SiteKey == "default"
            && site.DisplayName == "Default Web Site"
            && site.RootPath == "/var/www/html"
            && site.CreateDirectories
            && site.MaxReadBytes == 1048576
            && site.MaxWriteBytes == 2097152
            && site.AllowedExtensions.Contains(".html")
            && site.WritableExecutableExtensions.Contains(".php")
            && site.AllowedContentTypes.Any(rule => rule.ContentType == "text/html" && rule.Access.HasFlag(AllowedRootAccess.Write))
            && site.AllowedContentTypes.Any(rule => rule.ContentType == "application/zip" && rule.Access.HasFlag(AllowedRootAccess.Write))
            && site.AllowedFiles.Any(rule => rule.Pattern == ".zip" && rule.Access.HasFlag(AllowedRootAccess.Write))
            && site.AllowedFiles.Any(rule => rule.Pattern == "/downloads/*.exe" && rule.Access == (AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.CD)));
    }

    [Fact]
    public void LoadFile_ShouldReadUserWebPublicSites()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithUserWebPublicSites("keys/id_ed25519"));

        var profile = SshConnectionProfileFileLoader.LoadFile(filePath);

        profile.UserName.Should().Be("deploy");
        profile.WebPublicSites.Should().ContainSingle(site =>
            site.SiteKey == "default"
            && site.RootPath == "/var/www/html"
            && site.WritableExecutableExtensions.Contains(".php"));
    }

    [Fact]
    public void LoadFile_ShouldReadEnvironmentValues()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithEnvironmentValues("keys/id_ed25519"));

        var profile = SshConnectionProfileFileLoader.LoadFile(filePath);

        profile.Capabilities.Allows(KelpiePolicyNames.AllowPeekEnvironmentKeys).Should().BeTrue();
        profile.Capabilities.Allows(KelpiePolicyNames.AllowPeekEnvironmentValues).Should().BeTrue();
        profile.Capabilities.Allows(KelpiePolicyNames.AllowSetEnvironmentValues).Should().BeTrue();
        profile.EnvironmentValues.Should().ContainSingle(rule =>
            rule.Key == "PATH"
            && rule.Access.HasFlag(EnvironmentValueAccess.PeekCommon)
            && rule.Access.HasFlag(EnvironmentValueAccess.SetCommon)
            && rule.Access.HasFlag(EnvironmentValueAccess.NoLog));
        profile.EnvironmentValues.Should().ContainSingle(rule =>
            rule.Key == "MY_SECRET_KEY" && rule.Access == EnvironmentValueAccess.Hidden);
    }

    [Fact]
    public void LoadFile_ShouldRejectMisspelledWebPublicAllowedFiles()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(filePath, CreateProfileJsonWithMisspelledWebPublicAllowedFiles("keys/id_ed25519"));

        var action = () => SshConnectionProfileFileLoader.LoadFile(filePath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH web public site file rules must use AllowedFiles.");
    }

    [Fact]
    public void LoadFile_ShouldRejectInvalidWritableExecutableExtension()
    {
        var directory = CreateTempDirectory();
        var filePath = Path.Combine(directory, "vps01.json");
        File.WriteAllText(
            filePath,
            CreateProfileJsonWithWebPublicSites("keys/id_ed25519")
                .Replace("\"WritableExecutableExtensions\": [\".php\"]", "\"WritableExecutableExtensions\": [\"*.php\"]", StringComparison.Ordinal));

        var action = () => SshConnectionProfileFileLoader.LoadFile(filePath);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("SSH web public writable executable extensions must start with a dot.");
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kelpie-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateProfileJson(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Connection": {
            "TimeoutSeconds": 10
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "Mode": "Maintenance",
          "Capabilities": [
            "AllowListPackage"
          ],
          "AllowedRoots": [
            "/var/www"
          ]
        }
        """;
    }

    private static string CreateProfileJsonWithPathCapabilities(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "AllowedRoots": {
            "/var/www": "@Read|@Write",
            "/var/log": "@Read",
            "/tmp": "$ALL"
          },
          "SpecialPaths": {
            "**/.env": "Deny"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithUsers(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "DefaultUser": "readonly",
          "Users": {
            "deploy": {
              "Mode": "Safe",
              "AllowedRoots": {
                "/var/www": "@Read|@Write"
              }
            },
            "readonly": {
              "Mode": "ReadOnly",
              "AllowedRoots": {
                "/var/log": "@Read"
              }
            }
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithNamedRights(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "Rights": {
            "$WebDeploy": "$ReadWrite|@Import",
            "$LogRead": "$ReadOnly"
          },
          "AllowedRoots": {
            "/var/www": "$WebDeploy",
            "/var/log": "$LogRead",
            "/tmp": "$ALL"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithCircularNamedRights(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "Rights": {
            "$WebDeploy": "$LogRead|@Write",
            "$LogRead": "$WebDeploy|@Read"
          },
          "AllowedRoots": {
            "/var/www": "$WebDeploy"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithSystemRightOverride(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "Rights": {
            "$ReadOnly": "@Read"
          },
          "AllowedRoots": {
            "/var/www": "$ReadOnly"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithUnprefixedRightName(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "Rights": {
            "WebDeploy": "$ReadWrite"
          },
          "AllowedRoots": {
            "/var/www": "$WebDeploy"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithUnprefixedRightReference(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "AllowedRoots": {
            "/var/www": "ReadWrite"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithUnprefixedAccessFlag(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "AllowedRoots": {
            "/var/www": "Read|@Write"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithUserArray(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "DefaultUser": "readonly",
          "Users": [
            {
              "UserName": "deploy",
              "Mode": "Safe"
            },
            {
              "UserName": "readonly",
              "Mode": "ReadOnly"
            }
          ],
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithUserRolesAndServices(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "DefaultUser": "hoge",
          "Users": {
            "alma": "Expert|WebUser",
            "hoge": "Safe|WebAdmin"
          },
          "Services": {
            "Nginx": {
              "User": "user01",
              "Group": "group01",
              "Port": 8081,
              "Root": "/var/www/myRoot"
            }
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithWebPublicSites(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "WebPublicSites": {
            "default": {
              "DisplayName": "Default Web Site",
              "Root": "/var/www/html",
              "AllowedExtensions": [".html", ".png"],
              "WritableExecutableExtensions": [".php"],
              "AllowedContentTypes": {
                "text/html": "$ReadWrite",
                "image/png": "$ReadOnly"
              },
              "AllowedFiles": {
                "file:.zip": "$ReadWrite",
                "file:/downloads/*.exe": "$ReadOnly",
                "mime:application/zip": "$ReadWrite"
              },
              "CreateDirectories": true,
              "MaxReadBytes": 1048576,
              "MaxWriteBytes": 2097152
            }
          }
        }
        """;
    }

    private static string CreateProfileJsonWithUserWebPublicSites(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Auth": {
            "Method": "privateKey",
            "PrivateKeyFile": "{{privateKeyPath}}"
          },
          "DefaultUser": "deploy",
          "Users": {
            "deploy": {
              "Mode": "Maintenance|WebUser|WebAdmin",
              "WebPublicSites": {
                "default": {
                  "Root": "/var/www/html",
                  "WritableExecutableExtensions": [".php"]
                }
              }
            }
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithEnvironmentValues(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "Capabilities": "AllowPeekEnvironmentKeys|AllowPeekEnvironmentValues|AllowSetEnvironmentValues",
          "EnvironmentValues": {
            "PATH": "Common|NoLog",
            "MY_SECRET_KEY": "Hidden"
          }
        }
        """;
    }

    private static string CreateProfileJsonWithMisspelledWebPublicAllowedFiles(string privateKeyPath)
    {
        return $$"""
        {
          "Host": {
            "Address": "example.invalid",
            "Port": 22
          },
          "Authentication": {
            "UserName": "deploy",
            "Method": "privateKey",
            "PrivateKeyPath": "{{privateKeyPath}}"
          },
          "Platform": {
            "OsFamily": "debian",
            "PackageManager": "apt"
          },
          "WebPublicSites": {
            "default": {
              "DisplayName": "Default Web Site",
              "Root": "/var/www/html",
              "AllowdFoles": {
                "*.zip": "$ReadWrite"
              }
            }
          }
        }
        """;
    }
}
