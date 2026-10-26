using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KelpieSSH.Application.Ssh;

/// <summary>Maps the nginx helper protocol without exposing it as a public MCP schema.</summary>
internal static class NginxHelperClient
{
    internal static async Task<NginxHelperReply> CallAsync(SshCommandService service, SshConnectionProfile profile,
        string operation, NginxHelperRequest request, CancellationToken cancellationToken)
    {
        var reply = await CallCoreAsync(service, profile, operation, request, cancellationToken);
        if (reply.State != "unknown" || request.TransactionId.Length == 0 || operation is not ("apply" or "commit" or "rollback"))
            return reply;
        using var reconciliation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var status = await CallCoreAsync(service, profile, "status", request, reconciliation.Token);
        if (status.State is "unknown" or "rejected") return reply;
        if (operation == "commit" && status.State == "committed" || operation == "rollback" && status.State == "rolled-back")
            return status with { Ok = true, Error = "" };
        return status with { Ok = false, Error = reply.Error + " Reconciled state: " + status.State + "; no mutation was retried." };
    }

    private static async Task<NginxHelperReply> CallCoreAsync(SshCommandService service, SshConnectionProfile profile,
        string operation, NginxHelperRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(profile, "service_config_nginx_helper_" + operation,
                arguments: null, channel: KelpieExecutionChannel.Mcp,
                standardInput: JsonSerializer.Serialize(request, NginxHelperClientJson.Default.NginxHelperRequest),
                cancellationToken: cancellationToken);
            if (result.TimedOut || result.StandardOutput.Length > 65536 || result.StandardError.Length > 4096)
                return Failure("Helper outcome is unknown; reconcile the transaction before retrying.", request);
            using var document = JsonDocument.Parse(result.StandardOutput, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || document.RootElement.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count()
                    != document.RootElement.EnumerateObject().Count())
                return Failure("Invalid helper response; reconcile the transaction before retrying.", request);
            var reply = JsonSerializer.Deserialize(result.StandardOutput, NginxHelperClientJson.Default.NginxHelperReply);
            if (reply is null || reply.Protocol != 1 || reply.RequestId != request.RequestId
                || reply.TransactionId is null || reply.IntentDigest is null || reply.State is null || reply.Error is null
                || reply.State is not ("planned" or "allowed" or "applied" or "no-op" or "committed" or "rolled-back"
                    or "recovery-required" or "rejected" or "unknown" or "conflict" or "applying" or "rolling-back" or "committing")
                || reply.TransactionId.Length > 0 && !Guid.TryParseExact(reply.TransactionId, "N", out _)
                || request.TransactionId.Length > 0 && reply.TransactionId.Length > 0 && request.TransactionId != reply.TransactionId
                || reply.LinksChanged is < 0 or > 64 || reply.BytesWritten is < 0 or > 65536
                || reply.Ok && (result.ExitCode != 0 || reply.State is not ("planned" or "allowed" or "applied" or "no-op" or "committed" or "rolled-back"))
                || reply.Ok && operation != "check" && (!Guid.TryParseExact(reply.TransactionId, "N", out _) || !IsHash(reply.IntentDigest))
                || reply.State == "no-op" && (reply.ConfigChanged || reply.LinksChanged != 0 || reply.BytesWritten != 0))
                return Failure("Invalid helper response; reconcile the transaction before retrying.", request);
            return reply.Ok ? reply : reply with
            {
                Error = "Nginx helper operation failed (" + SafeCode(reply.Error) + "); "
                    + (reply.State is "unknown" or "recovery-required" ? "reconciliation is required." : "no automatic fallback is permitted."),
            };
        }
        catch (JsonException)
        {
            return Failure("Invalid helper response; reconcile the transaction before retrying.", request);
        }
        catch (OperationCanceledException)
        {
            return Failure("Helper outcome is unknown after cancellation; reconcile before retrying.", request);
        }
        catch (SshCommandConnectionException)
        {
            return Failure("Helper connection failed; reconcile before retrying a dispatched mutation.", request);
        }
    }

    internal static NginxHelperRequest Create(string path, string? content, string? original, bool? existed, bool disableSites) => new()
    {
        RequestId = Guid.NewGuid().ToString("N"), Target = path, ContentBase64 = content is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
        ExpectedExists = existed, ExpectedHash = original is null ? null : Hash(original), DisableDefaultSites = disableSites,
    };

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string SafeCode(string code) => code.Length is > 0 and <= 64 && code.All(c => c is >= 'a' and <= 'z' or '-') ? code : "operation-failed";
    private static bool IsHash(string value) => value.Length == 64 && value.All(c => c is >= 'a' and <= 'f' or >= '0' and <= '9');
    private static NginxHelperReply Failure(string error, NginxHelperRequest request) => new()
    {
        RequestId = request.RequestId, TransactionId = request.TransactionId, State = "unknown", Error = error,
    };

    internal static async Task<(NginxHelperRequest Request, NginxHelperReply Reply)> PrepareAndApplyAsync(
        SshCommandService service, SshConnectionProfile profile, NginxHelperRequest request, CancellationToken cancellationToken)
    {
        var plan = await CallAsync(service, profile, "plan", request, cancellationToken);
        if (!plan.Ok || plan.State != "planned") return (request, plan);
        request = request with { TransactionId = plan.TransactionId, IntentDigest = plan.IntentDigest };
        var applied = await CallAsync(service, profile, "apply", request, cancellationToken);
        return (request, applied);
    }
}

internal sealed record NginxHelperRequest
{
    public int Protocol { get; init; } = 1;
    public string RequestId { get; init; } = "";
    public string Target { get; init; } = "";
    public string? ContentBase64 { get; init; }
    public string? ExpectedHash { get; init; }
    public bool? ExpectedExists { get; init; }
    public bool DisableDefaultSites { get; init; }
    public string TransactionId { get; init; } = "";
    public string IntentDigest { get; init; } = "";
}

internal sealed record NginxHelperReply
{
    public int Protocol { get; init; }
    public string RequestId { get; init; } = "";
    public string TransactionId { get; init; } = "";
    public bool Ok { get; init; }
    public string State { get; init; } = "";
    public bool ConfigChanged { get; init; }
    public int LinksChanged { get; init; }
    public int BytesWritten { get; init; }
    public string IntentDigest { get; init; } = "";
    public string OriginalHash { get; init; } = "";
    public string CurrentHash { get; init; } = "";
    public bool Recoverable { get; init; }
    public string Error { get; init; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(NginxHelperRequest))]
[JsonSerializable(typeof(NginxHelperReply))]
internal partial class NginxHelperClientJson : JsonSerializerContext;
