namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents the normalized authorization-relevant state of an SSH profile.
/// </summary>
public sealed record SshProfileAuthorizationSnapshot(
    string Host,
    int Port,
    string UserName,
    string AuthenticationMethod,
    string CredentialReference,
    KelpiePolicyMode Mode,
    IReadOnlyCollection<string> Capabilities,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<SshProfileAllowedRootSnapshot> AllowedRoots,
    IReadOnlyCollection<SshProfileSpecialPathSnapshot> SpecialPaths,
    IReadOnlyCollection<SshProfileUserAuthorizationSnapshot> Users)
{
    /// <summary>
    /// Creates a normalized snapshot without secret values.
    /// </summary>
    public static SshProfileAuthorizationSnapshot FromProfile(SshConnectionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new SshProfileAuthorizationSnapshot(
            Normalize(profile.Host),
            profile.Port,
            NormalizeCaseSensitive(profile.UserName),
            Normalize(profile.AuthenticationMethod),
            ResolveCredentialReference(profile.PrivateKeyPath, profile.PasswordSecretName),
            profile.Mode,
            NormalizeSet(profile.Capabilities.List()),
            NormalizeSet(profile.Roles),
            NormalizeRoots(profile.AllowedRootRules),
            NormalizeSpecialPaths(profile.SpecialPaths),
            profile.Users.Select(user => new SshProfileUserAuthorizationSnapshot(
                NormalizeCaseSensitive(user.UserName),
                Normalize(user.AuthenticationMethod),
                ResolveCredentialReference(user.PrivateKeyPath, user.PasswordSecretName),
                user.Mode,
                NormalizeSet(user.Capabilities.List()),
                NormalizeSet(user.Roles),
                NormalizeRoots(user.AllowedRootRules),
                NormalizeSpecialPaths(user.SpecialPaths)))
                .OrderBy(user => user.UserName, StringComparer.Ordinal)
                .ToArray());
    }

    private static string ResolveCredentialReference(string? privateKeyPath, string? passwordSecretName)
    {
        return !string.IsNullOrWhiteSpace(passwordSecretName)
            ? "secret:" + NormalizeCaseSensitive(passwordSecretName)
            : !string.IsNullOrWhiteSpace(privateKeyPath)
                ? "key:" + NormalizeCaseSensitive(privateKeyPath)
                : string.Empty;
    }

    private static IReadOnlyCollection<string> NormalizeSet(IEnumerable<string> values)
    {
        return values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Normalize)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyCollection<SshProfileAllowedRootSnapshot> NormalizeRoots(IEnumerable<AllowedRootRule> rules)
    {
        return rules.Select(rule => new SshProfileAllowedRootSnapshot(NormalizeCaseSensitive(rule.Path), rule.Access))
            .Distinct()
            .OrderBy(rule => rule.Path, StringComparer.Ordinal)
            .ThenBy(rule => rule.Access)
            .ToArray();
    }

    private static IReadOnlyCollection<SshProfileSpecialPathSnapshot> NormalizeSpecialPaths(IEnumerable<SpecialPathRule> rules)
    {
        return rules.Select(rule => new SshProfileSpecialPathSnapshot(NormalizeCaseSensitive(rule.Pattern), rule.Action))
            .Distinct()
            .OrderBy(rule => rule.Pattern, StringComparer.Ordinal)
            .ThenBy(rule => rule.Action)
            .ToArray();
    }

    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string NormalizeCaseSensitive(string? value) => value?.Trim() ?? string.Empty;
}

/// <summary>Represents a normalized allowed-root rule.</summary>
public sealed record SshProfileAllowedRootSnapshot(string Path, AllowedRootAccess Access);

/// <summary>Represents a normalized special-path rule.</summary>
public sealed record SshProfileSpecialPathSnapshot(string Pattern, SpecialPathAction Action);

/// <summary>Represents normalized authorization for one selectable user.</summary>
public sealed record SshProfileUserAuthorizationSnapshot(
    string UserName,
    string AuthenticationMethod,
    string CredentialReference,
    KelpiePolicyMode Mode,
    IReadOnlyCollection<string> Capabilities,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<SshProfileAllowedRootSnapshot> AllowedRoots,
    IReadOnlyCollection<SshProfileSpecialPathSnapshot> SpecialPaths);

/// <summary>Classifies an authorization snapshot change.</summary>
public enum SshProfileAuthorizationChangeKind
{
    None,
    NonPrivilegeChange,
    PrivilegeReduction,
    PrivilegeExpansion,
}

/// <summary>Represents an authorization snapshot comparison result.</summary>
public sealed record SshProfileAuthorizationDiff(
    SshProfileAuthorizationChangeKind Kind,
    IReadOnlyCollection<string> ChangedFields);

/// <summary>Compares trusted and proposed profile authorization snapshots.</summary>
public static class SshProfileAuthorizationEvaluator
{
    /// <summary>Compares two normalized snapshots.</summary>
    public static SshProfileAuthorizationDiff Compare(
        SshProfileAuthorizationSnapshot baseline,
        SshProfileAuthorizationSnapshot proposed)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(proposed);
        var expansions = new HashSet<string>(StringComparer.Ordinal);
        var reductions = new HashSet<string>(StringComparer.Ordinal);

        CompareIdentity(baseline, proposed, expansions);
        ComparePermissionSet("Capabilities", baseline.Capabilities, proposed.Capabilities, expansions, reductions);
        ComparePermissionSet("Roles", baseline.Roles, proposed.Roles, expansions, reductions);
        CompareMode("Mode", baseline.Mode, proposed.Mode, expansions, reductions);
        CompareRoots("AllowedRoots", baseline.AllowedRoots, proposed.AllowedRoots, expansions, reductions);
        CompareSpecialPaths("SpecialPaths", baseline.SpecialPaths, proposed.SpecialPaths, expansions, reductions);
        CompareUsers(baseline.Users, proposed.Users, expansions, reductions);

        var fields = expansions.Concat(reductions).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var kind = expansions.Count > 0
            ? SshProfileAuthorizationChangeKind.PrivilegeExpansion
            : reductions.Count > 0
                ? SshProfileAuthorizationChangeKind.PrivilegeReduction
                : SshProfileAuthorizationChangeKind.None;
        return new SshProfileAuthorizationDiff(kind, fields);
    }

    private static void CompareIdentity(SshProfileAuthorizationSnapshot baseline, SshProfileAuthorizationSnapshot proposed, ISet<string> expansions)
    {
        if (baseline.Host != proposed.Host || baseline.Port != proposed.Port) expansions.Add("ConnectionTarget");
        if (baseline.UserName != proposed.UserName) expansions.Add("ConnectionUser");
        if (baseline.AuthenticationMethod != proposed.AuthenticationMethod) expansions.Add("AuthenticationMethod");
        if (baseline.CredentialReference != proposed.CredentialReference) expansions.Add("CredentialReference");
    }

    private static void CompareUsers(IReadOnlyCollection<SshProfileUserAuthorizationSnapshot> baseline, IReadOnlyCollection<SshProfileUserAuthorizationSnapshot> proposed, ISet<string> expansions, ISet<string> reductions)
    {
        var oldUsers = baseline.ToDictionary(user => user.UserName, StringComparer.Ordinal);
        var newUsers = proposed.ToDictionary(user => user.UserName, StringComparer.Ordinal);
        foreach (var userName in newUsers.Keys.Except(oldUsers.Keys, StringComparer.Ordinal)) expansions.Add($"Users.{userName}");
        foreach (var userName in oldUsers.Keys.Except(newUsers.Keys, StringComparer.Ordinal)) reductions.Add($"Users.{userName}");
        foreach (var userName in oldUsers.Keys.Intersect(newUsers.Keys, StringComparer.Ordinal))
        {
            var oldUser = oldUsers[userName];
            var newUser = newUsers[userName];
            var prefix = $"Users.{userName}.";
            if (oldUser.AuthenticationMethod != newUser.AuthenticationMethod) expansions.Add(prefix + "AuthenticationMethod");
            if (oldUser.CredentialReference != newUser.CredentialReference) expansions.Add(prefix + "CredentialReference");
            CompareMode(prefix + "Mode", oldUser.Mode, newUser.Mode, expansions, reductions);
            ComparePermissionSet(prefix + "Capabilities", oldUser.Capabilities, newUser.Capabilities, expansions, reductions);
            ComparePermissionSet(prefix + "Roles", oldUser.Roles, newUser.Roles, expansions, reductions);
            CompareRoots(prefix + "AllowedRoots", oldUser.AllowedRoots, newUser.AllowedRoots, expansions, reductions);
            CompareSpecialPaths(prefix + "SpecialPaths", oldUser.SpecialPaths, newUser.SpecialPaths, expansions, reductions);
        }
    }

    private static void CompareMode(string field, KelpiePolicyMode baseline, KelpiePolicyMode proposed, ISet<string> expansions, ISet<string> reductions)
    {
        if (proposed > baseline) expansions.Add(field);
        if (proposed < baseline) reductions.Add(field);
    }

    private static void ComparePermissionSet(string field, IEnumerable<string> baseline, IEnumerable<string> proposed, ISet<string> expansions, ISet<string> reductions)
    {
        var oldSet = baseline.ToHashSet(StringComparer.Ordinal);
        var newSet = proposed.ToHashSet(StringComparer.Ordinal);
        if (newSet.Except(oldSet).Any()) expansions.Add(field);
        if (oldSet.Except(newSet).Any()) reductions.Add(field);
    }

    private static void CompareRoots(string field, IEnumerable<SshProfileAllowedRootSnapshot> baseline, IEnumerable<SshProfileAllowedRootSnapshot> proposed, ISet<string> expansions, ISet<string> reductions)
    {
        var oldRoots = baseline.ToDictionary(rule => rule.Path, rule => rule.Access, StringComparer.Ordinal);
        var newRoots = proposed.ToDictionary(rule => rule.Path, rule => rule.Access, StringComparer.Ordinal);
        if (newRoots.Keys.Except(oldRoots.Keys, StringComparer.Ordinal).Any()) expansions.Add(field);
        if (oldRoots.Keys.Except(newRoots.Keys, StringComparer.Ordinal).Any()) reductions.Add(field);
        foreach (var path in oldRoots.Keys.Intersect(newRoots.Keys, StringComparer.Ordinal))
        {
            var added = newRoots[path] & ~oldRoots[path];
            var removed = oldRoots[path] & ~newRoots[path];
            if (added != AllowedRootAccess.None) expansions.Add(field);
            if (removed != AllowedRootAccess.None) reductions.Add(field);
        }
    }

    private static void CompareSpecialPaths(string field, IEnumerable<SshProfileSpecialPathSnapshot> baseline, IEnumerable<SshProfileSpecialPathSnapshot> proposed, ISet<string> expansions, ISet<string> reductions)
    {
        var oldRules = baseline.ToDictionary(rule => rule.Pattern, rule => rule.Action, StringComparer.Ordinal);
        var newRules = proposed.ToDictionary(rule => rule.Pattern, rule => rule.Action, StringComparer.Ordinal);
        if (newRules.Keys.Except(oldRules.Keys, StringComparer.Ordinal).Any(pattern => newRules[pattern] != SpecialPathAction.Deny)) expansions.Add(field);
        if (oldRules.Keys.Except(newRules.Keys, StringComparer.Ordinal).Any()) expansions.Add(field);
        foreach (var pattern in oldRules.Keys.Intersect(newRules.Keys, StringComparer.Ordinal))
        {
            if (newRules[pattern] > oldRules[pattern]) expansions.Add(field);
            if (newRules[pattern] < oldRules[pattern]) reductions.Add(field);
        }
    }
}
