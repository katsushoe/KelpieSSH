namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Validates SSH command requests and delegates execution to an SSH runner.
/// </summary>
public sealed class SshCommandService
{
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

    private IAllowedCommandCatalog ResolveAllowedCommandCatalog(SshConnectionProfile profile)
    {
        if (_allowedCommandCatalog is not null)
        {
            return _allowedCommandCatalog;
        }

        return AllowedCommandCatalog.CreateForProfile(profile, _commandProcessingProviders ?? []);
    }
}
