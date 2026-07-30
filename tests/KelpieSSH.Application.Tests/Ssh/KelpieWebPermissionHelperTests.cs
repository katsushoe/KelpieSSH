using System.Text;
using FluentAssertions;
using KelpieWebPermissionHelper;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class KelpieWebPermissionHelperTests
{
    [Fact]
    public void Run_ShouldPrintVersion()
    {
        var operations = new FakeUnixPermissionOperations();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            ["--version"],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
        output.ToString().Should().StartWith("kelpie-web-permission-helper 0.2.1.1");
    }

    [Fact]
    public void Run_ShouldChangeOwnerInsideSiteRoot()
    {
        var operations = new FakeUnixPermissionOperations();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "change-owner",
                Encode("/var/www/html"),
                Encode("/"),
                Encode("deploy"),
                Encode("www-data"),
                "0",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
        output.ToString().Should().Contain("\"ResolvedPath\":\"/var/www/html\"");
        operations.OwnerChanges.Should().ContainSingle().Which.Should().Be(("/var/www/html", 1000u, 33u));
    }

    [Fact]
    public void Run_ShouldChangeModeInsideSiteRoot()
    {
        var operations = new FakeUnixPermissionOperations();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "change-mode",
                Encode("/var/www/html"),
                Encode("/my_dir"),
                "775",
                "0",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
        output.ToString().Should().Contain("\"ResolvedPath\":\"/var/www/html/my_dir\"");
        operations.ModeChanges.Should().ContainSingle().Which.Should().Be(("/var/www/html/my_dir", 0x1FDu));
    }

    [Fact]
    public void Run_ShouldChangeOwnerRecursivelyInsideSiteRoot()
    {
        var operations = new FakeUnixPermissionOperations
        {
            Directories =
            {
                "/var/www/html/assets",
                "/var/www/html/assets/css",
            },
            Files =
            {
                "/var/www/html/assets/app.js",
                "/var/www/html/assets/css/site.css",
            },
            SymbolicLinks =
            {
                "/var/www/html/assets/link",
            },
        };
        operations.DirectoryEntries["/var/www/html/assets"] =
        [
            "/var/www/html/assets/app.js",
            "/var/www/html/assets/css",
            "/var/www/html/assets/link",
        ];
        operations.DirectoryEntries["/var/www/html/assets/css"] =
        [
            "/var/www/html/assets/css/site.css",
        ];
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "change-owner",
                Encode("/var/www/html"),
                Encode("/assets"),
                Encode("deploy"),
                Encode("www-data"),
                "1",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        operations.OwnerChanges.Should().Equal(
            ("/var/www/html/assets/app.js", 1000u, 33u),
            ("/var/www/html/assets/css/site.css", 1000u, 33u),
            ("/var/www/html/assets/css", 1000u, 33u),
            ("/var/www/html/assets", 1000u, 33u));
        operations.OwnerChanges.Select(change => change.Path)
            .Should().NotContain("/var/www/html/assets/link");
    }

    [Fact]
    public void Run_ShouldChangeModeRecursivelyInsideSiteRoot()
    {
        var operations = new FakeUnixPermissionOperations
        {
            Directories =
            {
                "/var/www/html/assets",
            },
            Files =
            {
                "/var/www/html/assets/app.js",
            },
        };
        operations.DirectoryEntries["/var/www/html/assets"] =
        [
            "/var/www/html/assets/app.js",
        ];
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "change-mode",
                Encode("/var/www/html"),
                Encode("/assets"),
                "775",
                "1",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        operations.ModeChanges.Should().Equal(
            ("/var/www/html/assets/app.js", 0x1FDu),
            ("/var/www/html/assets", 0x1FDu));
    }

    [Fact]
    public void Run_ShouldWriteFileAtomicallyWithRequestedOwnerAndMode()
    {
        var operations = new FakeUnixPermissionOperations();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "write-file",
                Encode("/var/www/html"),
                Encode("/my_dir/sample.html"),
                Convert.ToBase64String(Encoding.UTF8.GetBytes("<h1>OK</h1>")),
                "1000",
                "1",
                Encode("deploy:www-data"),
                Encode("775"),
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
        output.ToString().Should().Contain("\"ResolvedPath\":\"/var/www/html/my_dir/sample.html\"");
        output.ToString().Should().Contain("\"Owner\":\"deploy\"");
        output.ToString().Should().Contain("\"Group\":\"www-data\"");
        output.ToString().Should().Contain("\"Mode\":\"775\"");
        var tempPath = operations.Writes.Should().ContainSingle().Which.Path;
        tempPath.Should().StartWith("/var/www/html/my_dir/.kelpie-upload-");
        operations.OwnerChanges.Should().ContainSingle().Which.Should().Be((tempPath, 1000u, 33u));
        operations.ModeChanges.Should().ContainSingle().Which.Should().Be((tempPath, 0x1FDu));
        operations.Moves.Should().ContainSingle().Which.Should().Be((tempPath, "/var/www/html/my_dir/sample.html"));
    }

    [Fact]
    public void Run_ShouldReadWriteFileContentFromStandardInputWhenContentArgumentIsDash()
    {
        var operations = new FakeUnixPermissionOperations();
        using var input = new StringReader(Convert.ToBase64String(Encoding.UTF8.GetBytes("TOKEN=secret\n")));
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "write-file",
                Encode("/var/www/html"),
                Encode("/my_dir/.env"),
                "-",
                "1000",
                "1",
                Encode("deploy:www-data"),
                Encode("600"),
            ],
            operations,
            input,
            output,
            error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
        var write = operations.Writes.Should().ContainSingle().Which;
        Encoding.UTF8.GetString(write.Data).Should().Be("TOKEN=secret\n");
        operations.Moves.Should().ContainSingle().Which.DestinationPath.Should().Be("/var/www/html/my_dir/.env");
    }

    [Fact]
    public void Run_ShouldWriteFileWithModeOnlyUsingSudoUserOwner()
    {
        var operations = new FakeUnixPermissionOperations();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "write-file",
                Encode("/var/www/html"),
                Encode("/my_dir/sample.html"),
                Convert.ToBase64String(Encoding.UTF8.GetBytes("OK")),
                "1000",
                "1",
                Encode(string.Empty),
                Encode("775"),
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        var tempPath = operations.Writes.Should().ContainSingle().Which.Path;
        operations.OwnerChanges.Should().ContainSingle().Which.Should().Be((tempPath, 1000u, 1000u));
        operations.ModeChanges.Should().ContainSingle().Which.Should().Be((tempPath, 0x1FDu));
    }

    [Fact]
    public void Run_ShouldWriteFileWithOwnerOnlyUsingDefaultMode()
    {
        var operations = new FakeUnixPermissionOperations();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "write-file",
                Encode("/var/www/html"),
                Encode("/my_dir/sample.html"),
                Convert.ToBase64String(Encoding.UTF8.GetBytes("OK")),
                "1000",
                "1",
                Encode("deploy:www-data"),
                Encode(string.Empty),
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        var tempPath = operations.Writes.Should().ContainSingle().Which.Path;
        operations.OwnerChanges.Should().ContainSingle().Which.Should().Be((tempPath, 1000u, 33u));
        operations.ModeChanges.Should().ContainSingle().Which.Should().Be((tempPath, 0x1A4u));
    }

    [Fact]
    public void Run_ShouldPreserveRootPermissionsAndCreateRollbackBackup()
    {
        const string target = "/var/www/_webadmin/index.php";
        var original = Encoding.UTF8.GetBytes("old");
        var replacement = Encoding.UTF8.GetBytes("new");
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)).ToLowerInvariant();
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/_webadmin/index.php", "Update");
        operations.Directories.Add("/var/www/_webadmin");
        operations.Files.Add(target);
        operations.FileContents[target] = original;
        operations.OwnerIds[target] = (0, 0);
        operations.Modes[target] = "644";
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "write-file",
                Encode("/var/www"),
                Encode("/_webadmin/index.php"),
                Convert.ToBase64String(replacement),
                "1000",
                "0",
                Encode(string.Empty),
                Encode(string.Empty),
                expectedHash,
                "1",
                "1",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
        operations.FileContents[target + ".kelpiebakup"].Should().Equal(original);
        operations.FileContents[target].Should().Equal(replacement);
        operations.OwnerChanges.Should().Contain(change => change.Uid == 0 && change.Gid == 0);
        output.ToString().Should().Contain("\"PermissionsPreserved\":true");
        output.ToString().Should().Contain("\"PreviousSha256\":\"" + expectedHash + "\"");
    }

    [Fact]
    public void Run_ShouldRejectExpectedHashMismatchWithoutWriting()
    {
        const string target = "/var/www/_webadmin/index.php";
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/_webadmin/index.php", "Update");
        operations.Directories.Add("/var/www/_webadmin");
        operations.Files.Add(target);
        operations.FileContents[target] = Encoding.UTF8.GetBytes("old");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "write-file", Encode("/var/www"), Encode("/_webadmin/index.php"),
                Convert.ToBase64String(Encoding.UTF8.GetBytes("new")), "1000", "0",
                Encode(string.Empty), Encode(string.Empty), new string('0', 64), "1", "1",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("expected SHA-256 does not match");
        operations.Writes.Should().BeEmpty();
        operations.FileContents[target].Should().Equal(Encoding.UTF8.GetBytes("old"));
    }

    [Fact]
    public void Run_ShouldRejectStreamedContentHashMismatchBeforeAtomicMove()
    {
        const string target = "/var/www/downloads/package.bin";
        var content = Encoding.UTF8.GetBytes("new-package");
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/downloads/package.bin", "Create");
        operations.Directories.Add("/var/www/downloads");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "write-file", Encode("/var/www"), Encode("/downloads/package.bin"),
                "-", "1000", "0", Encode(string.Empty), Encode(string.Empty),
                "-", "0", "0", new string('0', 64),
            ],
            operations,
            new StringReader(Convert.ToBase64String(content)),
            output,
            error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("uploaded content SHA-256 does not match");
        operations.Files.Should().NotContain(target);
        operations.Moves.Should().BeEmpty();
    }

    [Fact]
    public void Run_ShouldRollbackBackupAtomicallyWithOriginalPermissions()
    {
        const string target = "/var/www/_webadmin/index.php";
        var current = Encoding.UTF8.GetBytes("new");
        var original = Encoding.UTF8.GetBytes("old");
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(current)).ToLowerInvariant();
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/_webadmin/index.php", "Update");
        operations.Directories.Add("/var/www/_webadmin");
        operations.Files.UnionWith([target, target + ".kelpiebakup"]);
        operations.FileContents[target] = current;
        operations.FileContents[target + ".kelpiebakup"] = original;
        operations.OwnerIds[target + ".kelpiebakup"] = (0, 0);
        operations.Modes[target + ".kelpiebakup"] = "644";
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            ["rollback-file", Encode("/var/www"), Encode("/_webadmin/index.php"), expectedHash],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        operations.FileContents[target].Should().Equal(original);
        operations.Files.Should().NotContain(target + ".kelpiebakup");
        operations.OwnerChanges.Should().Contain(change => change.Uid == 0 && change.Gid == 0);
        operations.ModeChanges.Should().Contain(change => change.Mode == 0x1A4u);
    }

    [Fact]
    public void Run_ShouldCreateMissingPreservedFileAsRootOwnedNonExecutable()
    {
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/_webadmin/includes/manual-license-migration.php", "Create");
        operations.Directories.Add("/var/www/_webadmin/includes");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "write-file", Encode("/var/www"), Encode("/_webadmin/includes/manual-license-migration.php"),
                Convert.ToBase64String(Encoding.UTF8.GetBytes("new")), "1000", "0",
                Encode(string.Empty), Encode(string.Empty), "-", "1", "1",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        operations.OwnerChanges.Should().Contain(change => change.Uid == 0 && change.Gid == 0);
        operations.ModeChanges.Should().Contain(change => change.Mode == 0x1A4u);
        output.ToString().Should().Contain("\"Created\":true");
        output.ToString().Should().Contain("\"BackupPath\":\"\"");
    }

    [Fact]
    public void Run_ShouldRejectSymbolicLinkWriteTarget()
    {
        const string target = "/var/www/_webadmin/index.php";
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/_webadmin/index.php", "Update");
        operations.Directories.Add("/var/www/_webadmin");
        operations.Files.Add(target);
        operations.SymbolicLinks.Add(target);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "write-file", Encode("/var/www"), Encode("/_webadmin/index.php"),
                Convert.ToBase64String(Encoding.UTF8.GetBytes("new")), "1000", "0",
                Encode(string.Empty), Encode(string.Empty), "-", "1", "1",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("must not be a symbolic link");
        operations.Writes.Should().BeEmpty();
    }

    [Fact]
    public void Run_ShouldRejectPathOutsideSiteRoot()
    {
        var operations = new FakeUnixPermissionOperations
        {
            RealPathOverrides =
            {
                ["/var/www/html/link"] = "/etc",
            },
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "change-mode",
                Encode("/var/www/html"),
                Encode("/link"),
                "775",
                "0",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("ERROR: resolved path is outside web public root");
        operations.ModeChanges.Should().BeEmpty();
    }

    [Fact]
    public void Run_ShouldRejectSymbolicLinkTarget()
    {
        var operations = new FakeUnixPermissionOperations
        {
            SymbolicLinks =
            {
                "/var/www/html/link",
            },
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "change-mode",
                Encode("/var/www/html"),
                Encode("/link"),
                "775",
                "1",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("ERROR: requested path must not be a symbolic link");
        operations.ModeChanges.Should().BeEmpty();
    }

    [Fact]
    public void Run_ShouldRejectRootOwner()
    {
        var operations = new FakeUnixPermissionOperations();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "change-owner",
                Encode("/var/www/html"),
                Encode("/"),
                Encode("root"),
                Encode("www-data"),
                "0",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("ERROR: owner must not be root");
        operations.OwnerChanges.Should().BeEmpty();
    }

    [Fact]
    public void Run_ShouldRejectWorldWritableMode()
    {
        var operations = new FakeUnixPermissionOperations();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            [
                "change-mode",
                Encode("/var/www/html"),
                Encode("/"),
                "777",
                "0",
            ],
            operations,
            output,
            error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("ERROR: mode must not be world-writable");
        operations.ModeChanges.Should().BeEmpty();
    }

    [Fact]
    public void Policy_ShouldListExistingEntries()
    {
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/index.php", "Update");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionHelper.Run(
            ["policy", "list", Encode("/var/www")],
            operations,
            output,
            error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("\"/index.php\": \"Update\"");
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public void Policy_ShouldPreviewAndApplyAddWithoutRemovingExistingEntry()
    {
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/index.php", "Update");
        using var previewOutput = new StringWriter();

        PermissionHelper.Run(
            ["policy", "preview-add", Encode("/var/www"), Encode("/contact.php"), "Update"],
            operations,
            previewOutput,
            TextWriter.Null).Should().Be(0);
        using var preview = System.Text.Json.JsonDocument.Parse(previewOutput.ToString());
        var hash = preview.RootElement.GetProperty("currentSha256").GetString()!;

        var exitCode = PermissionHelper.Run(
            ["policy", "apply-add", Encode("/var/www"), Encode("/contact.php"), "Update", hash],
            operations,
            TextWriter.Null,
            TextWriter.Null);

        exitCode.Should().Be(0);
        var policy = Encoding.UTF8.GetString(
            operations.FileContents["/etc/kelpie/web-permission-helper-policy.json"]);
        policy.Should().Contain("\"/index.php\": \"Update\"");
        policy.Should().Contain("\"/contact.php\": \"Update\"");
        operations.Moves.Should().ContainSingle();
        operations.OwnerChanges.Should().Contain(change => change.Uid == 0 && change.Gid == 0);
        operations.ModeChanges.Should().Contain(change => change.Mode == Convert.ToUInt32("644", 8));
        var audit = Encoding.UTF8.GetString(
            operations.FileContents["/var/log/kelpie/web-policy-audit.jsonl"]);
        audit.Should().Contain("\"state\":\"confirmed\"").And.Contain("\"state\":\"completed\"");
        audit.Should().NotContain("\"Sites\"");
    }

    [Fact]
    public void Policy_ShouldRejectDuplicateAndConfirmationHashMismatch()
    {
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/index.php", "Update");
        using var error = new StringWriter();

        PermissionHelper.Run(
            ["policy", "preview-add", Encode("/var/www"), Encode("/index.php"), "Update"],
            operations,
            TextWriter.Null,
            error).Should().Be(1);
        error.ToString().Should().Contain("already exists");

        PermissionHelper.Run(
            ["policy", "apply-remove", Encode("/var/www"), Encode("/index.php"), new string('0', 64)],
            operations,
            TextWriter.Null,
            error).Should().Be(1);
        Encoding.UTF8.GetString(
            operations.FileContents["/etc/kelpie/web-permission-helper-policy.json"])
            .Should().Contain("/index.php");
    }

    [Fact]
    public void Policy_ShouldRemoveAndRollbackLatestBackup()
    {
        var operations = new FakeUnixPermissionOperations();
        ConfigureManagedPolicy(operations, "/index.php", "Update");
        using var previewOutput = new StringWriter();
        PermissionHelper.Run(
            ["policy", "preview-remove", Encode("/var/www"), Encode("/index.php")],
            operations,
            previewOutput,
            TextWriter.Null).Should().Be(0);
        using var preview = System.Text.Json.JsonDocument.Parse(previewOutput.ToString());
        var hash = preview.RootElement.GetProperty("currentSha256").GetString()!;
        PermissionHelper.Run(
            ["policy", "apply-remove", Encode("/var/www"), Encode("/index.php"), hash],
            operations,
            TextWriter.Null,
            TextWriter.Null).Should().Be(0);

        var backup = operations.Writes.Single(write =>
            write.Path.StartsWith("/etc/kelpie/.web-policy-backups/", StringComparison.Ordinal));
        operations.DirectoryEntries["/etc/kelpie/.web-policy-backups"] = [backup.Path];
        using var rollbackPreviewOutput = new StringWriter();
        PermissionHelper.Run(
            ["policy", "preview-rollback"],
            operations,
            rollbackPreviewOutput,
            TextWriter.Null).Should().Be(0);
        using var rollbackPreview = System.Text.Json.JsonDocument.Parse(rollbackPreviewOutput.ToString());

        PermissionHelper.Run(
            [
                "policy",
                "apply-rollback",
                rollbackPreview.RootElement.GetProperty("currentSha256").GetString()!,
                Encode(rollbackPreview.RootElement.GetProperty("backupName").GetString()!),
            ],
            operations,
            TextWriter.Null,
            TextWriter.Null).Should().Be(0);
        Encoding.UTF8.GetString(
            operations.FileContents["/etc/kelpie/web-permission-helper-policy.json"])
            .Should().Contain("/index.php");
    }

    private static string Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private static void ConfigureManagedPolicy(FakeUnixPermissionOperations operations, string path, string access)
    {
        const string policyPath = "/etc/kelpie/web-permission-helper-policy.json";
        operations.Files.Add(policyPath);
        operations.FileContents[policyPath] = Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                Sites = new Dictionary<string, object>
                {
                    ["/var/www"] = new { AllowedFiles = new Dictionary<string, string> { [path] = access } },
                },
            }));
        operations.OwnerIds[policyPath] = (0, 0);
        operations.Modes[policyPath] = "644";
    }

    private sealed class FakeUnixPermissionOperations : IUnixPermissionOperations
    {
        public Dictionary<string, string> RealPathOverrides { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Directories { get; } = new(StringComparer.Ordinal)
        {
            "/var/www/html",
            "/var/www/html/my_dir",
        };

        public HashSet<string> Files { get; } = new(StringComparer.Ordinal);

        public HashSet<string> SymbolicLinks { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, IReadOnlyList<string>> DirectoryEntries { get; } = new(StringComparer.Ordinal);

        public List<(string Path, byte[] Data)> Writes { get; } = [];

        public Dictionary<string, byte[]> FileContents { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, (uint Uid, uint Gid)> OwnerIds { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Modes { get; } = new(StringComparer.Ordinal);

        public List<(string SourcePath, string DestinationPath)> Moves { get; } = [];

        public List<(string Path, uint Uid, uint Gid)> OwnerChanges { get; } = [];

        public List<(string Path, uint Mode)> ModeChanges { get; } = [];

        public string RealPath(string path)
        {
            return RealPathOverrides.TryGetValue(path, out var resolved)
                ? resolved
                : path.TrimEnd('/');
        }

        public bool DirectoryExists(string path)
        {
            return Directories.Contains(path.TrimEnd('/'));
        }

        public void CreateDirectory(string path)
        {
            Directories.Add(path.TrimEnd('/'));
        }

        public bool FileExists(string path)
        {
            return Files.Contains(path);
        }

        public bool IsRegularFile(string path)
        {
            return Files.Contains(path);
        }

        public bool IsSymbolicLink(string path)
        {
            return SymbolicLinks.Contains(path);
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path)
        {
            return DirectoryEntries.TryGetValue(path.TrimEnd('/'), out var entries)
                ? entries
                : [];
        }

        public (uint Uid, uint Gid) GetOwnerIds(string path)
        {
            return OwnerIds.TryGetValue(path, out var ids) ? ids : (1000, 33);
        }

        public (uint Uid, uint Gid) GetSudoUserIds()
        {
            return (1000, 1000);
        }

        public void WriteAllBytes(string path, byte[] data)
        {
            Files.Add(path);
            FileContents[path] = data;
            Writes.Add((path, data));
        }

        public Stream OpenRead(string path)
        {
            return new MemoryStream(ReadAllBytes(path), writable: false);
        }

        public Stream OpenWrite(string path)
        {
            return new CapturingStream(data => WriteAllBytes(path, data));
        }

        public byte[] ReadAllBytes(string path)
        {
            return FileContents.TryGetValue(path, out var data) ? data : [];
        }

        public void AppendAllText(string path, string content)
        {
            Files.Add(path);
            var previous = FileContents.TryGetValue(path, out var data) ? data : [];
            FileContents[path] = previous.Concat(Encoding.UTF8.GetBytes(content)).ToArray();
        }

        public void MoveFileOverwrite(string sourcePath, string destinationPath)
        {
            Files.Remove(sourcePath);
            Files.Add(destinationPath);
            if (FileContents.Remove(sourcePath, out var data))
            {
                FileContents[destinationPath] = data;
            }
            Moves.Add((sourcePath, destinationPath));
        }

        public void DeleteFileIfExists(string path)
        {
            Files.Remove(path);
            FileContents.Remove(path);
        }

        private sealed class CapturingStream(Action<byte[]> capture) : MemoryStream
        {
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    capture(ToArray());
                }

                base.Dispose(disposing);
            }
        }

        public uint ResolveUserId(string owner)
        {
            return owner switch
            {
                "deploy" => 1000,
                "root" => 0,
                _ => throw new InvalidOperationException("owner was not found: " + owner),
            };
        }

        public uint ResolveGroupId(string group)
        {
            return group switch
            {
                "www-data" => 33,
                "root" => 0,
                _ => throw new InvalidOperationException("group was not found: " + group),
            };
        }

        public void ChangeOwner(string path, uint uid, uint gid)
        {
            OwnerIds[path] = (uid, gid);
            OwnerChanges.Add((path, uid, gid));
        }

        public void ChangeMode(string path, uint mode)
        {
            Modes[path] = Convert.ToString(mode, 8).PadLeft(3, '0');
            ModeChanges.Add((path, mode));
        }

        public string GetMode(string path)
        {
            return Modes.TryGetValue(path, out var mode) ? mode : "775";
        }
    }
}
