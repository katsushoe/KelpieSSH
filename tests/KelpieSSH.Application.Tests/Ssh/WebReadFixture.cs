using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KelpieSSH.Application.Tests.Ssh;

internal static class WebReadFixture
{
    internal static string Encode(string json)
    {
        if (!json.StartsWith('{')) return json;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var prefix = "KELPIE_READ_1\n" + Base64(root.GetProperty("resolvedPath").GetString()!) + "\n";
        if (!root.GetProperty("exists").GetBoolean()) return prefix + "0\n";
        var date = DateTimeOffset.Parse(root.GetProperty("lastModified").GetString()!, CultureInfo.InvariantCulture).ToUniversalTime();
        var metadata = date.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + "\0" + date.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "00 +0000\0";
        return prefix + "1\n" + Base64(metadata) + "\n" + root.GetProperty("contentBase64").GetString() + "\n";
    }

    private static string Base64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
