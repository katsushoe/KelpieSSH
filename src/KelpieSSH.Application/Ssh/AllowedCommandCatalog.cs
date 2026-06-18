namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Provides a validated catalog of SSH commands that KelpieSSH may execute.
/// </summary>
public sealed class AllowedCommandCatalog : IAllowedCommandCatalog
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
    private static readonly string[] DeniedExecutableNames =
    [
        "rm",
        "reboot",
        "shutdown",
        "halt",
        "poweroff",
        "mkfs",
        "dd",
        "su",
        "chmod",
        "chown",
        "passwd",
    ];

    private readonly Dictionary<string, AllowedCommandDefinition> _commands;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllowedCommandCatalog"/> class.
    /// </summary>
    /// <param name="commands">The command definitions.</param>
    public AllowedCommandCatalog(IEnumerable<AllowedCommandDefinition> commands)
    {
        _commands = commands.ToDictionary(command => command.Name, ValidateCommand, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates the default read-only diagnostic command catalog.
    /// </summary>
    /// <returns>The default command catalog.</returns>
    public static AllowedCommandCatalog CreateDefault()
    {
        return CreateForProfile(
            CreateSyntheticProfile(),
            [
                new CommonDiagnosticCommandProvider(),
                new NginxServiceConfigCommandProvider(),
                new DebianNginxCommandProvider(),
            ]);
    }

    /// <summary>
    /// Creates a command catalog for the profile from command-processing providers.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="commandProcessingProviders">The command-processing providers.</param>
    /// <returns>The profile-specific command catalog.</returns>
    public static AllowedCommandCatalog CreateForProfile(
        SshConnectionProfile profile,
        IReadOnlyCollection<ICommandProcessingProvider> commandProcessingProviders)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(commandProcessingProviders);

        var commands = commandProcessingProviders
            .Where(provider => provider.Supports(profile))
            .SelectMany(provider => provider.GetCommands(profile))
            .ToArray();

        return new AllowedCommandCatalog(commands);
    }

    /// <inheritdoc />
    public bool TryGet(string name, out AllowedCommandDefinition command)
    {
        if (!IsSafeCommandName(name))
        {
            command = default!;
            return false;
        }

        return _commands.TryGetValue(name, out command!);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<AllowedCommandDefinition> List()
    {
        return _commands.Values.OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsSafeCommandName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return !DangerousFragments.Any(fragment => name.Contains(fragment, StringComparison.Ordinal));
    }

    private static AllowedCommandDefinition ValidateCommand(AllowedCommandDefinition command)
    {
        if (!IsSafeCommandName(command.Name))
        {
            throw new InvalidOperationException($"SSH command name is unsafe: {command.Name}");
        }

        if (string.IsNullOrWhiteSpace(command.CommandTemplate))
        {
            throw new InvalidOperationException($"SSH command template is required: {command.Name}");
        }

        if (command.DefaultTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"SSH command timeout must be positive: {command.Name}");
        }

        var executableName = command.CommandTemplate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (executableName is null)
        {
            throw new InvalidOperationException($"SSH command executable is required: {command.Name}");
        }

        if (DeniedExecutableNames.Contains(executableName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SSH command executable is denied: {executableName}");
        }

        ValidateCommandParameters(command);
        return command;
    }

    private static void ValidateCommandParameters(AllowedCommandDefinition command)
    {
        var parameters = command.Parameters ?? [];
        var parameterNames = parameters.Select(parameter => parameter.Name).ToArray();
        if (parameterNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != parameterNames.Length)
        {
            throw new InvalidOperationException($"SSH command argument is duplicated: {command.Name}");
        }

        foreach (var parameter in parameters)
        {
            parameter.ValidateDefinition();
        }

        var templateParameterNames = GetTemplateParameterNames(command.CommandTemplate).ToArray();
        foreach (var templateParameterName in templateParameterNames)
        {
            if (!parameterNames.Contains(templateParameterName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SSH command template references an undefined argument: {templateParameterName}");
            }
        }

        foreach (var parameterName in parameterNames)
        {
            if (!templateParameterNames.Contains(parameterName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SSH command argument is not used by template: {parameterName}");
            }
        }
    }

    private static IEnumerable<string> GetTemplateParameterNames(string commandTemplate)
    {
        var startIndex = -1;
        for (var index = 0; index < commandTemplate.Length; index++)
        {
            if (commandTemplate[index] == '{')
            {
                startIndex = index + 1;
                continue;
            }

            if (commandTemplate[index] == '}' && startIndex >= 0)
            {
                var length = index - startIndex;
                if (length > 0)
                {
                    yield return commandTemplate.Substring(startIndex, length);
                }

                startIndex = -1;
            }
        }
    }

    private static SshConnectionProfile CreateSyntheticProfile()
    {
        return new SshConnectionProfile
        {
            Name = "synthetic",
            Host = "example.invalid",
            UserName = "kelpie",
            PrivateKeyPath = "id_ed25519",
            OsFamily = "linux",
            PackageManager = "none",
            Capabilities = PolicySet.Empty,
        };
    }
}
