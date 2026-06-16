namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines the coarse permission mode for a server profile.
/// </summary>
public enum KelpiePolicyMode
{
    /// <summary>
    /// Read-only mode for diagnostics, listing, and logs.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Standard safe mode.
    /// </summary>
    Safe,

    /// <summary>
    /// Maintenance mode for VPS package maintenance.
    /// </summary>
    Maintenance,

    /// <summary>
    /// Expert mode.
    /// </summary>
    Expert,
}
