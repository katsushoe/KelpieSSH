namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Validates SSH command requests and delegates execution to an SSH runner.
/// </summary>
public sealed class SshCommandService
{
    private const string PersistentEnvironmentFilePath = "~/.kelpie/.env";

    private readonly IAllowedCommandCatalog? _allowedCommandCatalog;
    private readonly IReadOnlyCollection<ICommandProcessingProvider>? _commandProcessingProviders;
    private readonly KelpiePolicyEvaluator _policyEvaluator;
    private readonly RawShellCommandPolicy _rawShellCommandPolicy;
    private readonly ISshCommandRunner _sshCommandRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshCommandService"/> class.
    /// </summary>
    /// <param name="allowedCommandCatalog">The allowed command catalog.</param>
    /// <param name="sshCommandRunner">The SSH command runner.</param>
    public SshCommandService(IAllowedCommandCatalog allowedCommandCatalog, ISshCommandRunner sshCommandRunner)
        : this(allowedCommandCatalog, KelpiePolicyEvaluator.Default, sshCommandRunner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshCommandService"/> class.
    /// </summary>
    /// <param name="commandProcessingProviders">The command-processing providers.</param>
    /// <param name="sshCommandRunner">The SSH command runner.</param>
    public SshCommandService(
        IReadOnlyCollection<ICommandProcessingProvider> commandProcessingProviders,
        ISshCommandRunner sshCommandRunner)
        : this(commandProcessingProviders, KelpiePolicyEvaluator.Default, sshCommandRunner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshCommandService"/> class.
    /// </summary>
    /// <param name="allowedCommandCatalog">The allowed command catalog.</param>
    /// <param name="policyEvaluator">The Kelpie policy evaluator.</param>
    /// <param name="sshCommandRunner">The SSH command runner.</param>
    public SshCommandService(
        IAllowedCommandCatalog allowedCommandCatalog,
        KelpiePolicyEvaluator policyEvaluator,
        ISshCommandRunner sshCommandRunner)
        : this(allowedCommandCatalog, policyEvaluator, RawShellCommandPolicy.Default, sshCommandRunner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshCommandService"/> class.
    /// </summary>
    /// <param name="allowedCommandCatalog">The allowed command catalog.</param>
    /// <param name="policyEvaluator">The Kelpie policy evaluator.</param>
    /// <param name="rawShellCommandPolicy">The raw shell command policy.</param>
    /// <param name="sshCommandRunner">The SSH command runner.</param>
    public SshCommandService(
        IAllowedCommandCatalog allowedCommandCatalog,
        KelpiePolicyEvaluator policyEvaluator,
        RawShellCommandPolicy rawShellCommandPolicy,
        ISshCommandRunner sshCommandRunner)
    {
        _allowedCommandCatalog = allowedCommandCatalog;
        _policyEvaluator = policyEvaluator;
        _rawShellCommandPolicy = rawShellCommandPolicy;
        _sshCommandRunner = sshCommandRunner;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshCommandService"/> class.
    /// </summary>
    /// <param name="commandProcessingProviders">The command-processing providers.</param>
    /// <param name="policyEvaluator">The Kelpie policy evaluator.</param>
    /// <param name="sshCommandRunner">The SSH command runner.</param>
    public SshCommandService(
        IReadOnlyCollection<ICommandProcessingProvider> commandProcessingProviders,
        KelpiePolicyEvaluator policyEvaluator,
        ISshCommandRunner sshCommandRunner)
        : this(commandProcessingProviders, policyEvaluator, RawShellCommandPolicy.Default, sshCommandRunner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SshCommandService"/> class.
    /// </summary>
    /// <param name="commandProcessingProviders">The command-processing providers.</param>
    /// <param name="policyEvaluator">The Kelpie policy evaluator.</param>
    /// <param name="rawShellCommandPolicy">The raw shell command policy.</param>
    /// <param name="sshCommandRunner">The SSH command runner.</param>
    public SshCommandService(
        IReadOnlyCollection<ICommandProcessingProvider> commandProcessingProviders,
        KelpiePolicyEvaluator policyEvaluator,
        RawShellCommandPolicy rawShellCommandPolicy,
        ISshCommandRunner sshCommandRunner)
    {
        _commandProcessingProviders = commandProcessingProviders;
        _policyEvaluator = policyEvaluator;
        _rawShellCommandPolicy = rawShellCommandPolicy;
        _sshCommandRunner = sshCommandRunner;
    }

    /// <summary>
    /// Executes an allowed SSH command against a profile.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="commandName">The allowed command name.</param>
    /// <param name="arguments">The optional command arguments.</param>
    /// <param name="timeout">The optional timeout override.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> ExecuteAsync(
        SshConnectionProfile profile,
        string commandName,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            profile,
            commandName,
            arguments: null,
            timeout,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Executes one SSH remote operation.
    /// </summary>
    /// <param name="operation">The remote operation request.</param>
    /// <param name="channel">The execution channel.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> ExecuteAsync(
        SshRemoteOperation operation,
        KelpieExecutionChannel channel = KelpieExecutionChannel.Cli,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var profile = operation.ToConnectionProfile();
        var timeout = operation.Options?.TimeoutSeconds is null
            ? (TimeSpan?)null
            : TimeSpan.FromSeconds(operation.Options.TimeoutSeconds.Value);

        if (string.Equals(operation.Operation.Kind, "managed", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteAsync(
                profile,
                operation.Operation.Name,
                operation.Operation.Arguments,
                timeout,
                channel,
                cancellationToken);
        }

        if (string.Equals(operation.Operation.Kind, "raw", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteRawAsync(
                profile,
                operation.Operation.Name,
                timeout,
                channel,
                cancellationToken);
        }

        throw new InvalidOperationException($"SSH remote operation kind is not supported: {operation.Operation.Kind}");
    }

    /// <summary>
    /// Executes an allowed SSH command against a profile.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="commandName">The allowed command name.</param>
    /// <param name="arguments">The optional command arguments.</param>
    /// <param name="timeout">The optional timeout override.</param>
    /// <param name="channel">The execution channel.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> ExecuteAsync(
        SshConnectionProfile profile,
        string commandName,
        IReadOnlyDictionary<string, string>? arguments = null,
        TimeSpan? timeout = null,
        KelpieExecutionChannel channel = KelpieExecutionChannel.Cli,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.Equals(profile.UserName, "root", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Direct root SSH login is not allowed.");
        }

        var allowedCommandCatalog = ResolveAllowedCommandCatalog(profile);
        if (!allowedCommandCatalog.TryGet(commandName, out var command))
        {
            throw new InvalidOperationException($"SSH command is not allowed: {commandName}");
        }

        var commandArguments = arguments is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(arguments, StringComparer.OrdinalIgnoreCase);
        var commandText = command.BuildCommandText(commandArguments);
        _policyEvaluator.EnsureAllowed(profile, command, commandText, channel);

        var request = new SshCommandRequest(
            profile,
            command.Name,
            commandText,
            timeout ?? command.DefaultTimeout,
            commandArguments);

        return await _sshCommandRunner.ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Creates a validated preview for an allowed SSH command without executing it.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="commandName">The allowed command name.</param>
    /// <param name="arguments">The optional command arguments.</param>
    /// <param name="channel">The execution channel.</param>
    /// <returns>The SSH command preview.</returns>
    public SshCommandPreview Preview(
        SshConnectionProfile profile,
        string commandName,
        IReadOnlyDictionary<string, string>? arguments = null,
        KelpieExecutionChannel channel = KelpieExecutionChannel.Cli)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.Equals(profile.UserName, "root", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Direct root SSH login is not allowed.");
        }

        var allowedCommandCatalog = ResolveAllowedCommandCatalog(profile);
        if (!allowedCommandCatalog.TryGet(commandName, out var command))
        {
            throw new InvalidOperationException($"SSH command is not allowed: {commandName}");
        }

        var commandArguments = arguments is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(arguments, StringComparer.OrdinalIgnoreCase);
        var commandText = command.BuildCommandText(commandArguments);
        _policyEvaluator.EnsureAllowed(profile, command, commandText, channel);

        return new SshCommandPreview(
            command.Name,
            commandText,
            command.RiskLevel,
            command.RiskLevel == SshCommandRiskLevel.ConfirmRequired,
            commandArguments);
    }

    /// <summary>
    /// Gets the risk level for one allowed SSH command without rendering its arguments.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="commandName">The allowed command name.</param>
    /// <returns>The command risk level.</returns>
    public SshCommandRiskLevel GetRiskLevel(SshConnectionProfile profile, string commandName)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var allowedCommandCatalog = ResolveAllowedCommandCatalog(profile);
        if (!allowedCommandCatalog.TryGet(commandName, out var command))
        {
            throw new InvalidOperationException($"SSH command is not allowed: {commandName}");
        }

        return command.RiskLevel;
    }

    /// <summary>
    /// Creates a validated preview for one SSH remote operation without executing it.
    /// </summary>
    /// <param name="operation">The remote operation request.</param>
    /// <param name="channel">The execution channel.</param>
    /// <returns>The SSH command preview.</returns>
    public SshCommandPreview Preview(
        SshRemoteOperation operation,
        KelpieExecutionChannel channel = KelpieExecutionChannel.Cli)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!string.Equals(operation.Operation.Kind, "managed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SSH remote operation preview is not supported for kind: {operation.Operation.Kind}");
        }

        return Preview(
            operation.ToConnectionProfile(),
            operation.Operation.Name,
            operation.Operation.Arguments,
            channel);
    }

    /// <summary>
    /// Executes raw shell command text after applying Kelpie policy.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="commandText">The raw shell command text.</param>
    /// <param name="timeout">The optional timeout override.</param>
    /// <param name="channel">The execution channel.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> ExecuteRawAsync(
        SshConnectionProfile profile,
        string commandText,
        TimeSpan? timeout = null,
        KelpieExecutionChannel channel = KelpieExecutionChannel.Cli,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.Equals(profile.UserName, "root", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Direct root SSH login is not allowed.");
        }

        var trimmedCommandText = commandText.Trim();
        _rawShellCommandPolicy.EnsureAllowed(profile, trimmedCommandText, channel);

        var request = new SshCommandRequest(
            profile,
            "raw_shell",
            trimmedCommandText,
            timeout ?? TimeSpan.FromSeconds(30),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        return await _sshCommandRunner.ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Lists environment variable keys visible to the selected SSH user.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="timeout">The optional timeout override.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> GetEnvironmentKeysAsync(
        SshConnectionProfile profile,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureNonRoot(profile);
        EnsureCapability(profile, KelpiePolicyNames.AllowPeekEnvironmentKeys);

        var request = new SshCommandRequest(
            profile,
            "get_environment_keys",
            "printenv | cut -d= -f1 | sort",
            timeout ?? TimeSpan.FromSeconds(10),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var result = await _sshCommandRunner.ExecuteAsync(request, cancellationToken);
        var hiddenKeys = profile.EnvironmentValues
            .Where(rule => rule.IsHidden)
            .Select(rule => rule.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (hiddenKeys.Count == 0 || string.IsNullOrEmpty(result.StandardOutput))
        {
            return result;
        }

        var filteredOutput = string.Join(
            Environment.NewLine,
            result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(key => !hiddenKeys.Contains(key)));
        if (!string.IsNullOrEmpty(filteredOutput))
        {
            filteredOutput += Environment.NewLine;
        }

        return result with { StandardOutput = filteredOutput };
    }

    /// <summary>
    /// Reads one environment variable value when profile rules permit it.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="key">The environment variable key.</param>
    /// <param name="timeout">The optional timeout override.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> PeekEnvironmentValueAsync(
        SshConnectionProfile profile,
        string key,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureNonRoot(profile);
        EnsureCapability(profile, KelpiePolicyNames.AllowPeekEnvironmentValues);
        ValidateEnvironmentKey(key);

        var rule = FindEnvironmentValueRule(profile, key)
            ?? throw new KelpiePolicyError($"environment value is not allowed: {key}");
        if (rule.IsHidden || rule.Access.HasFlag(EnvironmentValueAccess.KeyOnly))
        {
            throw new KelpiePolicyError($"environment value is not allowed: {key}");
        }

        var request = new SshCommandRequest(
            profile,
            "peek_environment_value",
            $"printenv {QuoteShellArgument(key)}",
            timeout ?? TimeSpan.FromSeconds(10),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["key"] = key,
            });
        var result = await _sshCommandRunner.ExecuteAsync(request, cancellationToken);
        if (!rule.Access.HasFlag(EnvironmentValueAccess.Masked))
        {
            if (!rule.AllowsPeekValue)
            {
                throw new KelpiePolicyError($"environment value is not allowed: {key}");
            }

            return result;
        }

        var value = result.StandardOutput.TrimEnd('\r', '\n');
        var maskedValue = value.Length == 0
            ? "(empty)"
            : new string('*', value.Length);
        return result with
        {
            StandardOutput = $"{maskedValue} (length={value.Length}){Environment.NewLine}",
        };
    }

    /// <summary>
    /// Executes one command with one environment variable value set for that execution only.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="key">The environment variable key.</param>
    /// <param name="value">The environment variable value.</param>
    /// <param name="commandText">The command to execute.</param>
    /// <param name="timeout">The optional timeout override.</param>
    /// <param name="channel">The execution channel.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> SetEnvironmentValueAsync(
        SshConnectionProfile profile,
        string key,
        string value,
        string commandText,
        TimeSpan? timeout = null,
        KelpieExecutionChannel channel = KelpieExecutionChannel.Cli,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureNonRoot(profile);
        EnsureCapability(profile, KelpiePolicyNames.AllowSetEnvironmentValues);
        ValidateEnvironmentKey(key);

        var rule = FindEnvironmentValueRule(profile, key)
            ?? throw new KelpiePolicyError($"environment value set is not allowed: {key}");
        if (!rule.AllowsSetValue || rule.IsHidden)
        {
            throw new KelpiePolicyError($"environment value set is not allowed: {key}");
        }

        var trimmedCommandText = commandText.Trim();
        _rawShellCommandPolicy.EnsureAllowed(profile, trimmedCommandText, channel);
        var request = new SshCommandRequest(
            profile,
            "set_environment_value",
            $"if [ -f {PersistentEnvironmentFilePath} ]; then . {PersistentEnvironmentFilePath}; fi; env {key}={QuoteShellArgument(value)} {trimmedCommandText}",
            timeout ?? TimeSpan.FromSeconds(30),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["key"] = key,
                ["command"] = trimmedCommandText,
            });

        return await _sshCommandRunner.ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Lists persistent environment variable keys managed in the remote Kelpie env file.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="timeout">The optional timeout override.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> ListPersistentEnvironmentKeysAsync(
        SshConnectionProfile profile,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureNonRoot(profile);
        EnsureCapability(profile, KelpiePolicyNames.AllowPeekEnvironmentKeys);

        var request = new SshCommandRequest(
            profile,
            "list_persistent_environment_keys",
            $"if [ -f {PersistentEnvironmentFilePath} ]; then sed -n 's/^\\([A-Za-z_][A-Za-z0-9_]*\\)=.*/\\1/p' {PersistentEnvironmentFilePath} | sort; fi",
            timeout ?? TimeSpan.FromSeconds(10),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var result = await _sshCommandRunner.ExecuteAsync(request, cancellationToken);
        var hiddenKeys = profile.EnvironmentValues
            .Where(rule => rule.IsHidden)
            .Select(rule => rule.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (hiddenKeys.Count == 0 || string.IsNullOrEmpty(result.StandardOutput))
        {
            return result;
        }

        var filteredOutput = string.Join(
            Environment.NewLine,
            result.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(key => !hiddenKeys.Contains(key)));
        if (!string.IsNullOrEmpty(filteredOutput))
        {
            filteredOutput += Environment.NewLine;
        }

        return result with { StandardOutput = filteredOutput };
    }

    /// <summary>
    /// Persists one environment variable value in the remote Kelpie env file.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="key">The environment variable key.</param>
    /// <param name="value">The environment variable value.</param>
    /// <param name="timeout">The optional timeout override.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> PersistEnvironmentValueAsync(
        SshConnectionProfile profile,
        string key,
        string value,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureNonRoot(profile);
        EnsureCapability(profile, KelpiePolicyNames.AllowSetEnvironmentValues);
        ValidateEnvironmentKey(key);

        var rule = FindEnvironmentValueRule(profile, key)
            ?? throw new KelpiePolicyError($"environment value persist is not allowed: {key}");
        if (!rule.AllowsSetValue || rule.IsHidden)
        {
            throw new KelpiePolicyError($"environment value persist is not allowed: {key}");
        }

        var line = $"{key}={QuoteShellArgument(value)}";
        var commandText = string.Join(" && ", [
            "mkdir -p ~/.kelpie",
            $"touch {PersistentEnvironmentFilePath}",
            $"backup={PersistentEnvironmentFilePath}.$(date -u +%Y%m%dT%H%M%SZ).kelpie",
            $"cp {PersistentEnvironmentFilePath} \"$backup\"",
            $"awk -F= -v key={QuoteShellArgument(key)} '$1 != key {{ print }}' {PersistentEnvironmentFilePath} > {PersistentEnvironmentFilePath}.tmp",
            $"printf '%s\\n' {QuoteShellArgument(line)} >> {PersistentEnvironmentFilePath}.tmp",
            $"mv {PersistentEnvironmentFilePath}.tmp {PersistentEnvironmentFilePath}",
            $"chmod 600 {PersistentEnvironmentFilePath}",
            "printf 'Updated ~/.kelpie/.env\\nBackup: %s\\n' \"$backup\"",
        ]);
        var request = new SshCommandRequest(
            profile,
            "persist_environment_value",
            commandText,
            timeout ?? TimeSpan.FromSeconds(10),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["key"] = key,
            });

        return await _sshCommandRunner.ExecuteAsync(request, cancellationToken);
    }

    /// <summary>
    /// Removes one environment variable from the remote Kelpie env file.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="key">The environment variable key.</param>
    /// <param name="timeout">The optional timeout override.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The SSH command result.</returns>
    public async Task<SshCommandResult> RemovePersistentEnvironmentValueAsync(
        SshConnectionProfile profile,
        string key,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureNonRoot(profile);
        EnsureCapability(profile, KelpiePolicyNames.AllowSetEnvironmentValues);
        ValidateEnvironmentKey(key);

        var rule = FindEnvironmentValueRule(profile, key)
            ?? throw new KelpiePolicyError($"environment value remove is not allowed: {key}");
        if (!rule.AllowsSetValue || rule.IsHidden)
        {
            throw new KelpiePolicyError($"environment value remove is not allowed: {key}");
        }

        var commandText = string.Join(" && ", [
            "mkdir -p ~/.kelpie",
            $"touch {PersistentEnvironmentFilePath}",
            $"backup={PersistentEnvironmentFilePath}.$(date -u +%Y%m%dT%H%M%SZ).kelpie",
            $"cp {PersistentEnvironmentFilePath} \"$backup\"",
            $"awk -F= -v key={QuoteShellArgument(key)} '$1 != key {{ print }}' {PersistentEnvironmentFilePath} > {PersistentEnvironmentFilePath}.tmp",
            $"mv {PersistentEnvironmentFilePath}.tmp {PersistentEnvironmentFilePath}",
            $"chmod 600 {PersistentEnvironmentFilePath}",
            "printf 'Removed from ~/.kelpie/.env\\nBackup: %s\\n' \"$backup\"",
        ]);
        var request = new SshCommandRequest(
            profile,
            "remove_persistent_environment_value",
            commandText,
            timeout ?? TimeSpan.FromSeconds(10),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["key"] = key,
            });

        return await _sshCommandRunner.ExecuteAsync(request, cancellationToken);
    }

    private IAllowedCommandCatalog ResolveAllowedCommandCatalog(SshConnectionProfile profile)
    {
        if (_allowedCommandCatalog is not null)
        {
            return _allowedCommandCatalog;
        }

        return AllowedCommandCatalog.CreateForProfile(profile, _commandProcessingProviders ?? []);
    }

    private static void EnsureNonRoot(SshConnectionProfile profile)
    {
        if (string.Equals(profile.UserName, "root", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Direct root SSH login is not allowed.");
        }
    }

    private static void EnsureCapability(SshConnectionProfile profile, string capability)
    {
        if (!profile.Capabilities.Allows(capability))
        {
            throw new KelpiePolicyError($"{capability} is required.");
        }
    }

    private static EnvironmentValueRule? FindEnvironmentValueRule(SshConnectionProfile profile, string key)
    {
        return profile.EnvironmentValues.FirstOrDefault(rule =>
            string.Equals(rule.Key, key, StringComparison.Ordinal));
    }

    private static void ValidateEnvironmentKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Environment variable key is required.");
        }

        if (!key.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_') || char.IsDigit(key[0]))
        {
            throw new InvalidOperationException($"Environment variable key is invalid: {key}");
        }
    }

    private static string QuoteShellArgument(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }
}
