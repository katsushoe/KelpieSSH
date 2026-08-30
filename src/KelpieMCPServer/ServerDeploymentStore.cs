using System.Collections.Concurrent;

namespace KelpieMCPServer;

/// <summary>
/// Stores idempotent staged server deployments for the lifetime of the MCP server.
/// </summary>
public sealed class ServerDeploymentStore
{
    private const int MaximumDeployments = 100;
    private readonly ConcurrentDictionary<string, ServerDeployment> _deployments = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a deployment or returns the existing deployment for the same idempotency key.
    /// </summary>
    public ServerDeployment Prepare(
        string deploymentId,
        string targetName,
        string? targetId,
        string siteKey,
        string transferId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(transferId);

        var deployment = new ServerDeployment(
            deploymentId,
            targetName,
            targetId,
            siteKey,
            transferId,
            ServerDeploymentState.Prepared,
            null,
            null,
            null,
            null);
        if (_deployments.TryAdd(deploymentId, deployment))
        {
            return deployment;
        }

        var existing = Get(deploymentId);
        if (!string.Equals(existing.TargetName, targetName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.TargetId, targetId, StringComparison.Ordinal)
            || !string.Equals(existing.SiteKey, siteKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The deployment ID is already bound to different inputs.");
        }

        return existing;
    }

    /// <summary>
    /// Ensures another deployment can be retained.
    /// </summary>
    public void EnsureCapacity()
    {
        if (_deployments.Count >= MaximumDeployments)
        {
            throw new InvalidOperationException($"At most {MaximumDeployments} deployments may be retained.");
        }
    }

    /// <summary>
    /// Returns one deployment.
    /// </summary>
    public ServerDeployment Get(string deploymentId) =>
        _deployments.TryGetValue(deploymentId, out var deployment)
            ? deployment
            : throw new KeyNotFoundException("Deployment was not found.");

    /// <summary>
    /// Replaces one deployment after verifying its current value.
    /// </summary>
    public ServerDeployment Update(ServerDeployment current, ServerDeployment updated)
    {
        if (!_deployments.TryUpdate(current.DeploymentId, updated, current))
        {
            throw new InvalidOperationException("Deployment state changed concurrently.");
        }

        return updated;
    }
}

/// <summary>
/// Identifies a staged server deployment state.
/// </summary>
public enum ServerDeploymentState
{
    Prepared,
    Uploaded,
    Activated,
    Verified,
    RolledBack,
    Cleaned,
    Failed,
}

/// <summary>
/// Represents one staged server deployment.
/// </summary>
public sealed record ServerDeployment(
    string DeploymentId,
    string TargetName,
    string? TargetId,
    string SiteKey,
    string TransferId,
    ServerDeploymentState State,
    string? ArtifactPath,
    string? Destination,
    string? Sha256,
    ServerDeployError? Error);

/// <summary>
/// Represents a stable, non-secret deployment error.
/// </summary>
public sealed record ServerDeployError(string Code, string Message, bool Retryable);

/// <summary>
/// Represents the result of one deployment stage.
/// </summary>
public sealed record ServerDeployResult(bool Success, ServerDeployment Deployment);
