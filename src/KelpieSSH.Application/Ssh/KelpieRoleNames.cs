namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines supported high-level Kelpie role names.
/// </summary>
public static class KelpieRoleNames
{
    /// <summary>
    /// Allows read-only diagnostics.
    /// </summary>
    public const string ReadOnly = nameof(ReadOnly);

    /// <summary>
    /// Allows safe diagnostics and low-risk operations.
    /// </summary>
    public const string Safe = nameof(Safe);

    /// <summary>
    /// Allows maintenance operations.
    /// </summary>
    public const string Maintenance = nameof(Maintenance);

    /// <summary>
    /// Allows expert-level operations.
    /// </summary>
    public const string Expert = nameof(Expert);

    /// <summary>
    /// Allows read/write access to web public roots.
    /// </summary>
    public const string WebUser = nameof(WebUser);

    /// <summary>
    /// Allows web server configuration maintenance.
    /// </summary>
    public const string WebAdmin = nameof(WebAdmin);

    /// <summary>
    /// Returns whether the role name is supported.
    /// </summary>
    /// <param name="role">The role name.</param>
    /// <returns>True when the role is supported.</returns>
    public static bool IsKnown(string role)
    {
        return string.Equals(role, ReadOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, Safe, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, Maintenance, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, Expert, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, WebUser, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, WebAdmin, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns whether the role maps to a policy mode.
    /// </summary>
    /// <param name="role">The role name.</param>
    /// <returns>True when the role maps to a policy mode.</returns>
    public static bool IsPolicyRole(string role)
    {
        return string.Equals(role, ReadOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, Safe, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, Maintenance, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, Expert, StringComparison.OrdinalIgnoreCase);
    }
}
