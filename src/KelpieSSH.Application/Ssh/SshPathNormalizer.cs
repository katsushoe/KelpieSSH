namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Normalizes remote SSH paths before policy matching.
/// </summary>
public static class SshPathNormalizer
{
    /// <summary>
    /// Normalizes separators and resolves dot segments.
    /// </summary>
    /// <param name="value">The path or path glob.</param>
    /// <param name="allowGlob">A value indicating whether glob segments should be preserved.</param>
    /// <returns>The normalized path, or an empty string when parent traversal escapes a relative base.</returns>
    public static string Normalize(string value, bool allowGlob = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace('\\', '/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (normalized is "*" or "**")
        {
            return allowGlob ? normalized : string.Empty;
        }

        var prefix = GetPrefix(normalized);
        var startIndex = prefix.Length;
        var absolute = prefix.Length > 0;
        var stack = new List<string>();
        var segments = normalized[startIndex..].Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (stack.Count > 0)
                {
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                if (absolute)
                {
                    continue;
                }

                return string.Empty;
            }

            if (!allowGlob && segment.Contains('*', StringComparison.Ordinal))
            {
                return string.Empty;
            }

            stack.Add(segment);
        }

        var body = string.Join("/", stack);
        var result = prefix + body;
        if (string.IsNullOrEmpty(result))
        {
            return absolute ? prefix : ".";
        }

        return TrimTrailingSlash(result);
    }

    private static string GetPrefix(string path)
    {
        if (path.StartsWith("/", StringComparison.Ordinal))
        {
            return "/";
        }

        if (path.Length >= 3
            && char.IsLetter(path[0])
            && path[1] == ':'
            && path[2] == '/')
        {
            return path[..3];
        }

        return string.Empty;
    }

    private static string TrimTrailingSlash(string value)
    {
        if (value is "/" || (value.Length == 3 && value[1] == ':' && value[2] == '/'))
        {
            return value;
        }

        return value.TrimEnd('/');
    }
}
