using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Ssh;

/// <summary>Executes runtime.info through the normal catalog/policy boundary and validates the entire response.</summary>
public sealed class RuntimeDiagnosticService(SshCommandService commands, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Reads a fresh isolated observation; refresh is accepted for compatibility and no result is cached.</summary>
    public async Task<RuntimeDiagnosticResult> GetInfoAsync(SshConnectionProfile profile, string runtime,
        bool refresh = false, KelpieExecutionChannel channel = KelpieExecutionChannel.Cli,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("N");
        if (runtime is not ("php" or "node")) return Failure(runtime, id, "invalid-input");
        if (string.IsNullOrWhiteSpace(profile.HostKeyFingerprintSha256)
            || string.Equals(profile.UserName, "root", StringComparison.OrdinalIgnoreCase)) return Failure(runtime, id, "denied");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            profile.Validate();
            var input = "{\"protocol\":1,\"operation\":\"runtime.info\",\"requestId\":\"" + id + "\",\"runtime\":\"" + runtime + "\"}";
            var result = await commands.ExecuteAsync(profile, "runtime_" + runtime + "_info", arguments: null,
                channel: channel, cancellationToken: cancellationToken, standardInput: input);
            if (result.TimedOut || result.ExitCode is 124 or 137) return Failure(runtime, id, "timeout");
            if (result.ExitCode == 127 && result.StandardError.Trim() == "runtime-startup-unavailable") return Failure(runtime, id, "unavailable");
            if (result.ExitCode == 65 && result.StandardError.Trim() is "runtime-startup-untrusted" or "runtime-startup-changed") return Failure(runtime, id, "denied");
            if (result.ExitCode is 66 or 126 or 127) return Failure(runtime, id, "unsupported");
            // The fixed PHP CLI invocation rejects a CGI SAPI with exit 1; Node uses 9 for invalid options.
            if ((runtime == "php" && result.ExitCode == 1) || (runtime == "node" && result.ExitCode == 9))
                return Failure(runtime, id, "unsupported");
            if (result.ExitCode != 0 || result.StandardError.Length != 0) return Failure(runtime, id, "invalid-response");
            return Parse(result.StandardOutput, runtime, id);
        }
        catch (KelpiePolicyError) { return Failure(runtime, id, "denied"); }
        catch (OperationCanceledException) { return Failure(runtime, id, cancellationToken.IsCancellationRequested ? "cancelled" : "timeout"); }
        catch (TimeoutException) { return Failure(runtime, id, "timeout"); }
        catch (SshCommandOutputLimitException) { return Failure(runtime, id, "invalid-response"); }
        catch (DecoderFallbackException) { return Failure(runtime, id, "invalid-response"); }
        catch (JsonException) { return Failure(runtime, id, "invalid-response"); }
        catch (ArgumentException) { return Failure(runtime, id, "invalid-input"); }
        catch (Exception ex)
        {
            // Never log remote output, paths, connection data or exception messages.
            Trace.TraceError("Runtime diagnostic failed ({0}).", ex.GetType().Name);
            return Failure(runtime, id, "connection-failed");
        }
    }

    internal RuntimeDiagnosticResult Parse(string text, string runtime, string id)
    {
        if (new UTF8Encoding(false, true).GetByteCount(text) > 16384) throw new JsonException();
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 8 });
        var root = document.RootElement;
        ExactFields(root, ["protocol", "operation", "requestId", "runtime", "startupMode", "version", "features"]);
        if (root.GetProperty("protocol").ValueKind != JsonValueKind.Number || !root.GetProperty("protocol").TryGetInt32(out var protocol) || protocol != 1
            || Text(root, "operation") != "runtime.info" || Text(root, "requestId") != id
            || Text(root, "runtime") != runtime || Text(root, "startupMode") != "isolated") throw new JsonException();
        var version = Text(root, "version");
        if (version.Length > 64 || !Regex.IsMatch(version, @"\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant)) throw new JsonException();
        if (!(runtime == "php" ? version.StartsWith("8.", StringComparison.Ordinal) : version.StartsWith("22.", StringComparison.Ordinal) || version.StartsWith("24.", StringComparison.Ordinal))) throw new JsonException();
        var features = root.GetProperty("features");
        ExactFields(features, ["json", "sha256"]);
        if (features.GetProperty("json").ValueKind != JsonValueKind.True || features.GetProperty("sha256").ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new JsonException();
        return new(1, id, "runtime.info", runtime, "ok", "isolated", version,
            new(true, features.GetProperty("sha256").GetBoolean()), _clock.GetUtcNow(), false, []);
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.String
        ? value.GetProperty(name).GetString()! : throw new JsonException();

    private static void ExactFields(JsonElement value, string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException();
        var names = value.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != fields.Length || names.Distinct(StringComparer.Ordinal).Count() != fields.Length
            || names.Except(fields, StringComparer.Ordinal).Any()) throw new JsonException();
    }

    private RuntimeDiagnosticResult Failure(string runtime, string id, string status) =>
        new(1, id, "runtime.info", runtime, status, null, null, new(null, null), _clock.GetUtcNow(), false, []);
}
