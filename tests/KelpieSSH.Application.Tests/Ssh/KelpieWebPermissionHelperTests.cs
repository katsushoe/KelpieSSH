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
        output.ToString().Should().StartWith("kelpie-web-permission-helper 0.1.0.4");
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

    private static string Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
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
            return (1000, 33);
        }

        public (uint Uid, uint Gid) GetSudoUserIds()
        {
            return (1000, 1000);
        }

        public void WriteAllBytes(string path, byte[] data)
        {
            Files.Add(path);
            Writes.Add((path, data));
        }

        public void MoveFileOverwrite(string sourcePath, string destinationPath)
        {
            Files.Remove(sourcePath);
            Files.Add(destinationPath);
            Moves.Add((sourcePath, destinationPath));
        }

        public void DeleteFileIfExists(string path)
        {
            Files.Remove(path);
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
            OwnerChanges.Add((path, uid, gid));
        }

        public void ChangeMode(string path, uint mode)
        {
            ModeChanges.Add((path, mode));
        }

        public string GetMode(string path)
        {
            return "775";
        }
    }
}
