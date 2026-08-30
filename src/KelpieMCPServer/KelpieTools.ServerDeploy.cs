using System.ComponentModel;
using System.Security.Cryptography;
using KelpieSSH.Application.Ssh;
using ModelContextProtocol.Server;

namespace KelpieMCPServer;

public sealed partial class KelpieTools
{
    [McpServerTool(Name = "provider_version")]
    [Description("Returns the staged deployment provider name and version.")]
    public static ProviderVersionResult GetProviderVersion() =>
        new("KelpieSSH", typeof(KelpieTools).Assembly.GetName().Version?.ToString() ?? "unknown", "1");

    [McpServerTool(Name = "provider_capabilities")]
    [Description("Returns the staged deployment operations and bounded transfer limits.")]
    public static ProviderCapabilitiesResult GetProviderCapabilities() =>
        new(
            ["target_status", "deploy_prepare", "deploy_upload", "deploy_activate", "deploy_verify", "deploy_rollback", "deploy_cleanup"],
            "sha256",
            256L * 1024 * 1024,
            true,
            true);

    [McpServerTool(Name = "target_status")]
    [Description("Returns whether a named KelpieSSH target is configured without exposing credentials.")]
    public static TargetStatusResult GetTargetStatus(
        ISshConnectionProfileCatalog profileCatalog,
        string targetName,
        string? targetId = null)
    {
        var exists = profileCatalog.TryGet(targetName, out _);
        return new TargetStatusResult(targetName, targetId, exists, exists ? "configured" : "target-not-found");
    }

    [McpServerTool(Name = "deploy_prepare")]
    [Description("Creates an idempotent staged deployment bound to one configured target.")]
    public static ServerDeployResult PrepareDeployment(
        ISshConnectionProfileCatalog profileCatalog,
        WebBulkTransferStore transferStore,
        ServerDeploymentStore deploymentStore,
        string deploymentId,
        string targetName,
        string destination,
        string? targetId = null,
        string siteKey = "default")
    {
        try
        {
            if (!profileCatalog.TryGet(targetName, out _))
            {
                return Failure(deploymentId, targetName, targetId, siteKey, "target-not-found", "The target is not configured.", false);
            }

            var existing = TryGet(deploymentStore, deploymentId);
            if (existing is not null)
            {
                var normalizedDestination = NormalizeBulkRemotePath(destination);
                if (!string.Equals(existing.TargetName, targetName.Trim(), StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(existing.TargetId, targetId?.Trim(), StringComparison.Ordinal)
                    || !string.Equals(existing.SiteKey, siteKey.Trim(), StringComparison.Ordinal)
                    || !string.Equals(existing.Destination, normalizedDestination, StringComparison.Ordinal))
                {
                    return Failure(
                        deploymentId,
                        targetName,
                        targetId,
                        siteKey,
                        "idempotency-conflict",
                        "The deployment ID is already bound to different inputs.",
                        false);
                }

                return new ServerDeployResult(existing.Error is null, existing);
            }

            deploymentStore.EnsureCapacity();
            var transfer = transferStore.Create(targetName.Trim(), siteKey.Trim());
            var deployment = deploymentStore.Prepare(
                deploymentId.Trim(),
                targetName.Trim(),
                targetId?.Trim(),
                siteKey.Trim(),
                transfer.Id) with
            { Destination = NormalizeBulkRemotePath(destination) };
            deployment = deploymentStore.Update(deploymentStore.Get(deployment.DeploymentId), deployment);
            return new ServerDeployResult(true, deployment);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Failure(deploymentId, targetName, targetId, siteKey, "invalid-request", ex.Message, false);
        }
    }

    [McpServerTool(Name = "deploy_upload")]
    [Description("Registers and hashes one local artifact for a prepared deployment without returning its contents.")]
    public static async Task<ServerDeployResult> UploadDeploymentAsync(
        WebBulkTransferStore transferStore,
        ServerDeploymentStore deploymentStore,
        string deploymentId,
        string artifactPath,
        string sha256,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deployment = deploymentStore.Get(deploymentId);
            if (deployment.State == ServerDeploymentState.Uploaded
                && string.Equals(deployment.ArtifactPath, Path.GetFullPath(artifactPath), StringComparison.OrdinalIgnoreCase)
                && string.Equals(deployment.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
            {
                return new ServerDeployResult(true, deployment);
            }

            if (deployment.State != ServerDeploymentState.Prepared || deployment.Destination is null)
            {
                return WithError(deploymentStore, deployment, "invalid-state", "Only a prepared deployment can be uploaded.", false);
            }

            var actualHash = await ComputeSha256Async(artifactPath, cancellationToken);
            if (!string.Equals(actualHash, sha256, StringComparison.OrdinalIgnoreCase))
            {
                return WithError(deploymentStore, deployment, "artifact-hash-mismatch", "The artifact SHA-256 does not match.", false);
            }

            await AddWebBulkTransferFileAsync(
                transferStore,
                deployment.TransferId,
                artifactPath,
                deployment.Destination,
                contentType,
                cancellationToken: cancellationToken);
            var updated = deployment with
            {
                State = ServerDeploymentState.Uploaded,
                ArtifactPath = Path.GetFullPath(artifactPath),
                Sha256 = actualHash,
                Error = null,
            };
            return new ServerDeployResult(true, deploymentStore.Update(deployment, updated));
        }
        catch (KeyNotFoundException ex)
        {
            return Failure(deploymentId, string.Empty, null, string.Empty, "deployment-not-found", ex.Message, false);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Failure(deploymentId, string.Empty, null, string.Empty, "upload-failed", ex.Message, true);
        }
    }

    [McpServerTool(Name = "deploy_activate")]
    [Description("Atomically activates an uploaded deployment on its policy-approved target.")]
    public static async Task<ServerDeployResult> ActivateDeploymentAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider provider,
        WebBulkTransferStore transferStore,
        ServerDeploymentStore deploymentStore,
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deployment = deploymentStore.Get(deploymentId);
            if (deployment.State is ServerDeploymentState.Activated or ServerDeploymentState.Verified)
            {
                return new ServerDeployResult(true, deployment);
            }

            if (deployment.State != ServerDeploymentState.Uploaded)
            {
                return WithError(deploymentStore, deployment, "invalid-state", "Only an uploaded deployment can be activated.", false);
            }

            var preview = await PreviewWebBulkTransferAsync(
                sshCommandService, profileCatalog, provider, transferStore, deployment.TransferId, cancellationToken);
            var result = await ExecuteWebBulkTransferAsync(
                sshCommandService, profileCatalog, provider, transferStore, deployment.TransferId, preview.Confirmation, cancellationToken);
            if (!result.Applied)
            {
                return WithError(deploymentStore, deployment, "activate-failed", result.Error ?? "Deployment activation failed.", true);
            }

            var updated = deployment with { State = ServerDeploymentState.Activated, Error = null };
            return new ServerDeployResult(true, deploymentStore.Update(deployment, updated));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return Failure(deploymentId, string.Empty, null, string.Empty, "activate-failed", ex.Message, true);
        }
    }

    [McpServerTool(Name = "deploy_verify")]
    [Description("Verifies the activated remote artifact by metadata-only SHA-256.")]
    public static async Task<ServerDeployResult> VerifyDeploymentAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider provider,
        ServerDeploymentStore deploymentStore,
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deployment = deploymentStore.Get(deploymentId);
            if (deployment.State == ServerDeploymentState.Verified)
            {
                return new ServerDeployResult(true, deployment);
            }

            if (deployment.State != ServerDeploymentState.Activated || deployment.Destination is null || deployment.Sha256 is null)
            {
                return WithError(deploymentStore, deployment, "invalid-state", "Only an activated deployment can be verified.", false);
            }

            if (!profileCatalog.TryGet(deployment.TargetName, out var profile))
            {
                return WithError(deploymentStore, deployment, "target-not-found", "The target is not configured.", false);
            }

            var hash = await provider.HashAsync(
                sshCommandService, profile, deployment.SiteKey, deployment.Destination, "sha256", cancellationToken);
            if (hash.Error is not null || !string.Equals(hash.Hash, deployment.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return WithError(deploymentStore, deployment, "verify-failed", hash.Error?.Message ?? "The remote artifact SHA-256 does not match.", true);
            }

            var updated = deployment with { State = ServerDeploymentState.Verified, Error = null };
            return new ServerDeployResult(true, deploymentStore.Update(deployment, updated));
        }
        catch (KeyNotFoundException ex)
        {
            return Failure(deploymentId, string.Empty, null, string.Empty, "deployment-not-found", ex.Message, false);
        }
    }

    [McpServerTool(Name = "deploy_rollback")]
    [Description("Rolls back an activated or verified deployment and preserves stable failure details.")]
    public static async Task<ServerDeployResult> RollbackDeploymentAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider provider,
        WebBulkTransferStore transferStore,
        ServerDeploymentStore deploymentStore,
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deployment = deploymentStore.Get(deploymentId);
            if (deployment.State == ServerDeploymentState.RolledBack)
            {
                return new ServerDeployResult(true, deployment);
            }

            if (deployment.State is not (ServerDeploymentState.Activated or ServerDeploymentState.Verified))
            {
                return WithError(deploymentStore, deployment, "invalid-state", "Only an activated deployment can be rolled back.", false);
            }

            var result = await RollbackWebBulkTransferAsync(
                sshCommandService, profileCatalog, provider, transferStore, deployment.TransferId, cancellationToken);
            if (result.Error is not null)
            {
                return WithError(deploymentStore, deployment, "rollback-failed", result.Error, true);
            }

            var updated = deployment with { State = ServerDeploymentState.RolledBack, Error = null };
            return new ServerDeployResult(true, deploymentStore.Update(deployment, updated));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return Failure(deploymentId, string.Empty, null, string.Empty, "rollback-failed", ex.Message, true);
        }
    }

    [McpServerTool(Name = "deploy_cleanup")]
    [Description("Commits a verified deployment or removes an unactivated deployment draft.")]
    public static async Task<ServerDeployResult> CleanupDeploymentAsync(
        SshCommandService sshCommandService,
        ISshConnectionProfileCatalog profileCatalog,
        IWebPublicFileProvider provider,
        WebBulkTransferStore transferStore,
        ServerDeploymentStore deploymentStore,
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deployment = deploymentStore.Get(deploymentId);
            if (deployment.State == ServerDeploymentState.Cleaned)
            {
                return new ServerDeployResult(true, deployment);
            }

            if (deployment.State == ServerDeploymentState.Verified)
            {
                var result = await CommitWebBulkTransferAsync(
                    sshCommandService, profileCatalog, provider, transferStore, deployment.TransferId, cancellationToken);
                if (result.Error is not null)
                {
                    return WithError(deploymentStore, deployment, "cleanup-failed", result.Error, true);
                }
            }
            else if (deployment.State is ServerDeploymentState.Prepared or ServerDeploymentState.Uploaded)
            {
                CancelWebBulkTransfer(transferStore, deployment.TransferId);
            }
            else if (deployment.State != ServerDeploymentState.RolledBack)
            {
                return WithError(deploymentStore, deployment, "invalid-state", "Verify or roll back the deployment before cleanup.", false);
            }

            var updated = deployment with { State = ServerDeploymentState.Cleaned, Error = null };
            return new ServerDeployResult(true, deploymentStore.Update(deployment, updated));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return Failure(deploymentId, string.Empty, null, string.Empty, "cleanup-failed", ex.Message, true);
        }
    }

    private static ServerDeployment? TryGet(ServerDeploymentStore store, string deploymentId)
    {
        try
        {
            return store.Get(deploymentId);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private static ServerDeployResult WithError(
        ServerDeploymentStore store,
        ServerDeployment deployment,
        string code,
        string message,
        bool retryable)
    {
        var updated = deployment with { Error = new ServerDeployError(code, message, retryable) };
        return new ServerDeployResult(false, store.Update(deployment, updated));
    }

    private static ServerDeployResult Failure(
        string deploymentId,
        string targetName,
        string? targetId,
        string siteKey,
        string code,
        string message,
        bool retryable) =>
        new(false, new ServerDeployment(
            deploymentId,
            targetName,
            targetId,
            siteKey,
            string.Empty,
            ServerDeploymentState.Failed,
            null,
            null,
            null,
            new ServerDeployError(code, message, retryable)));

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("The artifact path must identify an existing regular file.");
        }

        await using var input = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
    }
}

public sealed record ProviderVersionResult(string Name, string Version, string ContractVersion);

public sealed record ProviderCapabilitiesResult(
    IReadOnlyList<string> Operations,
    string HashAlgorithm,
    long MaximumArtifactBytes,
    bool SupportsRollback,
    bool CredentialsManagedByProvider);

public sealed record TargetStatusResult(string TargetName, string? TargetId, bool Available, string Status);
