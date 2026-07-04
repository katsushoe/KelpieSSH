using System.Text;
using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Evaluates whether a normalized path is inside configured allowed roots.
/// </summary>
public static class AllowedRootMatcher
{
    /// <summary>
    /// Determines whether the path is allowed by the configured root patterns.
    /// </summary>
    /// <param name="path">The target path.</param>
    /// <param name="allowedRoots">The allowed root path or glob patterns.</param>
    /// <param name="osFamily">The target OS family.</param>
    /// <returns><c>true</c> when the path is allowed.</returns>
    public static bool IsAllowed(
        string path,
        IReadOnlyCollection<string>? allowedRoots,
        string osFamily)
    {
        if (string.IsNullOrWhiteSpace(path) || allowedRoots is null || allowedRoots.Count == 0)
        {
            return false;
        }

        var normalizedPath = SshPathNormalizer.Normalize(path);
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return false;
        }

        var comparison = IsWindows(osFamily) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var regexOptions = IsWindows(osFamily) ? RegexOptions.IgnoreCase : RegexOptions.None;
        var ancestors = GetAncestors(normalizedPath).ToArray();

        foreach (var root in allowedRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var normalizedRoot = SshPathNormalizer.Normalize(root, allowGlob: true);
            if (string.IsNullOrEmpty(normalizedRoot))
            {
                continue;
            }

            if (normalizedRoot is "*" or "**")
            {
                return true;
            }

            if (normalizedRoot.EndsWith("/**", StringComparison.Ordinal))
            {
                var rootPrefix = TrimTrailingSlash(normalizedRoot[..^3]);
                if (IsUnderRoot(normalizedPath, rootPrefix, comparison))
                {
                    return true;
                }

                continue;
            }

            if (!normalizedRoot.Contains('*', StringComparison.Ordinal))
            {
                if (IsUnderRoot(normalizedPath, normalizedRoot, comparison))
                {
                    return true;
                }

                continue;
            }

            var regex = new Regex(ToSegmentGlobRegex(normalizedRoot), regexOptions, TimeSpan.FromMilliseconds(100));
            if (ancestors.Any(ancestor => regex.IsMatch(ancestor)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether the path is allowed by the configured root rules and required access.
    /// </summary>
    /// <param name="path">The target path.</param>
    /// <param name="allowedRoots">The allowed root rules.</param>
    /// <param name="requiredAccess">The required access level.</param>
    /// <param name="osFamily">The target OS family.</param>
    /// <returns><c>true</c> when the path is allowed.</returns>
    public static bool IsAllowed(
        string path,
        IReadOnlyCollection<AllowedRootRule>? allowedRoots,
        AllowedRootAccess requiredAccess,
        string osFamily)
    {
        if (string.IsNullOrWhiteSpace(path) || allowedRoots is null || allowedRoots.Count == 0)
        {
            return false;
        }

        return allowedRoots.Any(root =>
            HasRequiredAccess(root.Access, requiredAccess)
            && IsAllowed(path, [root.Path], osFamily));
    }

    private static bool HasRequiredAccess(AllowedRootAccess actualAccess, AllowedRootAccess requiredAccess)
    {
        return (actualAccess & requiredAccess) == requiredAccess;
    }

    private static bool IsUnderRoot(string path, string root, StringComparison comparison)
    {
        if (string.Equals(path, root, comparison))
        {
            return true;
        }

        var prefix = root.EndsWith("/", StringComparison.Ordinal) ? root : root + "/";
        return path.StartsWith(prefix, comparison);
    }

    private static IEnumerable<string> GetAncestors(string path)
    {
        yield return path;

        var current = path;
        while (true)
        {
            var index = current.LastIndexOf('/');
            if (index <= 0)
            {
                if (current.StartsWith("/", StringComparison.Ordinal))
                {
                    yield return "/";
                }

                if (current.Length >= 3 && current[1] == ':' && current[2] == '/')
                {
                    yield return current[..3];
                }

                yield break;
            }

            current = current[..index];
            yield return current;
        }
    }

    private static string TrimTrailingSlash(string value)
    {
        if (value is "/" || (value.Length == 3 && value[1] == ':' && value[2] == '/'))
        {
            return value;
        }

        return value.TrimEnd('/');
    }

    private static bool IsWindows(string osFamily)
    {
        return string.Equals(osFamily, "windows", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToSegmentGlobRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        foreach (var character in pattern)
        {
            if (character == '*')
            {
                builder.Append("[^/]*");
                continue;
            }

            builder.Append(Regex.Escape(character.ToString()));
        }

        builder.Append('$');
        return builder.ToString();
    }
}
