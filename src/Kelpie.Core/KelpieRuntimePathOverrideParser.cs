namespace Kelpie.Core;

/// <summary>
/// Parses common runtime directory override options.
/// </summary>
public static class KelpieRuntimePathOverrideParser
{
    private static readonly HashSet<string> OptionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "--config-dir",
        "--profiles-dir",
        "--logs-dir",
        "--bin-dir",
        "--keys-dir",
        "--dat-dir",
    };

    /// <summary>
    /// Parses runtime path overrides and returns the remaining command arguments.
    /// </summary>
    /// <param name="args">The original command arguments.</param>
    /// <param name="remainingArgs">The command arguments after removing override options.</param>
    /// <param name="overrides">The parsed path overrides.</param>
    /// <param name="errorMessage">The parse error message.</param>
    /// <returns><c>true</c> when parsing succeeds.</returns>
    public static bool TryParse(
        string[] args,
        out string[] remainingArgs,
        out KelpieRuntimePathOverrides overrides,
        out string? errorMessage)
    {
        var remaining = new List<string>(args.Length);
        string? configDirectory = null;
        string? profilesDirectory = null;
        string? logsDirectory = null;
        string? binDirectory = null;
        string? keysDirectory = null;
        string? dataDirectory = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--", StringComparison.Ordinal))
            {
                remaining.AddRange(args[index..]);
                break;
            }

            if (!IsOptionName(arg))
            {
                remaining.Add(arg);
                continue;
            }

            if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
            {
                remainingArgs = args;
                overrides = KelpieRuntimePathOverrides.Empty;
                errorMessage = $"Missing directory value for {arg}.";
                return false;
            }

            var value = Path.GetFullPath(args[++index]);
            switch (arg)
            {
                case var option when string.Equals(option, "--config-dir", StringComparison.OrdinalIgnoreCase):
                    configDirectory = AssignOnce(configDirectory, value, arg, out errorMessage);
                    break;
                case var option when string.Equals(option, "--profiles-dir", StringComparison.OrdinalIgnoreCase):
                    profilesDirectory = AssignOnce(profilesDirectory, value, arg, out errorMessage);
                    break;
                case var option when string.Equals(option, "--logs-dir", StringComparison.OrdinalIgnoreCase):
                    logsDirectory = AssignOnce(logsDirectory, value, arg, out errorMessage);
                    break;
                case var option when string.Equals(option, "--bin-dir", StringComparison.OrdinalIgnoreCase):
                    binDirectory = AssignOnce(binDirectory, value, arg, out errorMessage);
                    break;
                case var option when string.Equals(option, "--keys-dir", StringComparison.OrdinalIgnoreCase):
                    keysDirectory = AssignOnce(keysDirectory, value, arg, out errorMessage);
                    break;
                case var option when string.Equals(option, "--dat-dir", StringComparison.OrdinalIgnoreCase):
                    dataDirectory = AssignOnce(dataDirectory, value, arg, out errorMessage);
                    break;
                default:
                    errorMessage = $"Unknown runtime path option: {arg}.";
                    break;
            }

            if (errorMessage is not null)
            {
                remainingArgs = args;
                overrides = KelpieRuntimePathOverrides.Empty;
                return false;
            }
        }

        remainingArgs = remaining.ToArray();
        overrides = new KelpieRuntimePathOverrides(
            configDirectory,
            profilesDirectory,
            logsDirectory,
            binDirectory,
            keysDirectory,
            dataDirectory);
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Converts overrides to command-line arguments.
    /// </summary>
    /// <param name="overrides">The overrides to convert.</param>
    /// <returns>The command-line arguments.</returns>
    public static IReadOnlyCollection<string> ToArguments(KelpieRuntimePathOverrides overrides)
    {
        var args = new List<string>();
        Add(args, "--config-dir", overrides.ConfigDirectory);
        Add(args, "--profiles-dir", overrides.ProfilesDirectory);
        Add(args, "--logs-dir", overrides.LogsDirectory);
        Add(args, "--bin-dir", overrides.BinDirectory);
        Add(args, "--keys-dir", overrides.KeysDirectory);
        Add(args, "--dat-dir", overrides.DataDirectory);
        return args;
    }

    private static bool IsOptionName(string arg)
    {
        return OptionNames.Contains(arg);
    }

    private static string? AssignOnce(string? currentValue, string value, string optionName, out string? errorMessage)
    {
        if (currentValue is not null)
        {
            errorMessage = $"{optionName} was specified more than once.";
            return currentValue;
        }

        errorMessage = null;
        return value;
    }

    private static void Add(List<string> args, string optionName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add(optionName);
        args.Add(value);
    }
}
