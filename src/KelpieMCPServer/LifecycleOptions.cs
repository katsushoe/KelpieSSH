using Moyai.ProviderAuthentication;

namespace KelpieMCPServer;

/// <summary>Administrator-owned lifecycle configuration; never populated from MCP arguments.</summary>
public sealed class LifecycleOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string TrustBundlePath { get; set; } = string.Empty;
    public string ReplayDatabasePath { get; set; } = string.Empty;
    public string DeploymentDatabasePath { get; set; } = string.Empty;
    public List<LifecycleTarget> Targets { get; set; } = [];

    public void Validate()
    {
        new AssertionOptions(Issuer).Validate();
        if (!Path.IsPathFullyQualified(TrustBundlePath)
            || !Path.IsPathFullyQualified(ReplayDatabasePath)
            || !Path.IsPathFullyQualified(DeploymentDatabasePath)
            || string.Equals(ReplayDatabasePath, DeploymentDatabasePath, StringComparison.OrdinalIgnoreCase)
            || Targets.Any(t => t.ProjectId == Guid.Empty || !ValidId(t.TargetId) || !ValidId(t.ProfileName))
            || Targets.Select(t => t.TargetId).Distinct(StringComparer.Ordinal).Count() != Targets.Count
            || Targets.Select(t => t.ProfileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Targets.Count)
            throw new ProviderAuthenticationException("authentication_unavailable");
    }

    internal static bool ValidId(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 200 && value == value.Trim() && !value.Any(char.IsControl);
}

public sealed record LifecycleTarget(Guid ProjectId, string TargetId, string ProfileName);

/// <summary>The provider, not its caller, defines the required scope.</summary>
public static class LifecycleContract
{
    public static IReadOnlyDictionary<string, string[]> ToolScopes { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["target_status"] = ["target.status"],
            ["deploy_prepare"] = ["deploy.prepare"],
            ["deploy_upload"] = ["deploy.upload"],
            ["deploy_activate"] = ["deploy.activate"],
            ["deploy_verify"] = ["deploy.verify"],
            ["deploy_rollback"] = ["deploy.rollback"],
            ["deploy_cleanup"] = ["deploy.cleanup"],
            ["deploy_status"] = ["deploy.status"],
        };

    public static AssertionCapability Capability { get; } = new(
        "kelpiessh", "kelpiessh", "2", "ES256", true, ToolScopes, ["kelpie_target"]);
}
