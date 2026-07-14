namespace KelpieSSH.Application.Ssh;

/// <summary>Exports a remote regular file directly to a protected local file.</summary>
public interface ISshFileExporter
{
    /// <summary>Exports one remote file without returning its content.</summary>
    Task<SshFileExportResult> ExportAsync(
        SshConnectionProfile profile,
        string remotePath,
        string localPath,
        bool confirmSpecialPath,
        CancellationToken cancellationToken = default);
}

/// <summary>Metadata returned after a successful SSH file export.</summary>
public sealed record SshFileExportResult(
    string ProfileName,
    string RemotePath,
    string ResolvedRemotePath,
    string LocalPath,
    string Algorithm,
    string Hash,
    long Size,
    string Owner,
    string Group,
    string Mode,
    IReadOnlyCollection<string> Warnings);
