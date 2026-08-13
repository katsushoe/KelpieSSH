using System.Text.Json;
using System.Text.RegularExpressions;

namespace KelpieWebPermissionHelper;

internal static partial class ManagedWebPolicyRules
{
    public static void ValidatePath(string path, bool allowRoot, bool allowGlob)
    {
        var suffixLength = GetGlobSuffixLength(path);
        if (suffixLength > 0)
        {
            if (!allowGlob)
            {
                throw new InvalidOperationException("policy path is invalid");
            }

            var directory = path[..^suffixLength];
            if (string.Equals(directory, string.Empty, StringComparison.Ordinal)
                || string.Equals(directory, "/", StringComparison.Ordinal)
                || !SafeUnixPathRegex().IsMatch(directory))
            {
                throw new InvalidOperationException("policy path is invalid");
            }

            return;
        }

        if (path.Contains('*', StringComparison.Ordinal)
            || !SafeUnixPathRegex().IsMatch(path)
            || (!allowRoot && string.Equals(path, "/", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("policy path is invalid");
        }
    }

    public static string GetAccess(JsonElement allowedFiles, string path)
    {
        if (allowedFiles.TryGetProperty(path, out var exact) && exact.ValueKind == JsonValueKind.String)
        {
            return exact.GetString() ?? string.Empty;
        }

        string? bestAccess = null;
        var bestLength = -1;
        foreach (var entry in allowedFiles.EnumerateObject())
        {
            var suffixLength = GetGlobSuffixLength(entry.Name);
            if (suffixLength == 0 || entry.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var directory = entry.Name[..^suffixLength];
            var prefix = directory + "/";
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var remainder = path[prefix.Length..];
            var recursive = suffixLength == 3;
            if (remainder.Length == 0 || (!recursive && remainder.Contains('/', StringComparison.Ordinal)))
            {
                continue;
            }

            if (directory.Length > bestLength)
            {
                bestLength = directory.Length;
                bestAccess = entry.Value.GetString();
            }
        }

        return bestAccess ?? throw new InvalidOperationException("managed web file is not allowed by helper policy");
    }

    private static int GetGlobSuffixLength(string path)
    {
        if (path.EndsWith("/**", StringComparison.Ordinal))
        {
            return 3;
        }

        return path.EndsWith("/*", StringComparison.Ordinal) ? 2 : 0;
    }

    [GeneratedRegex(@"^/(?:[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeUnixPathRegex();
}
