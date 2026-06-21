namespace Kelpie.Core;

/// <summary>
/// Holds command-line runtime directory overrides.
/// </summary>
public sealed record KelpieRuntimePathOverrides(
    string? ConfigDirectory = null,
    string? ProfilesDirectory = null,
    string? LogsDirectory = null,
    string? BinDirectory = null,
    string? KeysDirectory = null,
    string? DataDirectory = null)
{
    /// <summary>
    /// Gets an empty override set.
    /// </summary>
    public static KelpieRuntimePathOverrides Empty { get; } = new();

    /// <summary>
    /// Gets a value indicating whether any override is configured.
    /// </summary>
    public bool HasAnyOverride =>
        ConfigDirectory is not null
        || ProfilesDirectory is not null
        || LogsDirectory is not null
        || BinDirectory is not null
        || KeysDirectory is not null
        || DataDirectory is not null;
}
