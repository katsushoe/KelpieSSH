using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using KelpieSSH.Application.Ssh;
using NSubstitute;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class SshProfileEditServiceTests
{
    [Fact]
    public void SetScalar_ShouldUpdateStringAndKeepOtherFields()
    {
        using var profile = TestProfile.Create();
        var service = CreateService();

        var result = service.SetScalar(profile.Path, "Host.Address", "demo.example");

        result.Success.Should().BeTrue();
        var node = ReadProfile(profile.Path);
        node["Host"]!["Address"]!.GetValue<string>().Should().Be("demo.example");
        node["Auth"]!["PrivateKeyFile"]!.GetValue<string>().Should().Be("demo_ed25519");
        node["Users"]!["kelpie"]!["SpecialPaths"]!["**/.env"]!.GetValue<string>().Should().Be("Deny");
    }

    [Fact]
    public void SetScalar_ShouldConvertHostPortToInt()
    {
        using var profile = TestProfile.Create();
        var service = CreateService();

        var result = service.SetScalar(profile.Path, "Host.Port", "2224");

        result.Success.Should().BeTrue();
        ReadProfile(profile.Path)["Host"]!["Port"]!.GetValue<int>().Should().Be(2224);
    }

    [Fact]
    public void SetHostKeyFingerprint_ShouldSetNormalizedSha256Fingerprint()
    {
        using var profile = TestProfile.Create();
        var service = CreateService();

        var result = service.SetHostKeyFingerprint(profile.Path, "abc123=");

        result.Success.Should().BeTrue();
        var node = ReadProfile(profile.Path);
        node["Host"]!["HostKeyFingerprintSha256"]!.GetValue<string>().Should().Be("SHA256:abc123");
        SshConnectionProfileFileLoader.LoadFile(profile.Path).HostKeyFingerprintSha256.Should().Be("SHA256:abc123");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("abc")]
    public void SetScalar_ShouldRejectInvalidHostPortAndKeepOriginal(string port)
    {
        using var profile = TestProfile.Create();
        var original = File.ReadAllText(profile.Path);
        var service = CreateService();

        var result = service.SetScalar(profile.Path, "Host.Port", port);

        result.Success.Should().BeFalse();
        File.ReadAllText(profile.Path).Should().Be(original);
    }

    [Fact]
    public void SetScalar_ShouldAcceptRoleExpressionForUserMode()
    {
        using var profile = TestProfile.Create();
        var service = CreateService();

        var result = service.SetScalar(profile.Path, "Users.kelpie.Mode", "Maintenance|WebUser|WebAdmin");

        result.Success.Should().BeTrue();
        var loaded = SshConnectionProfileFileLoader.LoadFile(profile.Path);
        loaded.Roles.Should().Contain(KelpieRoleNames.Maintenance);
        loaded.Roles.Should().Contain(KelpieRoleNames.WebUser);
        loaded.Roles.Should().Contain(KelpieRoleNames.WebAdmin);
        loaded.Mode.Should().Be(KelpiePolicyMode.Maintenance);
    }

    [Fact]
    public void SetScalar_ShouldRejectUnknownRoleAndKeepOriginal()
    {
        using var profile = TestProfile.Create();
        var original = File.ReadAllText(profile.Path);
        var service = CreateService();

        var result = service.SetScalar(profile.Path, "Users.kelpie.Mode", "Maintenance|UnknownRole");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unknown SSH role");
        File.ReadAllText(profile.Path).Should().Be(original);
    }

    [Fact]
    public void AddRootAndRemoveRoot_ShouldUpdateAllowedRootsIdempotently()
    {
        using var profile = TestProfile.Create();
        var service = CreateService();

        service.AddRoot(profile.Path, "/etc/nginx", "ReadWrite").Success.Should().BeTrue();
        service.AddRoot(profile.Path, "/etc/nginx", "$ReadWrite").Success.Should().BeTrue();

        var node = ReadProfile(profile.Path);
        node["Users"]!["kelpie"]!["AllowedRoots"]!["/etc/nginx"]!.GetValue<string>().Should().Be("$ReadWrite");

        service.RemoveRoot(profile.Path, "/etc/nginx").Success.Should().BeTrue();
        service.RemoveRoot(profile.Path, "/etc/nginx").Success.Should().BeTrue();
        ReadProfile(profile.Path)["Users"]!["kelpie"]!["AllowedRoots"]!["/etc/nginx"].Should().BeNull();
    }

    [Fact]
    public void AddRoot_ShouldRejectInvalidAccessAndKeepOriginal()
    {
        using var profile = TestProfile.Create();
        var original = File.ReadAllText(profile.Path);
        var service = CreateService();

        var result = service.AddRoot(profile.Path, "/etc/nginx", "WriteOnly");

        result.Success.Should().BeFalse();
        File.ReadAllText(profile.Path).Should().Be(original);
    }

    [Fact]
    public void AddDeny_ShouldSupportPatternWithDot()
    {
        using var profile = TestProfile.Create();
        var service = CreateService();

        var result = service.AddDeny(profile.Path, "**/.htpasswd");

        result.Success.Should().BeTrue();
        ReadProfile(profile.Path)["Users"]!["kelpie"]!["SpecialPaths"]!["**/.htpasswd"]!.GetValue<string>().Should().Be("Deny");
    }

    [Fact]
    public void RemoveDeny_ShouldRemovePatternIdempotently()
    {
        using var profile = TestProfile.Create();
        var service = CreateService();

        service.AddDeny(profile.Path, "**/.htpasswd").Success.Should().BeTrue();
        service.RemoveDeny(profile.Path, "**/.htpasswd").Success.Should().BeTrue();
        service.RemoveDeny(profile.Path, "**/.htpasswd").Success.Should().BeTrue();

        ReadProfile(profile.Path)["Users"]!["kelpie"]!["SpecialPaths"]!["**/.htpasswd"].Should().BeNull();
    }

    [Fact]
    public void SetScalar_ShouldRejectNonScalarDotPath()
    {
        using var profile = TestProfile.Create();
        var service = CreateService();

        var result = service.SetScalar(profile.Path, "Users.kelpie.AllowedRoots", "/etc/nginx");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("add-root");
    }

    [Fact]
    public void EditWithEditor_ShouldResolveEditorCommandByPriority()
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["KELPIE_EDITOR"] = "nano",
            ["VISUAL"] = "code --wait",
            ["EDITOR"] = "vi",
        };
        string? GetEnv(string key) => env.TryGetValue(key, out var value) ? value : null;

        ProfileEditorCommandResolver.Resolve("notepad", GetEnv, isWindows: true).Should().Be("notepad");
        ProfileEditorCommandResolver.Resolve("vscode --wait", GetEnv, isWindows: true).Should().Be("code --wait");
        ProfileEditorCommandResolver.Resolve("\"vscode\" --wait", GetEnv, isWindows: true).Should().Be("code --wait");
        ProfileEditorCommandResolver.Resolve("", GetEnv, isWindows: true).Should().Be("nano");
        env["KELPIE_EDITOR"] = "";
        ProfileEditorCommandResolver.Resolve("", GetEnv, isWindows: true).Should().Be("code --wait");
        env["VISUAL"] = "";
        ProfileEditorCommandResolver.Resolve("", GetEnv, isWindows: false).Should().Be("vi");
        env["EDITOR"] = "";
        ProfileEditorCommandResolver.Resolve("", GetEnv, isWindows: true).Should().Be("notepad");
        ProfileEditorCommandResolver.Resolve("", GetEnv, isWindows: false).Should().Be("vi");
    }

    [Fact]
    public void EditWithEditor_ShouldResolveVscodeAliasFromPath()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kelpie-code-path-{Guid.NewGuid():N}", "VS Code Bin");
        Directory.CreateDirectory(directory);
        var codePath = System.IO.Path.Combine(directory, "code.cmd");
        File.WriteAllText(codePath, string.Empty);
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = directory,
            ["PATHEXT"] = ".EXE;.CMD",
        };
        string? GetEnv(string key) => env.TryGetValue(key, out var value) ? value : null;

        ProfileEditorCommandResolver.Resolve("vscode --wait", GetEnv, isWindows: true)
            .Should().Be($"\"{codePath}\" --wait");
        ProfileEditorCommandResolver.Resolve("\"vscode\" --wait", GetEnv, isWindows: true)
            .Should().Be($"\"{codePath}\" --wait");
    }

    [Fact]
    public void EditWithEditor_ShouldLaunchCmdEditorPath()
    {
        using var profile = TestProfile.Create();
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kelpie-cmd-editor-{Guid.NewGuid():N}", "Editor Bin");
        Directory.CreateDirectory(directory);
        var editorPath = System.IO.Path.Combine(directory, "editor.cmd");
        File.WriteAllText(editorPath, "@exit /b 0");
        var launcher = new ProcessEditorLauncher();

        var result = launcher.Launch($"\"{editorPath}\"", profile.Path);

        result.Success.Should().BeTrue(result.ErrorMessage);
    }

    [Fact]
    public void EditWithEditor_ShouldRestoreOriginalWhenEditorExitsWithError()
    {
        using var profile = TestProfile.Create();
        var original = File.ReadAllText(profile.Path);
        var launcher = Substitute.For<IEditorLauncher>();
        launcher.Launch("fake", profile.Path).Returns(EditorLaunchResult.FromExitCode(7));
        var service = new SshProfileEditService(launcher);

        var result = service.EditWithEditor(profile.Path, "fake", _ => ProfileEditRecoveryAction.Retry);

        result.Success.Should().BeFalse();
        NormalizeLineEndings(File.ReadAllText(profile.Path)).Should().Be(NormalizeLineEndings(original));
        launcher.Received(1).Launch("fake", profile.Path);
    }

    [Fact]
    public void EditWithEditor_ShouldRestoreOriginalWhenValidationFailsAndUserAborts()
    {
        using var profile = TestProfile.Create();
        var original = File.ReadAllText(profile.Path);
        var launcher = Substitute.For<IEditorLauncher>();
        launcher.Launch("fake", profile.Path).Returns(_ =>
        {
            File.WriteAllText(profile.Path, "{ invalid", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return EditorLaunchResult.FromExitCode(0);
        });
        var service = new SshProfileEditService(launcher);

        var result = service.EditWithEditor(profile.Path, "fake", _ => ProfileEditRecoveryAction.Abort);

        result.Success.Should().BeFalse();
        NormalizeLineEndings(File.ReadAllText(profile.Path)).Should().Be(NormalizeLineEndings(original));
    }

    private static SshProfileEditService CreateService()
    {
        var launcher = Substitute.For<IEditorLauncher>();
        return new SshProfileEditService(launcher);
    }

    private static JsonObject ReadProfile(string path)
    {
        return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private sealed class TestProfile : IDisposable
    {
        private TestProfile(string directory, string path)
        {
            Directory = directory;
            Path = path;
        }

        public string Directory { get; }

        public string Path { get; }

        public static TestProfile Create()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kelpie-profile-edit-{Guid.NewGuid():N}", "profiles");
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "demo.json");
            File.WriteAllText(
                path,
                """
                {
                  "Host": {
                    "Address": "127.0.0.1",
                    "Port": 22
                  },
                  "Auth": {
                    "Method": "privateKey",
                    "PrivateKeyFile": "demo_ed25519"
                  },
                  "DefaultUser": "kelpie",
                  "Users": {
                    "kelpie": {
                      "Mode": "Safe",
                      "AllowedRoots": {
                        "/var/log": "$ReadOnly"
                      },
                      "SpecialPaths": {
                        "**/.env": "Deny"
                      }
                    }
                  },
                  "Platform": {
                    "OsFamily": "debian"
                  }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new TestProfile(System.IO.Path.GetDirectoryName(directory)!, path);
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }
}
