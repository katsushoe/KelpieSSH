using System.Text.Json;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents enabled Kelpie capability flags without a fixed bit-width limit.
/// </summary>
public sealed class PolicySet
{
    private static readonly IReadOnlyDictionary<string, string> KnownPolicyNames =
        KelpiePolicyNames.List().ToDictionary(name => name, StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _flags;

    private PolicySet(IEnumerable<string> flags)
    {
        _flags = new HashSet<string>(flags, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets an empty policy set.
    /// </summary>
    public static PolicySet Empty { get; } = new([]);

    /// <summary>
    /// Creates a policy set from capability flag names.
    /// </summary>
    /// <param name="policyNames">The capability flag names.</param>
    /// <returns>The policy set.</returns>
    public static PolicySet FromNames(IEnumerable<string> policyNames)
    {
        ArgumentNullException.ThrowIfNull(policyNames);

        var canonicalNames = policyNames
            .SelectMany(SplitPolicyName)
            .Select(ResolveKnownPolicyName)
            .ToArray();

        return new PolicySet(canonicalNames);
    }

    /// <summary>
    /// Creates a policy set from JSON that may be a string, an array, or an object with a Flags property.
    /// </summary>
    /// <param name="capabilitiesElement">The capabilities JSON element.</param>
    /// <returns>The policy set.</returns>
    public static PolicySet FromJson(JsonElement capabilitiesElement)
    {
        return capabilitiesElement.ValueKind switch
        {
            JsonValueKind.Undefined => Empty,
            JsonValueKind.Null => Empty,
            JsonValueKind.String => FromNames([capabilitiesElement.GetString() ?? string.Empty]),
            JsonValueKind.Array => FromNames(ReadArrayPolicyNames(capabilitiesElement)),
            JsonValueKind.Object => FromObject(capabilitiesElement),
            _ => throw new InvalidOperationException("SSH capabilities must be a string, an array, or an object."),
        };
    }

    /// <summary>
    /// Determines whether the capability flag is enabled.
    /// </summary>
    /// <param name="policyName">The capability flag name.</param>
    /// <returns><c>true</c> when the capability flag is enabled.</returns>
    public bool Allows(string policyName)
    {
        return _flags.Contains(ResolveKnownPolicyName(policyName));
    }

    /// <summary>
    /// Lists enabled capability flag names.
    /// </summary>
    /// <returns>The enabled capability flag names.</returns>
    public IReadOnlyCollection<string> List()
    {
        return _flags.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static PolicySet FromObject(JsonElement capabilitiesElement)
    {
        if (!capabilitiesElement.TryGetProperty("Flags", out var flagsElement))
        {
            return Empty;
        }

        return FromJson(flagsElement);
    }

    private static IEnumerable<string> ReadArrayPolicyNames(JsonElement capabilitiesElement)
    {
        foreach (var item in capabilitiesElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("SSH capability array items must be strings.");
            }

            yield return item.GetString() ?? string.Empty;
        }
    }

    private static IEnumerable<string> SplitPolicyName(string value)
    {
        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ResolveKnownPolicyName(string policyName)
    {
        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new InvalidOperationException("SSH capability flag name must not be empty.");
        }

        if (KnownPolicyNames.TryGetValue(policyName, out var canonicalName))
        {
            return canonicalName;
        }

        throw new InvalidOperationException($"Unknown SSH capability flag: {policyName}");
    }
}
