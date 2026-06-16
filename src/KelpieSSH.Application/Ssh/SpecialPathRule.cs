namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a special path pattern and its action.
/// </summary>
/// <param name="Pattern">The path or glob pattern.</param>
/// <param name="Action">The special path action.</param>
public sealed record SpecialPathRule(string Pattern, SpecialPathAction Action);
