using KelpieSSH.Application.Ssh;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace KelpieSSH.Infrastructure.Ssh;

/// <summary>
/// Reads an SSH server host key fingerprint without running a remote command.
/// </summary>
public sealed class SshNetHostKeyFingerprintReader
{
    /// <summary>
    /// Reads the remote host key SHA256 fingerprint.
    /// </summary>
    /// <param name="profile">The SSH profile.</param>
    /// <returns>The normalized SHA256 fingerprint with the SHA256 prefix.</returns>
    public string ReadSha256(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var authenticationMethod = new NoneAuthenticationMethod(profile.UserName);
        var connectionInfo = new ConnectionInfo(
            profile.Host,
            profile.Port,
            profile.UserName,
            authenticationMethod)
        {
            Timeout = profile.ConnectionTimeout,
        };

        string? fingerprint = null;
        using var client = new SshClient(connectionInfo);
        client.HostKeyReceived += (_, args) =>
        {
            fingerprint = FormatSha256(args.FingerPrintSHA256);
            args.CanTrust = true;
        };

        try
        {
            client.Connect();
        }
        catch (SshAuthenticationException) when (!string.IsNullOrWhiteSpace(fingerprint))
        {
            return fingerprint;
        }
        catch (SshConnectionException) when (!string.IsNullOrWhiteSpace(fingerprint))
        {
            return fingerprint;
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new InvalidOperationException("SSH host key fingerprint could not be read.");
        }

        return fingerprint;
    }

    private static string FormatSha256(string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new InvalidOperationException("SSH host key SHA256 fingerprint was empty.");
        }

        var normalized = fingerprint.Trim();
        if (!normalized.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "SHA256:" + normalized;
        }

        return normalized.TrimEnd('=');
    }
}
