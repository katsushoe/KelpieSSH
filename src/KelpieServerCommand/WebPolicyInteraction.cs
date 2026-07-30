namespace KelpieServerCommand;

/// <summary>
/// Abstracts the terminal used by the human-only web-policy command.
/// </summary>
public interface IWebPolicyInteraction
{
    /// <summary>Gets whether input and output are attached to a human terminal.</summary>
    bool IsInteractive { get; }

    /// <summary>Gets standard output.</summary>
    TextWriter Output { get; }

    /// <summary>Gets standard error.</summary>
    TextWriter Error { get; }

    /// <summary>Reads one confirmation line.</summary>
    string? ReadLine();
}

internal sealed class ConsoleWebPolicyInteraction : IWebPolicyInteraction
{
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public TextWriter Output => Console.Out;

    public TextWriter Error => Console.Error;

    public string? ReadLine()
    {
        return Console.ReadLine();
    }
}
