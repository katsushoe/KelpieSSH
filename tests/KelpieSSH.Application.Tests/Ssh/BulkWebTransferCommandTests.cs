using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using KelpieWebPermissionHelper;

namespace KelpieSSH.Application.Tests.Ssh;

public sealed class BulkWebTransferCommandTests
{
    [Theory]
    [InlineData("/absolute")]
    [InlineData("folder\\file")]
    [InlineData("../escape")]
    public void Write_WhenArchiveEntryNameIsUnsafe_ShouldReject(string entryName)
    {
        var result = RunUnsafeArchive(entryName, externalAttributes: 0);

        result.ExitCode.Should().Be(1);
        result.Error.Should().Contain("unsafe or duplicate");
    }

    [Fact]
    public void Write_WhenArchiveEntryIsSymbolicLink_ShouldReject()
    {
        var result = RunUnsafeArchive("files/0000", externalAttributes: 0xA000 << 16);

        result.ExitCode.Should().Be(1);
        result.Error.Should().Contain("links and non-regular");
    }

    [Fact]
    public void Write_WhenArchiveContainsTraversalEntry_ShouldRejectAndLeaveNoTransaction()
    {
        var root = Path.Combine(Path.GetTempPath(), "kelpie-bulk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var manifest = new StreamWriter(zip.CreateEntry("manifest.json").Open(), Encoding.UTF8))
                {
                    manifest.Write("[]");
                }

                using (var traversal = new StreamWriter(zip.CreateEntry("../escape").Open(), Encoding.UTF8))
                {
                    traversal.Write("unsafe");
                }
            }

            archive.Position = 0;
            var hash = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
            archive.Position = 0;
            using var encodedArchive = EncodeForSshTransport(archive);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = BulkWebTransferCommand.Write(
                [
                    "bulk-write",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(root)),
                    "-",
                    archive.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "0",
                    new string('a', 32),
                    hash,
                ],
                encodedArchive,
                output,
                error);

            exitCode.Should().Be(1);
            error.ToString().Should().Contain("unsafe or duplicate");
            Directory.Exists(Path.Combine(root, ".kelpie-bulk-" + new string('a', 32))).Should().BeFalse();
            File.Exists(Path.Combine(root, "escape")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (int ExitCode, string Error) RunUnsafeArchive(string entryName, int externalAttributes)
    {
        var root = Path.Combine(Path.GetTempPath(), "kelpie-bulk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
            {
                using (var manifest = new StreamWriter(zip.CreateEntry("manifest.json").Open(), Encoding.UTF8))
                {
                    manifest.Write("[]");
                }

                var unsafeEntry = zip.CreateEntry(entryName);
                unsafeEntry.ExternalAttributes = externalAttributes;
                using var content = new StreamWriter(unsafeEntry.Open(), Encoding.UTF8);
                content.Write("unsafe");
            }

            archive.Position = 0;
            var hash = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
            archive.Position = 0;
            using var encodedArchive = EncodeForSshTransport(archive);
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = BulkWebTransferCommand.Write(
                [
                    "bulk-write",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(root)),
                    "-",
                    archive.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "0",
                    new string('a', 32),
                    hash,
                ],
                encodedArchive,
                output,
                error);
            return (exitCode, error.ToString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static MemoryStream EncodeForSshTransport(Stream archive)
    {
        using var encoded = new MemoryStream();
        using (var transform = new ToBase64Transform())
        using (var base64 = new CryptoStream(encoded, transform, CryptoStreamMode.Write, leaveOpen: true))
        {
            archive.CopyTo(base64);
            base64.FlushFinalBlock();
        }

        return new MemoryStream(encoded.ToArray(), writable: false);
    }
}
