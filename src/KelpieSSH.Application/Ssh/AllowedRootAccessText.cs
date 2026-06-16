namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Parses and formats AllowedRoots access flags.
/// </summary>
public static class AllowedRootAccessText
{
    /// <summary>
    /// Parses an AllowedRoots access flag expression.
    /// </summary>
    /// <param name="value">The flag expression.</param>
    /// <returns>The parsed access flags.</returns>
    public static AllowedRootAccess Parse(string? value)
    {
        return Parse(value, CreateSystemRights());
    }

    /// <summary>
    /// Creates the built-in rights dictionary.
    /// </summary>
    /// <returns>The built-in rights dictionary.</returns>
    public static Dictionary<string, AllowedRootAccess> CreateSystemRights()
    {
        return new Dictionary<string, AllowedRootAccess>(StringComparer.OrdinalIgnoreCase)
        {
            ["$ReadOnly"] = AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.CD,
            ["$ReadWrite"] = AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.CD | AllowedRootAccess.Write,
            ["$ALL"] = AllowedRootAccess.All,
        };
    }

    /// <summary>
    /// Parses an AllowedRoots access flag expression with named rights.
    /// </summary>
    /// <param name="value">The flag expression.</param>
    /// <param name="rights">The named rights available for reference.</param>
    /// <returns>The parsed access flags.</returns>
    public static AllowedRootAccess Parse(
        string? value,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("SSH allowed root access is required.");
        }

        var access = AllowedRootAccess.None;
        foreach (var part in value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            access |= ParsePart(part, rights);
        }

        if (access == AllowedRootAccess.None)
        {
            throw new InvalidOperationException("SSH allowed root access is required.");
        }

        return access;
    }

    /// <summary>
    /// Formats access flags as a pipe-separated expression.
    /// </summary>
    /// <param name="access">The access flags.</param>
    /// <returns>The formatted access expression.</returns>
    public static string Format(AllowedRootAccess access)
    {
        if ((access & AllowedRootAccess.All) == AllowedRootAccess.All)
        {
            return "$ALL";
        }

        var parts = new List<string>();
        AddPart(parts, access, AllowedRootAccess.Read, "@Read");
        AddPart(parts, access, AllowedRootAccess.List, "@List");
        AddPart(parts, access, AllowedRootAccess.Write, "@Write");
        AddPart(parts, access, AllowedRootAccess.Import, "@Import");
        AddPart(parts, access, AllowedRootAccess.Export, "@Export");
        AddPart(parts, access, AllowedRootAccess.CD, "@CD");
        return parts.Count == 0 ? "None" : string.Join("|", parts);
    }

    private static AllowedRootAccess ParsePart(
        string part,
        IReadOnlyDictionary<string, AllowedRootAccess> rights)
    {
        if (rights.TryGetValue(part, out var access))
        {
            return access;
        }

        if (!part.StartsWith('@'))
        {
            throw new InvalidOperationException($"Unknown SSH allowed root access: {part}");
        }

        return part.ToUpperInvariant() switch
        {
            "@READ" => AllowedRootAccess.Read,
            "@LIST" => AllowedRootAccess.List,
            "@WRITE" => AllowedRootAccess.Write,
            "@IMPORT" => AllowedRootAccess.Import,
            "@EXPORT" => AllowedRootAccess.Export,
            "@CD" => AllowedRootAccess.CD,
            "@CHANGEDIR" => AllowedRootAccess.CD,
            _ => throw new InvalidOperationException($"Unknown SSH allowed root access: {part}"),
        };
    }

    private static void AddPart(
        List<string> parts,
        AllowedRootAccess access,
        AllowedRootAccess flag,
        string name)
    {
        if (access.HasFlag(flag))
        {
            parts.Add(name);
        }
    }
}
