using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KelpieSSH.Application.Tests.Ssh;

internal static class WebStatFixture
{
    internal static string Encode(string json)
    {
        if (!json.StartsWith('{')) return json;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var prefix = "KELPIE_STAT_1\n" + Base64(root.GetProperty("resolvedPath").GetString()!) + "\n";
        if (!root.GetProperty("exists").GetBoolean()) return prefix + "0\n";
        var type = root.GetProperty("type").GetString();
        var bits = type switch { "file" => 0x8000, "directory" => 0x4000, "symlink" => 0xa000, _ => 0x1000 };
        var mode = bits | Convert.ToInt32(root.GetProperty("mode").GetString(), 8);
        var date = DateTimeOffset.Parse(root.GetProperty("lastModified").GetString()!, CultureInfo.InvariantCulture).ToUniversalTime();
        var fields = new[] { mode.ToString("x", CultureInfo.InvariantCulture), root.GetProperty("size").GetInt64().ToString(CultureInfo.InvariantCulture), "1000", "1000", root.GetProperty("owner").GetString()!, root.GetProperty("group").GetString()!, date.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "00 +0000", "" };
        return prefix + "1\n" + Base64(string.Join('\0', fields)) + "\n";
    }
    private static string Base64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
