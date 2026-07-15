namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Reads and writes provider-approved web public files.
/// </summary>
public interface IWebPublicFileProvider
{
    /// <summary>
    /// Lists web public files and directories.
    /// </summary>
    Task<WebPublicFileListResult> ListAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        int maxDepth = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches web public file and directory names with a restricted glob pattern.
    /// </summary>
    Task<WebPublicFileListResult> SearchNameAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string pattern,
        int maxDepth = 3,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches readable web public text files for a bounded query.
    /// </summary>
    Task<WebPublicTextSearchResult> SearchTextAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string query,
        string path = "/",
        int maxDepth = 3,
        int limit = 50,
        int maxFileBytes = 262144,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns metadata for one web public path.
    /// </summary>
    Task<WebPublicFileStatResult> StatAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a metadata-only hash for one readable web public file.
    /// </summary>
    Task<WebPublicFileHashResult> HashAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string? algorithm = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether one web public file can be written without writing it.
    /// </summary>
    Task<WebPublicFileWriteCheckResult> CheckWriteAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string? contentType = null,
        bool usePrivilegedHelper = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether one explicitly allowed secret file can be written without writing it.
    /// </summary>
    Task<WebPublicFileWriteCheckResult> CheckSecretWriteAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string secretName,
        string? contentType = null,
        string? owner = null,
        string? mode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether one web public path is eligible for permission changes without changing it.
    /// </summary>
    Task<WebPublicPermissionCheckResult> CheckPermissionsAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string? owner = null,
        string? group = null,
        string? mode = null,
        bool recursive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one web public file.
    /// </summary>
    Task<WebPublicFileReadResult> ReadFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the beginning of one web public file with bounded bytes and lines.
    /// </summary>
    Task<WebPublicFileReadResult> ReadHeadAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        int maxBytes = 4096,
        int maxLines = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the end of one web public file with bounded bytes and lines.
    /// </summary>
    Task<WebPublicFileReadResult> ReadTailAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        int maxBytes = 4096,
        int maxLines = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes one web public file.
    /// </summary>
    Task<WebPublicFileWriteResult> WriteFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string contentBase64,
        string? encoding,
        string? contentType,
        string? owner = null,
        string? mode = null,
        string? expectedSha256 = null,
        bool createBackup = false,
        bool preservePermissions = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes one explicitly allowed secret file.
    /// </summary>
    Task<WebPublicFileWriteResult> WriteSecretFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string contentBase64,
        string? contentType,
        string? owner = null,
        string? mode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores one web public file from its managed backup.
    /// </summary>
    Task<WebPublicFileWriteResult> RollbackFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string expectedSha256,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits one web public file update by removing its managed backup.
    /// </summary>
    Task<WebPublicFileWriteResult> CommitFileAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the owner of one web public path.
    /// </summary>
    Task<WebPublicPermissionChangeResult> ChangeOwnerAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string owner,
        string group,
        bool recursive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the mode of one web public path.
    /// </summary>
    Task<WebPublicPermissionChangeResult> ChangeModeAsync(
        SshCommandService sshCommandService,
        SshConnectionProfile profile,
        string siteKey,
        string path,
        string mode,
        bool recursive = false,
        CancellationToken cancellationToken = default);
}
