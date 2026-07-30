using System.Net.Sockets;
using KelpieSSH.Application.Ssh;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace KelpieSSH.Infrastructure.Ssh;

/// <summary>
/// Uploads one local file to an explicitly supplied remote staging path.
/// </summary>
public sealed class SshNetFileUploader
{
    private readonly SshNetAuthenticationFactory _authenticationFactory = new(new NullSshPasswordProvider());

    /// <summary>
    /// Uploads a local file without exposing its contents through command output.
    /// </summary>
    public async Task UploadAsync(
        SshConnectionProfile profile,
        string localPath,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(remotePath);
        profile.Validate();

        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        var authentication = await _authenticationFactory.CreateAsync(profile, linkedSource.Token);
        await Task.Run(
            () => Upload(profile, authentication, localPath, remotePath, linkedSource.Token),
            linkedSource.Token);
    }

    private static void Upload(
        SshConnectionProfile profile,
        AuthenticationMethod authentication,
        string localPath,
        string remotePath,
        CancellationToken cancellationToken)
    {
        var connectionInfo = new ConnectionInfo(
            profile.Host,
            profile.Port,
            profile.UserName,
            authentication)
        {
            Timeout = profile.ConnectionTimeout,
        };
        using var client = new SftpClient(connectionInfo);
        client.HostKeyReceived += (_, args) =>
            args.CanTrust = SshHostKeyVerifier.IsTrusted(
                profile.HostKeyFingerprintSha256,
                args.FingerPrintSHA256);
        try
        {
            client.Connect();
            cancellationToken.ThrowIfCancellationRequested();
            using var input = new FileStream(
                localPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.SequentialScan);
            client.UploadFile(input, remotePath, canOverride: true);
            client.ChangePermissions(remotePath, 384);
        }
        catch (Exception ex) when (ex is SocketException or SshException)
        {
            throw new SshConnectionException("SSH helper upload failed.", ex);
        }
    }
}
