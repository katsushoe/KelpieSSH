namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents a Kelpie policy violation.
/// </summary>
public sealed class KelpiePolicyError : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KelpiePolicyError"/> class.
    /// </summary>
    /// <param name="message">The policy violation message.</param>
    public KelpiePolicyError(string message)
        : base($"KelpiePolicyError: {message}")
    {
    }
}
