using System.Text;
using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Evaluates special path rules.
/// </summary>
public static class SpecialPathMatcher
{
    /// <summary>
    /// Finds the first special path action for the specified path.
    /// </summary>
    /// <param name="path">The target path.</param>
    /// <param name="specialPaths">The special path rules.</param>
    /// <param name="osFamily">The target OS family.</param>
    /// <returns>The matched action, or <c>null</c> when no rule matches.</returns>
    public static SpecialPathAction? FindAction(
        string path,
        IReadOnlyCollection<SpecialPathRule>? specialPaths,
        string osFamily)
    {
        if (string.IsNullOrWhiteSpace(path) || specialPaths is null || specialPaths.Count == 0)
        {
            return null;
        }

        var normalizedPath = NormalizePath(path);
        var regexOptions = IsWindows(osFamily) ? RegexOptions.IgnoreCase : RegexOptions.None;

        foreach (var rule in specialPaths)
        {
            if (string.IsNullOrWhiteSpace(rule.Pattern))
            {
                continue;
            }

            var normalizedPattern = NormalizePath(rule.Pattern);
            var regex = new Regex(ToGlobRegex(normalizedPattern), regexOptions, TimeSpan.FromMilliseconds(100));
            if (regex.IsMatch(normalizedPath))
            {
                return rule.Action;
            }
        }

        return null;
    }

    private static string NormalizePath(string value)
    {
        var normalized = value.Trim().Replace('\\', '/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        return normalized.TrimEnd('/');
    }

    private static bool IsWindows(string osFamily)
    {
        return string.Equals(osFamily, "windows", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToGlobRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (character == '*')
            {
                if (index + 1 < pattern.Length && pattern[index + 1] == '*')
                {
                    builder.Append(".*");
                    index++;
                }
                else
                {
                    builder.Append("[^/]*");
                }

                continue;
            }

            builder.Append(Regex.Escape(character.ToString()));
        }

        builder.Append('$');
        return builder.ToString();
    }
}
