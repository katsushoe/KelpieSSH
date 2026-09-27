using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moyai.ProviderAuthentication;

namespace KelpieMCPServer;

/// <summary>Authenticates the actual tool request, before reserving or invoking any deployment stage.</summary>
public sealed class LifecycleToolFilter(
    IHttpContextAccessor http,
    ServerDeploymentStore deployments,
    WebBulkTransferStore transfers,
    ILogger<LifecycleToolFilter> logger,
    LifecycleOptions? options = null,
    IAssertionValidator? validator = null,
    LifecycleDeploymentStore? journal = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions ResultJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request,
        McpRequestHandler<CallToolRequestParams, CallToolResult> next, CancellationToken cancellationToken)
    {
        var parameters = request.Params;
        var tool = parameters?.Name ?? string.Empty;
        if (tool is "provider_version" or "provider_capabilities")
            return await next(request, cancellationToken);
        if (!LifecycleContract.ToolScopes.ContainsKey(tool))
        {
            // A lifecycle assertion cannot authorize another SSH or administrator tool.
            if (http.HttpContext?.Request.Headers.ContainsKey("Authorization") == true)
                return Failure("auth_scope_denied");
            return await next(request, cancellationToken);
        }
        return await ExecuteAsync(tool, parameters?.Arguments,
            http.HttpContext?.Request.Headers.Authorization.ToString(),
            http.HttpContext?.Request.Headers["X-Moyai-Operation-Id"].ToString(),
            async () => await next(request, cancellationToken), cancellationToken);
    }

    internal async Task<CallToolResult> ExecuteAsync(string tool, IDictionary<string, JsonElement>? arguments,
        string? authorization, string? operationId, Func<Task<CallToolResult>> invoke,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (options is null || validator is null || journal is null)
                throw new ProviderAuthenticationException("authentication_unavailable");
            if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.Ordinal)
                || authorization.Length > 16400)
                throw new ProviderAuthenticationException("auth_assertion_missing");
            if (operationId is null || !LifecycleOptions.ValidId(operationId))
                throw new ProviderAuthenticationException("auth_assertion_invalid");
            ValidateArguments(tool, arguments);
            var id = tool == "target_status" ? string.Empty : Required(arguments, "deploymentId");
            var snapshot = id.Length == 0 ? null : await journal.FindAsync(id, cancellationToken);
            LifecycleTarget target;
            if (tool is "target_status" or "deploy_prepare")
            {
                var profile = Required(arguments, "targetName");
                target = options.Targets.SingleOrDefault(t => string.Equals(t.ProfileName, profile, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ProviderAuthenticationException("auth_project_mismatch");
                if (arguments?.TryGetValue("targetId", out var supplied) == true
                    && supplied.ValueKind != JsonValueKind.Null && supplied.GetString() != target.TargetId)
                    throw new ProviderAuthenticationException("auth_project_mismatch");
            }
            else target = snapshot?.Target ?? throw new ProviderAuthenticationException("auth_project_mismatch");
            if (!options.Targets.Contains(target) || (snapshot is not null && snapshot.Target != target))
                throw new ProviderAuthenticationException("auth_project_mismatch");
            var expected = AssertionContext.ForResource("kelpiessh", target.ProjectId,
                "kelpie_target", target.TargetId, tool, LifecycleContract.ToolScopes[tool], operationId);
            if (validator is not IAssertionExecutionValidator executionValidator)
                throw new ProviderAuthenticationException("authentication_unavailable");
            var principal = await validator.ValidateAsync(authorization[7..], expected, cancellationToken);
            logger.LogInformation("Lifecycle authentication accepted. Tool={Tool} Project={Project} Target={Target} Operation={Operation}",
                tool, target.ProjectId, target.TargetId, operationId);

            if (tool == "deploy_status")
            {
                await executionValidator.EnsureCurrentAsync(principal, cancellationToken);
                return Result(new LifecycleDeploymentStatus(id, target.ProjectId, target.TargetId,
                    snapshot!.ResultUnknown ? "result-unknown" : snapshot.Deployment?.State.ToString().ToLowerInvariant() ?? "not-started",
                    snapshot.ResultUnknown));
            }
            // Use only the provider's canonical target identity in the invoked tool and response.
            if (tool is "target_status" or "deploy_prepare")
            {
                arguments!["targetName"] = JsonSerializer.SerializeToElement(target.ProfileName);
                arguments["targetId"] = JsonSerializer.SerializeToElement(target.TargetId);
            }
            if (tool == "target_status")
            {
                await executionValidator.EnsureCurrentAsync(principal, cancellationToken);
                return await invoke();
            }

            var inputHash = HashArguments(arguments);
            var cached = await journal.BeginAsync(id, target, tool, inputHash, cancellationToken);
            if (cached is not null)
            {
                await executionValidator.EnsureCurrentAsync(principal, cancellationToken);
                return cached;
            }
            if (snapshot?.Deployment is not null) deployments.Restore(snapshot.Deployment);
            if (snapshot?.Transfer is not null)
                transfers.Restore(snapshot.Transfer with { ExpiresAt = DateTimeOffset.MaxValue });
            try
            {
                await executionValidator.EnsureCurrentAsync(principal, cancellationToken);
            }
            catch (ProviderAuthenticationException)
            {
                // Dispatch has not started; release only this reservation so renewed authorization can retry.
                await journal.CancelBeforeDispatchAsync(id, tool, inputHash, CancellationToken.None);
                throw;
            }
            var result = await invoke();
            // Keep exceptions/provider diagnostics out of persisted and returned stage errors.
            var deployment = deployments.Find(id);
            var stage = result.IsError == true ? null : ReadStageResult(result);
            if (deployment is null) result = Failure("deployment_stage_failed");
            else
            {
                var success = stage?.Success == true && deployment.Error is null;
                if (!success)
                    deployment = deployment with { Error = new("deployment_stage_failed", "Deployment stage failed.", false) };
                deployments.Restore(deployment);
                result = Result(new ServerDeployResult(success, deployment));
            }
            WebBulkTransfer? transfer = deployment is null ? null : transfers.Find(deployment.TransferId);
            if (transfer?.Error is not null) transfer = transfer with { Error = "Deployment stage failed." };
            await journal.CompleteAsync(id, tool, result, deployment, transfer, cancellationToken);
            return result;
        }
        catch (ProviderAuthenticationException ex)
        {
            logger.LogWarning("Lifecycle request rejected. Code={Code}", ex.Code);
            return Failure(ex.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The journal reservation remains pending if dispatch may have begun.
            logger.LogError("Lifecycle request failed. Type={Type}", ex.GetType().Name);
            return Failure("deployment_result_unknown");
        }
        finally { _gate.Release(); }
    }

    private static string Required(IDictionary<string, JsonElement>? args, string name)
    {
        if (args is null || !args.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String
            || !LifecycleOptions.ValidId(value.GetString()!))
            throw new ProviderAuthenticationException("invalid-request");
        return value.GetString()!;
    }

    private static ServerDeployResult? ReadStageResult(CallToolResult result)
    {
        var json = result.StructuredContent?.GetRawText()
            ?? result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        return json is null ? null : JsonSerializer.Deserialize<ServerDeployResult>(json, ResultJson);
    }

    private static void ValidateArguments(string tool, IDictionary<string, JsonElement>? arguments)
    {
        string[] allowed = tool switch
        {
            "target_status" => ["targetName", "targetId"],
            "deploy_prepare" => ["deploymentId", "targetName", "destination", "targetId", "siteKey"],
            "deploy_upload" => ["deploymentId", "artifactPath", "sha256", "contentType"],
            _ => ["deploymentId"],
        };
        if (arguments is null || arguments.Any(p => !allowed.Contains(p.Key, StringComparer.Ordinal)
            || p.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)))
            throw new ProviderAuthenticationException("invalid-request");
    }

    internal static string HashArguments(IDictionary<string, JsonElement>? arguments)
    {
        // Stage arguments are scalar values. Reject containers and untyped secret-bearing extensions.
        if (arguments is null || arguments.Values.Any(v => v.ValueKind is JsonValueKind.Object or JsonValueKind.Array))
            throw new ProviderAuthenticationException("invalid-request");
        var ordered = arguments.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(ordered)));
    }

    internal static CallToolResult Result<T>(T value) => new()
    {
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(value, ResultJson) }],
        StructuredContent = JsonSerializer.SerializeToElement(value, ResultJson),
    };

    internal static CallToolResult Failure(string code) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = code }],
    };
}

public sealed record LifecycleDeploymentStatus(string DeploymentId, Guid ProjectId, string TargetId,
    string State, bool ResultUnknown);
