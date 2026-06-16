namespace Kelpie.Core;

/// <summary>
/// Represents the result of Kelpie home initialization.
/// </summary>
/// <param name="HomeDirectory">The initialized Kelpie home directory.</param>
/// <param name="ProfileName">The created or existing profile name.</param>
/// <param name="CreatedDirectories">The directories created during initialization.</param>
/// <param name="CreatedFiles">The files created during initialization.</param>
/// <param name="ExistingFiles">The files that already existed and were not overwritten.</param>
public sealed record KelpieHomeInitializationResult(
    string HomeDirectory,
    string ProfileName,
    IReadOnlyCollection<string> CreatedDirectories,
    IReadOnlyCollection<string> CreatedFiles,
    IReadOnlyCollection<string> ExistingFiles);
