namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Evaluates raw shell command text for interactive CLI sessions.
/// </summary>
public sealed class RawShellCommandPolicy
{
    private static readonly string[] ShellControlFragments =
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
    private static readonly string[] ReadOnlyExecutables =
    [
        "awk",
        "cat",
        "clear",
        "date",
        "df",
        "dnf",
        "du",
        "echo",
        "find",
        "free",
        "grep",
        "head",
        "hostname",
        "id",
        "ip",
        "journalctl",
        "less",
        "ls",
        "more",
        "ps",
        "pwd",
        "rpm",
        "sed",
        "ss",
        "stat",
        "systemctl",
        "tail",
        "top",
        "uname",
        "uptime",
        "who",
        "whoami",
        "yum",
    ];

    /// <summary>
    /// Gets the default raw shell command policy.
    /// </summary>
    public static RawShellCommandPolicy Default { get; } = new();

    /// <summary>
    /// Throws when raw command text is not allowed for the profile and channel.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="commandText">The raw shell command text.</param>
    /// <param name="channel">The execution channel.</param>
    public void EnsureAllowed(
        SshConnectionProfile profile,
        string commandText,
        KelpieExecutionChannel channel)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var text = commandText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new KelpiePolicyError("command is empty.");
        }

        if (ShellControlFragments.Any(fragment => text.Contains(fragment, StringComparison.Ordinal)))
        {
            throw new KelpiePolicyError("shell control operators are not allowed in interactive command.");
        }

        var tokens = SplitTokens(text);
        if (tokens.Length == 0)
        {
            throw new KelpiePolicyError("command is empty.");
        }

        var executable = tokens[0];
        var capabilities = CreateEffectiveCapabilities(profile, channel);
        EnsureSpecialPolicy(text, executable, capabilities, channel);
        EnsureModeAllows(profile, text, executable, capabilities);
    }

    private static void EnsureSpecialPolicy(
        string commandText,
        string executable,
        PolicySet capabilities,
        KelpieExecutionChannel channel)
    {
        if (IsPasswordDisplayCommand(commandText))
        {
            if (channel == KelpieExecutionChannel.Mcp)
            {
                throw new KelpiePolicyError("secrets cannot be displayed through MCP.");
            }

            EnsurePolicy(capabilities, KelpiePolicyNames.AllowShowPassword, executable);
        }

        if (IsPrivateKeyDisplayCommand(commandText))
        {
            if (channel == KelpieExecutionChannel.Mcp)
            {
                throw new KelpiePolicyError("secrets cannot be displayed through MCP.");
            }

            EnsurePolicy(capabilities, KelpiePolicyNames.AllowShowPrivateKey, executable);
        }

        if (string.Equals(executable, "sudo", StringComparison.OrdinalIgnoreCase))
        {
            EnsurePolicy(capabilities, KelpiePolicyNames.AllowSudo, executable);
        }

        if (string.Equals(executable, "alias", StringComparison.OrdinalIgnoreCase))
        {
            EnsurePolicy(capabilities, KelpiePolicyNames.AllowAlias, executable);
        }
    }

    private static void EnsureModeAllows(
        SshConnectionProfile profile,
        string commandText,
        string executable,
        PolicySet capabilities)
    {
        if (IsAlwaysForbiddenExecutable(executable))
        {
            throw new KelpiePolicyError($"command is forbidden: {executable}");
        }

        if (IsChangeDirectoryCommand(executable))
        {
            EnsureChangeDirectoryAllowed(profile, commandText);
            return;
        }

        if (IsShellExitExecutable(executable))
        {
            return;
        }

        if (IsPackageInstallCommand(commandText))
        {
            EnsurePolicy(capabilities, KelpiePolicyNames.AllowInstallPackage, executable);
            return;
        }

        if (IsPackageRemoveCommand(commandText))
        {
            EnsurePolicy(capabilities, KelpiePolicyNames.AllowRemovePackage, executable);
            return;
        }

        if (IsPackageUpdateIndexCommand(commandText))
        {
            EnsurePolicy(capabilities, KelpiePolicyNames.AllowUpdatePackageIndex, executable);
            return;
        }

        if (IsPackageListCommand(commandText))
        {
            EnsurePolicy(capabilities, KelpiePolicyNames.AllowListPackage, executable);
            return;
        }

        if (string.Equals(executable, "rm", StringComparison.OrdinalIgnoreCase))
        {
            EnsurePolicy(capabilities, KelpiePolicyNames.AllowDeleteFiles, executable);
            EnsureDeleteTargetsAllowed(profile, commandText);
            return;
        }

        if (string.Equals(executable, "mv", StringComparison.OrdinalIgnoreCase))
        {
            EnsurePolicy(capabilities, KelpiePolicyNames.AllowMoveFiles, executable);
            return;
        }

        if (IsReadOnlyExecutable(executable))
        {
            return;
        }

        if (profile.Mode == KelpiePolicyMode.Expert)
        {
            return;
        }

        throw new KelpiePolicyError($"command is not allowed in {profile.Mode} mode: {executable}");
    }

    private static PolicySet CreateEffectiveCapabilities(SshConnectionProfile profile, KelpieExecutionChannel channel)
    {
        var modeCapabilities = profile.Mode switch
        {
            KelpiePolicyMode.ReadOnly => [KelpiePolicyNames.AllowListPackage],
            KelpiePolicyMode.Safe => [KelpiePolicyNames.AllowListPackage],
            KelpiePolicyMode.Maintenance =>
            [
                KelpiePolicyNames.AllowListPackage,
                KelpiePolicyNames.AllowUpdatePackageIndex,
                KelpiePolicyNames.AllowInstallPackage,
                KelpiePolicyNames.AllowRemovePackage,
            ],
            KelpiePolicyMode.Expert => channel == KelpieExecutionChannel.Mcp
                ? KelpiePolicyNames.List().Where(name =>
                    !string.Equals(name, KelpiePolicyNames.AllowShowPassword, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, KelpiePolicyNames.AllowShowPrivateKey, StringComparison.OrdinalIgnoreCase))
                : KelpiePolicyNames.List(),
            _ => [],
        };

        return channel == KelpieExecutionChannel.Mcp
            ? PolicySet.FromNames(modeCapabilities)
            : PolicySet.FromNames(modeCapabilities.Concat(profile.Capabilities.List()));
    }

    private static string[] SplitTokens(string commandText)
    {
        return commandText.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsAlwaysForbiddenExecutable(string executable)
    {
        return string.Equals(executable, "reboot", StringComparison.OrdinalIgnoreCase)
            || string.Equals(executable, "shutdown", StringComparison.OrdinalIgnoreCase)
            || string.Equals(executable, "halt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(executable, "poweroff", StringComparison.OrdinalIgnoreCase)
            || string.Equals(executable, "passwd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(executable, "su", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadOnlyExecutable(string executable)
    {
        return ReadOnlyExecutables.Contains(executable, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsChangeDirectoryCommand(string executable)
    {
        return string.Equals(executable, "cd", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureChangeDirectoryAllowed(SshConnectionProfile profile, string commandText)
    {
        var tokens = SplitTokens(commandText);
        if (tokens.Length == 1)
        {
            return;
        }

        var target = tokens[1];
        var allowedRoots = GetAllowedRootRules(profile);
        if (allowedRoots.Count == 0 || HasGlobalAllowedRoot(allowedRoots, AllowedRootAccess.CD))
        {
            return;
        }

        if (!IsAbsolutePath(target, profile.OsFamily))
        {
            throw new KelpiePolicyError("relative cd is not allowed when AllowedRoots is configured.");
        }

        if (!AllowedRootMatcher.IsAllowed(target, allowedRoots, AllowedRootAccess.CD, profile.OsFamily))
        {
            throw new KelpiePolicyError($"cd target is outside AllowedRoots: {target}");
        }
    }

    private static void EnsureDeleteTargetsAllowed(SshConnectionProfile profile, string commandText)
    {
        var allowedRoots = GetAllowedRootRules(profile);
        if (allowedRoots.Count == 0 || HasGlobalAllowedRoot(allowedRoots, AllowedRootAccess.Write))
        {
            return;
        }

        var targets = SplitTokens(commandText)
            .Skip(1)
            .Where(token => !token.StartsWith("-", StringComparison.Ordinal))
            .ToArray();

        if (targets.Length == 0)
        {
            throw new KelpiePolicyError("rm target is required.");
        }

        foreach (var target in targets)
        {
            if (!IsAbsolutePath(target, profile.OsFamily))
            {
                throw new KelpiePolicyError("relative rm target is not allowed when AllowedRoots is configured.");
            }

            if (!AllowedRootMatcher.IsAllowed(target, allowedRoots, AllowedRootAccess.Write, profile.OsFamily))
            {
                throw new KelpiePolicyError($"rm target is outside writable AllowedRoots: {target}");
            }

            EnsureSpecialPathAllowsDelete(profile, target);
        }
    }

    private static void EnsureSpecialPathAllowsDelete(SshConnectionProfile profile, string target)
    {
        var action = SpecialPathMatcher.FindAction(target, profile.SpecialPaths, profile.OsFamily);
        if (action == SpecialPathAction.Deny)
        {
            throw new KelpiePolicyError($"rm target is denied by SpecialPaths: {target}");
        }

        if (action == SpecialPathAction.Confirm)
        {
            throw new KelpiePolicyError($"rm target requires confirmation by SpecialPaths: {target}");
        }
    }

    private static bool IsAbsolutePath(string path, string osFamily)
    {
        if (string.Equals(osFamily, "windows", StringComparison.OrdinalIgnoreCase))
        {
            return path.Length >= 3
                && char.IsLetter(path[0])
                && path[1] == ':'
                && (path[2] == '\\' || path[2] == '/');
        }

        return path.StartsWith("/", StringComparison.Ordinal);
    }

    private static bool HasGlobalAllowedRoot(IReadOnlyCollection<string> allowedRoots)
    {
        return allowedRoots.Any(root =>
            string.Equals(root, "*", StringComparison.Ordinal)
            || string.Equals(root, "**", StringComparison.Ordinal));
    }

    private static bool HasGlobalAllowedRoot(
        IReadOnlyCollection<AllowedRootRule> allowedRoots,
        AllowedRootAccess requiredAccess)
    {
        return allowedRoots.Any(root =>
            (string.Equals(root.Path, "*", StringComparison.Ordinal)
                || string.Equals(root.Path, "**", StringComparison.Ordinal))
            && root.Access.HasFlag(requiredAccess));
    }

    private static IReadOnlyCollection<AllowedRootRule> GetAllowedRootRules(SshConnectionProfile profile)
    {
        return profile.AllowedRootRules.Count > 0
            ? profile.AllowedRootRules
            : profile.AllowedRoots
                .Select(root => new AllowedRootRule(root, AllowedRootAccess.Read | AllowedRootAccess.List | AllowedRootAccess.CD))
                .ToArray();
    }

    private static bool IsShellExitExecutable(string executable)
    {
        return string.Equals(executable, "exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(executable, "logout", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPackageInstallCommand(string commandText)
    {
        return StartsWithCommand(commandText, "apt install")
            || StartsWithCommand(commandText, "apt-get install")
            || StartsWithCommand(commandText, "dnf install")
            || StartsWithCommand(commandText, "yum install");
    }

    private static bool IsPackageRemoveCommand(string commandText)
    {
        return StartsWithCommand(commandText, "apt remove")
            || StartsWithCommand(commandText, "apt purge")
            || StartsWithCommand(commandText, "apt-get remove")
            || StartsWithCommand(commandText, "apt-get purge")
            || StartsWithCommand(commandText, "dnf remove")
            || StartsWithCommand(commandText, "yum remove");
    }

    private static bool IsPackageUpdateIndexCommand(string commandText)
    {
        return StartsWithCommand(commandText, "apt update")
            || StartsWithCommand(commandText, "apt-get update")
            || StartsWithCommand(commandText, "dnf check-update")
            || StartsWithCommand(commandText, "yum check-update");
    }

    private static bool IsPackageListCommand(string commandText)
    {
        return StartsWithCommand(commandText, "apt list")
            || StartsWithCommand(commandText, "apt search")
            || StartsWithCommand(commandText, "apt show")
            || StartsWithCommand(commandText, "apt-cache search")
            || StartsWithCommand(commandText, "apt-cache show")
            || StartsWithCommand(commandText, "dnf list")
            || StartsWithCommand(commandText, "dnf search")
            || StartsWithCommand(commandText, "dnf info")
            || StartsWithCommand(commandText, "yum list")
            || StartsWithCommand(commandText, "yum search")
            || StartsWithCommand(commandText, "yum info")
            || StartsWithCommand(commandText, "rpm -q");
    }

    private static bool StartsWithCommand(string commandText, string commandStart)
    {
        return commandText.StartsWith(commandStart + " ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(commandText, commandStart, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPasswordDisplayCommand(string commandText)
    {
        return commandText.Contains("/etc/shadow", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrivateKeyDisplayCommand(string commandText)
    {
        return commandText.Contains("id_rsa", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("id_ed25519", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("BEGIN OPENSSH PRIVATE KEY", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("BEGIN RSA PRIVATE KEY", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsurePolicy(PolicySet capabilities, string policyName, string commandName)
    {
        if (!capabilities.Allows(policyName))
        {
            throw new KelpiePolicyError($"{policyName} is required for command: {commandName}");
        }
    }
}
