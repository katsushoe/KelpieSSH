using System.Text.RegularExpressions;

namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one allowed argument for an SSH command.
/// </summary>
/// <param name="Name">The argument name.</param>
/// <param name="Required">A value indicating whether the argument is required.</param>
/// <param name="MaxLength">The maximum argument length.</param>
/// <param name="Pattern">The optional regular expression pattern the value must match.</param>
public sealed record AllowedCommandParameterDefinition(
    string Name,
    bool Required = true,
    int MaxLength = 128,
    string? Pattern = null)
{
    private static readonly string[] DangerousFragments =
    [
        "&&",
        "||",
        ";",
        "`",
        "$(",
        ">",
        "<",
        "|",
        "\r",
        "\n",
    ];

    /// <summary>
    /// Validates the parameter definition.
    /// </summary>
    public void ValidateDefinition()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("SSH command argument name is required.");
        }

        if (MaxLength <= 0)
        {
            throw new InvalidOperationException($"SSH command argument max length is invalid: {Name}");
        }

        var hasDangerousFragment = DangerousFragments.Any(fragment => Name.Contains(fragment, StringComparison.Ordinal));
        if (hasDangerousFragment)
        {
            throw new InvalidOperationException($"SSH command argument name contains a dangerous fragment: {Name}");
        }

        if (!string.IsNullOrWhiteSpace(Pattern))
        {
            _ = new Regex(Pattern, RegexOptions.CultureInvariant);
        }
    }

    /// <summary>
    /// Validates an argument value.
    /// </summary>
    /// <param name="value">The argument value.</param>
    public void Validate(string value)
    {
        ValidateDefinition();

        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"SSH command argument is empty: {Name}");
        }

        if (value.Length > MaxLength)
        {
            throw new InvalidOperationException($"SSH command argument is too long: {Name}");
        }

        var hasDangerousFragment = DangerousFragments.Any(fragment => value.Contains(fragment, StringComparison.Ordinal));
        if (hasDangerousFragment)
        {
            throw new InvalidOperationException($"SSH command argument contains a dangerous fragment: {Name}");
        }

        if (!string.IsNullOrWhiteSpace(Pattern) && !Regex.IsMatch(value, Pattern, RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException($"SSH command argument format is invalid: {Name}");
        }
    }
}
