using System.Text.Json.Serialization;

namespace KelpieWebPermissionHelper;

internal sealed record NginxRequest
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

internal sealed record NginxPolicy
{
    public int Protocol { get; init; }
    public string Revision { get; init; } = "";
    public string StateDirectory { get; init; } = "";
    public uint[] CallerUids { get; init; } = [];
    public string[] Targets { get; init; } = [];
    public string[] Directories { get; init; } = [];
    public string SitesEnabledDirectory { get; init; } = "";
    public string[] SiteTargetDirectories { get; init; } = [];
}

internal sealed record NginxLink
{
    public string Path { get; init; } = "";
    public string Target { get; init; } = "";
    public string ResolvedPath { get; init; } = "";
    public string Hash { get; init; } = "";
    public bool Pending { get; set; }
    public bool Removed { get; set; }
}

internal sealed record NginxTransaction
{
    public string Id { get; init; } = "";
    public string RequestId { get; init; } = "";
    public uint CallerUid { get; init; }
    public string PolicyHash { get; init; } = "";
    public string Target { get; init; } = "";
    public string ResolvedPath { get; init; } = "";
    public bool OriginalExists { get; init; }
    public string OriginalHash { get; init; } = "";
    public string NewHash { get; init; } = "";
    public string IntentDigest { get; set; } = "";
    public uint OriginalMode { get; init; }
    public uint OriginalUid { get; init; }
    public uint OriginalGid { get; init; }
    public ulong OriginalInode { get; init; }
    public bool WriteRequested { get; init; }
    public bool DisableDefaultSites { get; init; }
    public int NewBytes { get; init; }
    public NginxLink[] Links { get; init; } = [];
    public DateTimeOffset ExpiresAt { get; init; }
    public string State { get; set; } = "planned";
    public bool FilePending { get; set; }
    public bool FileApplied { get; set; }
    public ulong AppliedInode { get; set; }
    public ulong RestoredInode { get; set; }
    public bool BackupCreated { get; set; }
    public ulong BackupInode { get; set; }
    public bool BackupPending { get; set; }
    public bool BackupCleanupPending { get; set; }
    public string Error { get; set; } = "";
}

internal sealed record NginxResponse
{
    public int Protocol { get; init; } = 1;
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
[JsonSerializable(typeof(NginxRequest))]
[JsonSerializable(typeof(NginxPolicy))]
[JsonSerializable(typeof(NginxTransaction))]
[JsonSerializable(typeof(NginxResponse))]
internal partial class NginxContractJsonContext : JsonSerializerContext;
