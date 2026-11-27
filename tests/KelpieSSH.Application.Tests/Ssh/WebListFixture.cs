using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KelpieSSH.Application.Tests.Ssh;

internal static class WebListFixture
{
    internal static string Encode(string json)
    {
        if (!json.StartsWith('{')) return json;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var wire = "KELPIE_LIST_1\n" + Base64(root.GetProperty("resolvedPath").GetString()!) + "\n";
        if (!root.GetProperty("exists").GetBoolean()) return wire + "0\n";
        wire += "1\n";
        var payload = new StringBuilder();
        foreach (var e in root.GetProperty("entries").EnumerateArray())
        {
            var date = DateTimeOffset.Parse(e.GetProperty("lastModified").GetString() ?? "1970-01-01T00:00:00Z", CultureInfo.InvariantCulture).ToUniversalTime();
            var type = e.GetProperty("type").GetString() switch { "file" => 0x8000, "directory" => 0x4000, "symlink" => 0xa000, _ => 0x1000 };
            var mode = type | Convert.ToInt32(e.GetProperty("mode").GetString(), 8);
            payload.Append(string.Join('\0', new[] { e.GetProperty("name").GetString(), e.GetProperty("path").GetString(),
                e.GetProperty("resolvedPath").GetString(), e.GetProperty("depth").GetRawText(), mode.ToString("x", CultureInfo.InvariantCulture),
                e.GetProperty("size").GetRawText(), "1", "1", e.GetProperty("owner").GetString(), e.GetProperty("group").GetString(),
                date.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "00 +0000", "" }));
        }
        return wire + Base64(payload.ToString()) + "\n" + (root.GetProperty("truncated").GetBoolean() ? "END:1\n" : "END:0\n");
    }

    private static string Base64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
