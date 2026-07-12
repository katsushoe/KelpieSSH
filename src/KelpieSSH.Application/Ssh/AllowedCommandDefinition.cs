namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Represents one SSH command that is allowed by KelpieSSH.
/// </summary>
/// <param name="Name">The stable command name exposed to callers.</param>
/// <param name="CommandTemplate">The command template sent over SSH after validated arguments are rendered.</param>
/// <param name="DefaultTimeout">The default execution timeout.</param>
/// <param name="Parameters">The allowed command parameters.</param>
/// <param name="RiskLevel">The execution risk level.</param>
public sealed record AllowedCommandDefinition(
    string Name,
    string CommandTemplate,
    TimeSpan DefaultTimeout,
    IReadOnlyCollection<AllowedCommandParameterDefinition>? Parameters = null,
    SshCommandRiskLevel RiskLevel = SshCommandRiskLevel.ReadOnly)
{
    private static readonly string[] DangerousFragments =
    [
        "&&",
        "||",
        "&",
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
    /// Builds the final SSH command text from validated arguments.
    /// </summary>
    /// <param name="arguments">The caller-supplied command arguments.</param>
    /// <returns>The final SSH command text.</returns>
    public string BuildCommandText(IReadOnlyDictionary<string, string>? arguments = null)
    {
        ValidateSafeText(Name, "command name");
        ValidateSafeTemplate(CommandTemplate);

        var renderedCommand = CommandTemplate;
        var parameters = Parameters ?? [];
        var normalizedArguments = arguments ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var allowedParameters = parameters.ToDictionary(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var argumentName in normalizedArguments.Keys)
        {
            ValidateSafeText(argumentName, "argument name");
            if (!allowedParameters.ContainsKey(argumentName))
            {
                throw new InvalidOperationException($"SSH command argument is not allowed: {argumentName}");
            }
        }

        foreach (var parameter in parameters)
        {
            if (!normalizedArguments.TryGetValue(parameter.Name, out var value))
            {
                if (parameter.Required)
                {
                    throw new InvalidOperationException($"SSH command argument is required: {parameter.Name}");
                }

                continue;
            }

            parameter.Validate(value);
            renderedCommand = renderedCommand.Replace("{" + parameter.Name + "}", QuoteShellArgument(value), StringComparison.Ordinal);
        }

        if (renderedCommand.Contains('{', StringComparison.Ordinal) || renderedCommand.Contains('}', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"SSH command template has unresolved arguments: {Name}");
        }

        return renderedCommand;
    }

    private static void ValidateSafeTemplate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("SSH command template is required.");
        }

        if (value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SSH command template contains a line break.");
        }
    }

    private static void ValidateSafeText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"SSH {label} is required.");
        }

        var hasDangerousFragment = DangerousFragments.Any(fragment => value.Contains(fragment, StringComparison.Ordinal));
        if (hasDangerousFragment)
        {
            throw new InvalidOperationException($"SSH {label} contains a dangerous fragment.");
        }
    }

    private static string QuoteShellArgument(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }
}
