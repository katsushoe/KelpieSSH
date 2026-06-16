using System.Globalization;
using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Applies provider-specific Nginx configuration edit targets.
/// </summary>
public sealed partial class NginxConfigEditMatcher
{
    /// <summary>
    /// Applies one provider-specific Nginx configuration edit.
    /// </summary>
    /// <param name="content">The source configuration content.</param>
    /// <param name="path">The source configuration path.</param>
    /// <param name="targetKey">The Nginx target key.</param>
    /// <param name="method">The edit method.</param>
    /// <param name="targetValue">The value or line to write.</param>
    /// <param name="updatedContent">The updated content.</param>
    /// <param name="error">The error message when the edit cannot be applied.</param>
    /// <returns><c>true</c> when the edit was applied.</returns>
    public bool TryApply(
        string content,
        string path,
        string targetKey,
        string method,
        string? targetValue,
        out string updatedContent,
        out string error)
    {
        updatedContent = content;
        error = string.Empty;

        var normalizedMethod = string.IsNullOrWhiteSpace(method) ? string.Empty : method.Trim().ToLowerInvariant();
        return normalizedMethod switch
        {
            "replace" => TryApplyReplace(content, targetKey, targetValue, out updatedContent, out error),
            "insert" => TryApplyInsert(content, path, targetKey, targetValue, out updatedContent, out error),
            "delete" => TryApplyDelete(content, targetKey, out updatedContent, out error),
            _ => FailEdit("Unsupported service config write method. Supported methods: replace, insert, delete.", out updatedContent, out error),
        };
    }

    private static bool TryApplyReplace(
        string content,
        string targetKey,
        string? targetValue,
        out string updatedContent,
        out string error)
    {
        updatedContent = content;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(targetKey))
        {
            return FailEdit("TargetKey is required.", out updatedContent, out error);
        }

        if (!TryValidateSingleLineValue(targetValue, "TargetValue", out error))
        {
            updatedContent = content;
            return false;
        }

        if (!TryParseDirectiveTarget(targetKey, out var blockPath, out var directiveName, out var targetIndex, out error))
        {
            return FailEdit(error, out updatedContent, out error);
        }

        var lines = SplitConfigLines(content, out var hadFinalNewline);
        var matches = FindDirectiveLineMatches(lines, directiveName, blockPath);
        if (!TrySelectDirectiveLineMatch(matches, targetIndex, out var matchedIndex, out error))
        {
            return FailEdit(error, out updatedContent, out error);
        }

        var indent = LeadingWhitespaceRegex().Match(lines[matchedIndex]).Value;
        var safeValue = NormalizeDirectiveValue(targetValue!, directiveName);
        if (string.IsNullOrWhiteSpace(safeValue) || safeValue.Contains(';', StringComparison.Ordinal) || safeValue.Contains('{', StringComparison.Ordinal) || safeValue.Contains('}', StringComparison.Ordinal))
        {
            return FailEdit("Replacement value must be a single Nginx directive value without semicolons or block braces.", out updatedContent, out error);
        }

        lines[matchedIndex] = $"{indent}{directiveName} {safeValue};";
        updatedContent = JoinConfigLines(lines, hadFinalNewline);
        return true;
    }

    private static bool TryApplyInsert(
        string content,
        string path,
        string targetKey,
        string? targetValue,
        out string updatedContent,
        out string error)
    {
        updatedContent = content;
        error = string.Empty;

        if (!TryParseLineTarget(path, targetKey, out var lineNumber, out error))
        {
            return false;
        }

        if (!TryValidateSingleLineValue(targetValue, "TargetValue", out error))
        {
            return false;
        }

        var lines = SplitConfigLines(content, out var hadFinalNewline);
        if (lineNumber < 1 || lineNumber > lines.Count + 1)
        {
            return FailEdit("Line target is outside the editable file range.", out updatedContent, out error);
        }

        var lineToInsert = NormalizeInsertLine(targetValue!);
        if (!SingleDirectiveLineRegex().IsMatch(lineToInsert))
        {
            return FailEdit("Insert targetValue must be a single Nginx directive line such as server_name localhost;.", out updatedContent, out error);
        }

        lines.Insert(lineNumber - 1, lineToInsert);
        updatedContent = JoinConfigLines(lines, hadFinalNewline: true);
        return true;
    }

    private static bool TryApplyDelete(
        string content,
        string targetKey,
        out string updatedContent,
        out string error)
    {
        updatedContent = content;
        error = string.Empty;

        if (!TryFindSingleDirectiveLine(content, targetKey, out var lines, out var hadFinalNewline, out var matchedIndex, out error))
        {
            return false;
        }

        lines.RemoveAt(matchedIndex);
        updatedContent = JoinConfigLines(lines, hadFinalNewline);
        return true;
    }

    private static bool TryFindSingleDirectiveLine(
        string content,
        string targetKey,
        out List<string> lines,
        out bool hadFinalNewline,
        out int matchedIndex,
        out string error)
    {
        error = string.Empty;
        matchedIndex = -1;
        lines = SplitConfigLines(content, out hadFinalNewline);

        if (string.IsNullOrWhiteSpace(targetKey))
        {
            error = "TargetKey is required.";
            return false;
        }

        if (!TryParseDirectiveTarget(targetKey, out var blockPath, out var directiveName, out var targetIndex, out error))
        {
            return false;
        }

        var matches = FindDirectiveLineMatches(lines, directiveName, blockPath);
        return TrySelectDirectiveLineMatch(matches, targetIndex, out matchedIndex, out error);
    }

    private static bool TryParseDirectiveTarget(
        string targetKey,
        out IReadOnlyList<string> blockPath,
        out string directiveName,
        out int? targetIndex,
        out string error)
    {
        blockPath = [];
        directiveName = string.Empty;
        targetIndex = null;
        error = string.Empty;

        var segments = targetKey
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            error = "TargetKey must be a dot-separated Nginx directive path such as server.server_name or server.server_name[0].";
            return false;
        }

        if (segments[..^1].Any(segment => !DirectiveNameRegex().IsMatch(segment)))
        {
            error = "TargetKey must be a dot-separated Nginx directive path such as server.server_name or server.server_name[0].";
            return false;
        }

        var directiveMatch = DirectiveTargetRegex().Match(segments[^1]);
        if (!directiveMatch.Success)
        {
            error = "TargetKey must be a dot-separated Nginx directive path such as server.server_name or server.server_name[0].";
            return false;
        }

        directiveName = directiveMatch.Groups["name"].Value;
        if (directiveMatch.Groups["index"].Success)
        {
            targetIndex = int.Parse(directiveMatch.Groups["index"].Value, CultureInfo.InvariantCulture);
        }

        blockPath = segments[..^1];
        return true;
    }

    private static IReadOnlyList<int> FindDirectiveLineMatches(
        IReadOnlyList<string> lines,
        string directiveName,
        IReadOnlyList<string> blockPath)
    {
        var matches = new List<int>();
        var stack = new List<string>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (DirectiveLineRegex(directiveName).IsMatch(line) && BlockPathMatches(stack, blockPath))
            {
                matches.Add(index);
            }

            UpdateNginxBlockStack(line, stack);
        }

        return matches;
    }

    private static bool TrySelectDirectiveLineMatch(
        IReadOnlyList<int> matches,
        int? targetIndex,
        out int matchedIndex,
        out string error)
    {
        matchedIndex = -1;
        error = string.Empty;

        if (matches.Count == 0)
        {
            error = "TargetKey did not match any editable Nginx directive.";
            return false;
        }

        if (targetIndex is not null)
        {
            if (targetIndex.Value >= matches.Count)
            {
                error = "TargetKey index did not match any editable Nginx directive.";
                return false;
            }

            matchedIndex = matches[targetIndex.Value];
            return true;
        }

        if (matches.Count > 1)
        {
            error = "TargetKey matched multiple Nginx directives. Use an indexed target such as server.server_name[0].";
            return false;
        }

        matchedIndex = matches[0];
        return true;
    }

    private static bool TryParseLineTarget(
        string path,
        string targetKey,
        out int lineNumber,
        out string error)
    {
        lineNumber = 0;
        error = string.Empty;
        var normalizedTarget = string.IsNullOrWhiteSpace(targetKey) ? string.Empty : targetKey.Trim();
        if (normalizedTarget.StartsWith("line:", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(normalizedTarget[5..], CultureInfo.InvariantCulture, out lineNumber)
                ? true
                : FailLineTarget(out error);
        }

        var separator = normalizedTarget.LastIndexOf(':');
        if (separator <= 0)
        {
            return FailLineTarget(out error);
        }

        var targetPath = normalizedTarget[..separator];
        if (!string.Equals(targetPath, path, StringComparison.Ordinal))
        {
            error = "Line target path must match the path argument.";
            return false;
        }

        return int.TryParse(normalizedTarget[(separator + 1)..], CultureInfo.InvariantCulture, out lineNumber)
            ? true
            : FailLineTarget(out error);
    }

    private static bool FailLineTarget(out string error)
    {
        error = "Insert targetKey must be line:<number> or <path>:<number>.";
        return false;
    }

    private static bool TryValidateSingleLineValue(string? value, string name, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"{name} is required.";
            return false;
        }

        if (value.Contains('\0', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
        {
            error = $"{name} must be a single UTF-8 text line.";
            return false;
        }

        return true;
    }

    private static string NormalizeDirectiveValue(string targetValue, string directiveName)
    {
        var value = targetValue.Trim();
        if (value.EndsWith(';'))
        {
            value = value[..^1].TrimEnd();
        }

        if (value.StartsWith(directiveName + " ", StringComparison.Ordinal))
        {
            value = value[(directiveName.Length + 1)..].TrimStart();
        }

        if (value.StartsWith(directiveName + ":", StringComparison.Ordinal))
        {
            value = value[(directiveName.Length + 1)..].TrimStart();
        }

        return value;
    }

    private static string NormalizeInsertLine(string targetValue)
    {
        var value = targetValue.Trim();
        var shorthand = DirectiveShorthandRegex().Match(value);
        return shorthand.Success
            ? $"{shorthand.Groups["name"].Value} {shorthand.Groups["value"].Value.Trim()};"
            : value;
    }

    private static bool BlockPathMatches(IReadOnlyList<string> stack, IReadOnlyList<string> blockPath)
    {
        if (blockPath.Count == 0)
        {
            return stack.Count == 0;
        }

        if (stack.Count < blockPath.Count)
        {
            return false;
        }

        var start = stack.Count - blockPath.Count;
        for (var index = 0; index < blockPath.Count; index++)
        {
            if (!string.Equals(stack[start + index], blockPath[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void UpdateNginxBlockStack(string line, List<string> stack)
    {
        var closeCount = line.Count(character => character == '}');
        for (var index = 0; index < closeCount && stack.Count > 0; index++)
        {
            stack.RemoveAt(stack.Count - 1);
        }

        var match = BlockStartRegex().Match(line);
        if (match.Success)
        {
            stack.Add(match.Groups["name"].Value);
        }
    }

    private static List<string> SplitConfigLines(string content, out bool hadFinalNewline)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        hadFinalNewline = normalized.EndsWith('\n');
        if (hadFinalNewline)
        {
            normalized = normalized[..^1];
        }

        return normalized.Length == 0
            ? []
            : normalized.Split('\n').ToList();
    }

    private static string JoinConfigLines(IReadOnlyList<string> lines, bool hadFinalNewline)
    {
        var joined = string.Join('\n', lines);
        return hadFinalNewline ? joined + "\n" : joined;
    }

    private static bool FailEdit(string message, out string updatedContent, out string error)
    {
        updatedContent = string.Empty;
        error = message;
        return false;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectiveNameRegex();

    [GeneratedRegex(@"^(?<name>[A-Za-z_][A-Za-z0-9_]*)(\[(?<index>[0-9]+)\])?$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectiveTargetRegex();

    [GeneratedRegex(@"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\b[^{;#]*\{\s*(#.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex BlockStartRegex();

    [GeneratedRegex(@"^\s*", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingWhitespaceRegex();

    [GeneratedRegex(@"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectiveShorthandRegex();

    [GeneratedRegex(@"^\s*[A-Za-z_][A-Za-z0-9_]*\s+[^;{}]+;\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SingleDirectiveLineRegex();

    private static Regex DirectiveLineRegex(string directiveName)
    {
        return new Regex(
            @"^\s*" + Regex.Escape(directiveName) + @"\s+[^;]+;\s*(#.*)?$",
            RegexOptions.CultureInvariant);
    }
}
