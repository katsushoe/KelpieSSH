using System.Text.Json.Serialization;

namespace KelpieSSH.Application.Ssh;

/// <summary>Normalized isolated runtime information. Failed observations are null, never false measurements.</summary>
public sealed record RuntimeDiagnosticResult(
    [property: JsonPropertyName("protocol")] int Protocol,
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("runtime")] string Runtime,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("startupMode")] string? StartupMode,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("features")] RuntimeDiagnosticFeatures Features,
    [property: JsonPropertyName("checkedAt")] DateTimeOffset CheckedAt,
    [property: JsonPropertyName("cached")] bool Cached,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings);

/// <summary>Only the two explicitly measured built-in features are exposed.</summary>
public sealed record RuntimeDiagnosticFeatures(
    [property: JsonPropertyName("json")] bool? Json,
    [property: JsonPropertyName("sha256")] bool? Sha256);
