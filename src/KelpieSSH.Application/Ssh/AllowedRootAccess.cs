namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines access allowed for a configured root path.
/// </summary>
[Flags]
public enum AllowedRootAccess
{
    /// <summary>
    /// No path operation is allowed.
    /// </summary>
    None = 0,

    /// <summary>
    /// File content read operations are allowed.
    /// </summary>
    Read = 1 << 0,

    /// <summary>
    /// File and directory listing operations are allowed.
    /// </summary>
    List = 1 << 1,

    /// <summary>
    /// File content write, edit, delete, and move operations are allowed.
    /// </summary>
    Write = 1 << 2,

    /// <summary>
    /// Upload/import operations are allowed.
    /// </summary>
    Import = 1 << 3,

    /// <summary>
    /// Download/export operations are allowed.
    /// </summary>
    Export = 1 << 4,

    /// <summary>
    /// Change directory operations are allowed.
    /// </summary>
    CD = 1 << 5,

    /// <summary>
    /// All path operations are allowed.
    /// </summary>
    All = Read | List | Write | Import | Export | CD,
}
