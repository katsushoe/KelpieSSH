namespace KelpieSSH.Application.Ssh;

/// <summary>
/// Evaluates Kelpie SSH command policy for CLI and MCP execution.
/// </summary>
public sealed class KelpiePolicyEvaluator
{
    /// <summary>
    /// Gets the default policy evaluator.
    /// </summary>
    public static KelpiePolicyEvaluator Default { get; } = new();

    /// <summary>
    /// Throws when the command is not allowed by the profile mode and channel policy.
    /// </summary>
    /// <param name="profile">The SSH connection profile.</param>
    /// <param name="command">The allowed command definition.</param>
    /// <param name="commandText">The rendered command text.</param>
    /// <param name="channel">The execution channel.</param>
    public void EnsureAllowed(
        SshConnectionProfile profile,
        AllowedCommandDefinition command,
        string commandText,
        KelpieExecutionChannel channel)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(command);

        var effectiveCapabilities = CreateEffectiveCapabilities(profile, channel);

        if (command.RiskLevel == SshCommandRiskLevel.Forbidden)
        {
            throw new KelpiePolicyError($"command is forbidden: {command.Name}");
        }

        if (IsSecretDisplayCommand(command.Name, commandText))
        {
            if (channel == KelpieExecutionChannel.Mcp)
            {
                throw new KelpiePolicyError("secrets cannot be displayed through MCP.");
            }

            var requiredPolicy = IsPrivateKeyDisplayCommand(command.Name, commandText)
                ? KelpiePolicyNames.AllowShowPrivateKey
                : KelpiePolicyNames.AllowShowPassword;
            EnsurePolicy(effectiveCapabilities, requiredPolicy, command.Name);
        }

        if (RequiresSudo(commandText))
        {
            if (!AllowsSudoByRole(profile, command))
            {
                EnsurePolicy(effectiveCapabilities, KelpiePolicyNames.AllowSudo, command.Name);
            }
        }

        var requiredPolicyName = GetRequiredPolicyName(command);
        if (requiredPolicyName is not null)
        {
            EnsurePolicy(effectiveCapabilities, requiredPolicyName, command.Name);
        }

        if (command.RiskLevel == SshCommandRiskLevel.ConfirmRequired && requiredPolicyName is null)
        {
            if (profile.Mode is not KelpiePolicyMode.Maintenance and not KelpiePolicyMode.Expert
                && !AllowsConfirmRequiredByRole(profile, command))
            {
                throw new KelpiePolicyError($"command requires confirmation and is not allowed in {profile.Mode} mode: {command.Name}");
            }
        }
    }

    private static PolicySet CreateEffectiveCapabilities(SshConnectionProfile profile, KelpieExecutionChannel channel)
    {
        var modePolicyNames = GetModePolicyNames(profile.Mode, channel);
        if (channel == KelpieExecutionChannel.Mcp)
        {
            return PolicySet.FromNames(modePolicyNames);
        }

        return PolicySet.FromNames(modePolicyNames.Concat(profile.Capabilities.List()));
    }

    private static IEnumerable<string> GetModePolicyNames(KelpiePolicyMode mode, KelpieExecutionChannel channel)
    {
        return mode switch
        {
            KelpiePolicyMode.ReadOnly => [KelpiePolicyNames.AllowListPackage],
            KelpiePolicyMode.Safe => [KelpiePolicyNames.AllowListPackage],
            KelpiePolicyMode.Maintenance =>
            [
                KelpiePolicyNames.AllowListPackage,
                KelpiePolicyNames.AllowUpdatePackageIndex,
                KelpiePolicyNames.AllowInstallPackage,
                KelpiePolicyNames.AllowRemovePackage,
                KelpiePolicyNames.AllowSudo,
            ],
            KelpiePolicyMode.Expert => GetExpertPolicyNames(channel),
            _ => [],
        };
    }

    private static IEnumerable<string> GetExpertPolicyNames(KelpieExecutionChannel channel)
    {
        var policyNames = KelpiePolicyNames.List();
        return channel == KelpieExecutionChannel.Mcp
            ? policyNames.Where(name =>
                !string.Equals(name, KelpiePolicyNames.AllowShowPassword, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, KelpiePolicyNames.AllowShowPrivateKey, StringComparison.OrdinalIgnoreCase))
            : policyNames;
    }

    private static string? GetRequiredPolicyName(AllowedCommandDefinition command)
    {
        return command.Name switch
        {
            "pkg_check_updates" => KelpiePolicyNames.AllowListPackage,
            "pkg_simulate_install" => KelpiePolicyNames.AllowListPackage,
            "pkg_simulate_remove" => KelpiePolicyNames.AllowListPackage,
            "pkg_install" => KelpiePolicyNames.AllowInstallPackage,
            "pkg_remove" => KelpiePolicyNames.AllowRemovePackage,
            "certbot_check_install" => KelpiePolicyNames.AllowListPackage,
            "certbot_install" => KelpiePolicyNames.AllowInstallPackage,
            _ => null,
        };
    }

    private static bool RequiresSudo(string commandText)
    {
        return commandText.StartsWith("sudo ", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains(" sudo ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowsSudoByRole(SshConnectionProfile profile, AllowedCommandDefinition command)
    {
        return HasRole(profile, KelpieRoleNames.WebAdmin)
            && IsWebAdminCommand(command.Name);
    }

    private static bool AllowsConfirmRequiredByRole(SshConnectionProfile profile, AllowedCommandDefinition command)
    {
        return HasRole(profile, KelpieRoleNames.WebAdmin)
            && IsWebAdminCommand(command.Name);
    }

    private static bool IsWebAdminCommand(string commandName)
    {
        return commandName is "service_config_nginx_write_config"
            or "service_config_nginx_check_write_config"
            or "service_config_nginx_test_config"
            or "service_reload"
            or "service_enable_now"
            or "service_restart"
            or "service_stop"
            or "service_disable";
    }

    private static bool HasRole(SshConnectionProfile profile, string role)
    {
        return profile.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private static void EnsurePolicy(PolicySet capabilities, string policyName, string commandName)
    {
        if (!capabilities.Allows(policyName))
        {
            throw new KelpiePolicyError($"{policyName} is required for command: {commandName}");
        }
    }

    private static bool IsSecretDisplayCommand(string commandName, string commandText)
    {
        return IsPasswordDisplayCommand(commandName, commandText)
            || IsPrivateKeyDisplayCommand(commandName, commandText);
    }

    private static bool IsPasswordDisplayCommand(string commandName, string commandText)
    {
        return ContainsToken(commandName, "password")
            || ContainsToken(commandName, "shadow")
            || ContainsToken(commandText, "/etc/shadow")
            || ContainsToken(commandText, "password");
    }

    private static bool IsPrivateKeyDisplayCommand(string commandName, string commandText)
    {
        return ContainsToken(commandName, "private_key")
            || ContainsToken(commandName, "privatekey")
            || ContainsToken(commandText, "id_rsa")
            || ContainsToken(commandText, "id_ed25519")
            || ContainsToken(commandText, "BEGIN OPENSSH PRIVATE KEY")
            || ContainsToken(commandText, "BEGIN RSA PRIVATE KEY");
    }

    private static bool ContainsToken(string value, string token)
    {
        return value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
