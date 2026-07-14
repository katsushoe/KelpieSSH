using System.Net.Sockets;
using System.Security.Cryptography;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace KelpieSSH.Infrastructure.Ssh;

/// <summary>Exports remote files through SFTP without materializing content in MCP messages.</summary>
public sealed class SshNetFileExporter : ISshFileExporter
{
    private const long MaxExportBytes = 16 * 1024 * 1024;
    private readonly SshNetAuthenticationFactory _authenticationFactory;
    private readonly string _exportRoot;

    public SshNetFileExporter(ISshPasswordProvider passwordProvider, string exportRoot)
    {
        _authenticationFactory = new SshNetAuthenticationFactory(passwordProvider);
        _exportRoot = Path.GetFullPath(exportRoot);
    }

    public async Task<SshFileExportResult> ExportAsync(
        SshConnectionProfile profile,
        string remotePath,
        string localPath,
        bool confirmSpecialPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        var normalizedRemotePath = SshPathNormalizer.Normalize(remotePath);
        if (string.IsNullOrWhiteSpace(normalizedRemotePath) || !normalizedRemotePath.StartsWith("/", StringComparison.Ordinal))
        {
            throw new KelpiePolicyError("ssh_file_export requires an absolute remote path.");
        }

        var required = AllowedRootAccess.Read | AllowedRootAccess.Export;
        if (!AllowedRootMatcher.IsAllowed(normalizedRemotePath, profile.AllowedRootRules, required, profile.OsFamily))
        {
            throw new KelpiePolicyError("remote path requires both @Read and @Export in AllowedRoots.");
        }

        var specialAction = SpecialPathMatcher.FindAction(normalizedRemotePath, profile.SpecialPaths, profile.OsFamily);
        if (specialAction == SpecialPathAction.Deny)
        {
            throw new KelpiePolicyError("remote path is denied by SpecialPaths.");
        }

        if (specialAction == SpecialPathAction.Confirm && !confirmSpecialPath)
        {
            throw new KelpiePolicyError("remote path requires confirmSpecialPath=true by SpecialPaths.");
        }

        var destination = Path.GetFullPath(Path.IsPathRooted(localPath) ? localPath : Path.Combine(_exportRoot, localPath));
        var rootPrefix = _exportRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new KelpiePolicyError("local destination must be inside the configured Kelpie export directory.");
        }
        var parent = Path.GetDirectoryName(destination)
            ?? throw new KelpiePolicyError("local destination must have a parent directory.");
        Directory.CreateDirectory(parent);
        var temporary = destination + ".kelpie-" + Guid.NewGuid().ToString("N") + ".tmp";
        KpLog.Info($"SSH file export started. profile={profile.Name}, remotePath={normalizedRemotePath}, localPath={destination}");

        try
        {
            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            var authentication = await _authenticationFactory.CreateAsync(profile, linkedSource.Token);
            var result = await Task.Run(
                () => Export(profile, authentication, normalizedRemotePath, destination, temporary, linkedSource.Token),
                linkedSource.Token);
            KpLog.Info($"SSH file export completed. profile={profile.Name}, remotePath={normalizedRemotePath}, localPath={destination}, size={result.Size}, sha256={result.Hash}");
            return result;
        }
        catch (Exception ex) when (ex is SocketException or SshException)
        {
            KpLog.Warn($"SSH file export connection failed. profile={profile.Name}, remotePath={normalizedRemotePath}, exceptionType={ex.GetType().FullName}");
            throw new SshConnectionException("SSH file export connection failed.", ex);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static SshFileExportResult Export(
        SshConnectionProfile profile,
        AuthenticationMethod authentication,
        string remotePath,
        string destination,
        string temporary,
        CancellationToken cancellationToken)
    {
        var connectionInfo = new ConnectionInfo(profile.Host, profile.Port, profile.UserName, authentication)
        {
            Timeout = profile.ConnectionTimeout,
        };
        using var client = new SftpClient(connectionInfo);
        client.HostKeyReceived += (_, args) => args.CanTrust = SshHostKeyVerifier.IsTrusted(profile.HostKeyFingerprintSha256, args.FingerPrintSHA256);
        client.Connect();
        cancellationToken.ThrowIfCancellationRequested();

        EnsureNoSymlinkComponents(client, remotePath);
        var resolved = remotePath;
        if (!AllowedRootMatcher.IsAllowed(resolved, profile.AllowedRootRules, AllowedRootAccess.Read | AllowedRootAccess.Export, profile.OsFamily))
        {
            throw new KelpiePolicyError("resolved remote path is outside @Read and @Export AllowedRoots.");
        }

        var before = client.GetAttributes(resolved);
        if (!before.IsRegularFile || before.IsSymbolicLink)
        {
            throw new KelpiePolicyError("ssh_file_export only accepts non-symlink regular files.");
        }

        if (before.Size < 0 || before.Size > MaxExportBytes)
        {
            throw new KelpiePolicyError($"remote file exceeds the {MaxExportBytes}-byte export limit.");
        }

        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
        {
            client.DownloadFile(resolved, output, downloaded =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((long)downloaded > MaxExportBytes) throw new KelpiePolicyError("remote file exceeded the export limit while reading.");
            });
            output.Flush(flushToDisk: true);
        }

        var after = client.GetAttributes(resolved);
        var localInfo = new FileInfo(temporary);
        if (before.Size != after.Size || before.LastWriteTimeUtc != after.LastWriteTimeUtc || localInfo.Length != before.Size)
        {
            throw new IOException("remote file changed while it was being exported.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temporary))).ToLowerInvariant();
        File.Move(temporary, destination, overwrite: true);
        return new SshFileExportResult(profile.Name, remotePath, resolved, destination, "sha256", hash, localInfo.Length,
            before.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            before.GroupId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string.Empty,
            ["File content was not returned in the MCP response.", "SFTP did not expose a portable numeric mode value."]);
    }

    private static void EnsureNoSymlinkComponents(SftpClient client, string path)
    {
        var current = "/";
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = client.ListDirectory(current).SingleOrDefault(candidate => string.Equals(candidate.Name, segment, StringComparison.Ordinal))
                ?? throw new FileNotFoundException("remote path component was not found.");
            if (entry.IsSymbolicLink)
            {
                throw new KelpiePolicyError("ssh_file_export rejects symlinks in every remote path component.");
            }

            current = current == "/" ? "/" + segment : current + "/" + segment;
        }
    }
}
