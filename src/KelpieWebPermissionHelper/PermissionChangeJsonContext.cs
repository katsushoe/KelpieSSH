using System.Text.Json.Serialization;

namespace KelpieWebPermissionHelper;

[JsonSerializable(typeof(PermissionHelper.PermissionChangeOutput))]
[JsonSerializable(typeof(PermissionHelper.PermissionedWriteOutput))]
internal sealed partial class PermissionChangeJsonContext : JsonSerializerContext;
