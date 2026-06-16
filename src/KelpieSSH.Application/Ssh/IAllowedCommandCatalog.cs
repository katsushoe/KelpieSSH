namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Defines access to allowed SSH command definitions.
/// </summary>
public interface IAllowedCommandCatalog
{
    /// <summary>
    /// Attempts to get an allowed command by name.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="command">The allowed command definition.</param>
    /// <returns><c>true</c> when the command exists and is safe to look up.</returns>
    bool TryGet(string name, out AllowedCommandDefinition command);

    /// <summary>
    /// Lists all allowed commands.
    /// </summary>
    /// <returns>The allowed commands.</returns>
    IReadOnlyCollection<AllowedCommandDefinition> List();
}
