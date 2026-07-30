using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KelpieClientCommand;
using KelpieServerCommand;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;
using Microsoft.Extensions.Configuration;
using Renci.SshNet.Common;

ConfigureConsoleEncoding();

if (!KelpieRuntimePathOverrideParser.TryParse(args, out var commandArgs, out var runtimePathOverrides, out var runtimePathError))
{
    Console.Error.WriteLine(runtimePathError);
    Environment.ExitCode = 1;
    return;
}

KelpieRuntimePaths.SetOverrides(runtimePathOverrides);
args = commandArgs;

KpLogSetup.Configure(
    AppContext.BaseDirectory,
    "kelpie.log",
    KelpieRuntimePaths.KelpieConfigFileName,
    "kelpie");
KpLog.Info("Kelpie CLI starting.");

try
{
var command = args.Length > 0 ? args[0] : string.Empty;
if (!IsCheckCommand(args))
{
    WarnLegacyEditorConfigIfNeeded();
}

if (IsHelpCommand(command))
{
    ShowUsage();
    return;
}

if (IsVersionCommand(command))
{
    ShowVersion();
    return;
}

if (string.Equals(command, "init", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI init requested.");
    InitializeKelpieHome(args);
    return;
}

if (string.Equals(command, "config", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI config requested.");
    if (!TryExtractPagerOption(args, out var configArgs, out var configPagerMode))
    {
        Console.Error.WriteLine("Specify only one of --pager or --no-pager.");
        Environment.ExitCode = 1;
        return;
    }

    RunWithPager(configPagerMode, () => RunConfigCheck(configArgs));
    return;
}

if (string.Equals(command, "login", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI login requested.");
    var loginOption = args.Length > 1 ? args[1] : string.Empty;
    var openInConsole = string.Equals(loginOption, "--console", StringComparison.OrdinalIgnoreCase);
    var forceConsoleSession = string.Equals(loginOption, "--console-session", StringComparison.OrdinalIgnoreCase);

    if (string.Equals(loginOption, "--window", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("`kelpie login --window` was renamed to `kelpie login --console`.");
        Environment.ExitCode = 1;
        return;
    }

    if (args.Length > 2
        || (args.Length > 1 && !openInConsole && !forceConsoleSession))
    {
        if (loginOption.StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Unknown `kelpie login` option: {loginOption}");
            Environment.ExitCode = 1;
            return;
        }

        Console.Error.WriteLine("`kelpie login` does not accept a profile argument.");
        Console.Error.WriteLine($"Use `kelpie open {args[1]}` then `kelpie login`.");
        Environment.ExitCode = 1;
        return;
    }

    var openProfileName = LoadOpenProfileName();
    if (string.IsNullOrWhiteSpace(openProfileName))
    {
        Console.Error.WriteLine("No profile is open.");
        Console.Error.WriteLine("Use `kelpie open <profile>` first.");
        Environment.ExitCode = 1;
        return;
    }

    if (openInConsole)
    {
        StartLoginConsole(openProfileName);
        return;
    }

    var catalog = LoadProfileCatalog();
    if (!TryResolveProfile(catalog, openProfileName, out var profile))
    {
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        await RunInteractiveLoginAsync(profile);
    }
    catch (Exception ex) when (ex is InvalidOperationException or SshException)
    {
        KpLog.Warn(ex.Message);
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }

    return;
}

if (string.Equals(command, "logout", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI logout requested.");
    Console.Error.WriteLine("No interactive SSH session is active.");
    Environment.ExitCode = 1;
    return;
}

if (string.Equals(command, "profiles", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI profiles requested.");
    ShowProfiles(LoadProfileCatalog());
    return;
}

if (string.Equals(command, "open", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"Kelpie CLI open requested. profile={profileName}");
    OpenProfile(LoadProfileCatalog(), profileName);
    return;
}

if (string.Equals(command, "sessions", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI sessions requested.");
    await KelpieServerCommandRunner.SessionsAsync(LoadCommandOptions());
    return;
}

if (string.Equals(command, "kill", StringComparison.OrdinalIgnoreCase))
{
    var handle = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"Kelpie CLI kill requested. handle={handle}");
    await KelpieServerCommandRunner.KillAsync(LoadCommandOptions(), handle);
    return;
}

if (string.Equals(command, "profile", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI profile requested.");
    var subcommand = args.Length > 1 ? args[1] : string.Empty;
    if (IsHelpCommand(subcommand))
    {
        WriteProfileUsage(Console.Out);
        return;
    }

    if (string.Equals(subcommand, "create", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryParseProfileCreateOptions(args, out var createArgs, out var createOptions))
        {
            WriteProfileCreateUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        var createProfileName = createArgs.Length > 2 ? createArgs[2] : string.Empty;
        if (IsHelpCommand(createProfileName))
        {
            WriteProfileCreateUsage(Console.Out);
            return;
        }

        if (createArgs.Length != 3)
        {
            WriteProfileCreateUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        CreateProfile(createProfileName, createOptions);
        return;
    }

    if (string.Equals(subcommand, "edit", StringComparison.OrdinalIgnoreCase))
    {
        RunProfileEdit(args);
        return;
    }

    if (string.Equals(subcommand, "delete", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryExtractNoBackupOption(args, out var deleteArgs, out var deleteNoBackup))
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        if (!TryExtractDryRunOption(deleteArgs, out deleteArgs, out var deleteDryRun))
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        if (deleteArgs.Length != 3)
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        DeleteProfile(deleteArgs[2], deleteNoBackup, deleteDryRun);
        return;
    }

    if (string.Equals(subcommand, "clean", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryExtractDryRunOption(args, out var cleanArgs, out var cleanDryRun))
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        if (cleanArgs.Length != 3)
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        CleanProfile(cleanArgs[2], cleanDryRun);
        return;
    }

    if (string.Equals(subcommand, "commit", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryExtractDryRunOption(args, out var commitArgs, out var commitDryRun))
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        if (commitArgs.Length != 3)
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        CommitProfile(commitArgs[2], commitDryRun);
        return;
    }

    if (string.Equals(subcommand, "rollback", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryExtractDryRunOption(args, out var rollbackArgs, out var rollbackDryRun))
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        if (rollbackArgs.Length != 3)
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        RollbackProfile(rollbackArgs[2], rollbackDryRun);
        return;
    }

    if (string.Equals(subcommand, "trust-host-key", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryExtractNoBackupOption(args, out var trustArgs, out var trustNoBackup))
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        if (!TryExtractDryRunOption(trustArgs, out trustArgs, out var trustDryRun))
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        if (trustArgs.Length != 3)
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        TrustHostKey(trustArgs[2], trustNoBackup, trustDryRun);
        return;
    }

    if (string.Equals(subcommand, "check", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryExtractPagerOption(args, out var checkArgs, out var checkPagerMode))
        {
            Console.Error.WriteLine("Specify only one of --pager or --no-pager.");
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        if (checkArgs.Length != 3)
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        RunWithPager(checkPagerMode, () => CheckProfile(checkArgs[2]));
        return;
    }

    if (!string.Equals(subcommand, "show", StringComparison.OrdinalIgnoreCase))
    {
        WriteProfileUsage(Console.Error);
        Environment.ExitCode = 1;
        return;
    }

    if (!TryExtractPagerOption(args, out var showArgs, out var showPagerMode))
    {
        Console.Error.WriteLine("Specify only one of --pager or --no-pager.");
        WriteProfileUsage(Console.Error);
        Environment.ExitCode = 1;
        return;
    }

    if (showArgs.Length != 3)
    {
        WriteProfileUsage(Console.Error);
        Environment.ExitCode = 1;
        return;
    }

    var profileName = showArgs[2];
    if (ContainsWildcard(profileName))
    {
        RunWithPager(showPagerMode, () => ShowProfilesByPattern(LoadProfileCatalog(), profileName));
        return;
    }

    RunWithPager(showPagerMode, () => ShowProfile(LoadProfileCatalog(), profileName));
    return;
}

if (string.Equals(command, "status", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"Kelpie CLI status requested. profile={profileName}");
    await ShowStatusAsync(LoadProfileCatalog(), LoadCommandOptions(), profileName);
    return;
}

if (string.Equals(command, "diag", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"Kelpie CLI diag requested. profile={profileName}");
    await RunDiagnosticsAsync(LoadProfileCatalog(), profileName);
    return;
}

if (string.Equals(command, "inventory", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    KpLog.Info($"Kelpie CLI inventory requested. profile={profileName}");
    await RunInventoryAsync(LoadProfileCatalog(), profileName);
    return;
}

if (string.Equals(command, "logs", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    var service = args.Length > 2 ? args[2] : string.Empty;
    var lines = args.Length > 3 ? args[3] : "100";
    KpLog.Info($"Kelpie CLI logs requested. profile={profileName}, service={service}, lines={lines}");
    await RunLogsAsync(LoadProfileCatalog(), profileName, service, lines);
    return;
}

if (string.Equals(command, "pkg", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI pkg requested.");
    await RunPackageAsync(LoadProfileCatalog(), args);
    return;
}

if (string.Equals(command, "env", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI env requested.");
    await RunEnvironmentAsync(LoadProfileCatalog(), args);
    return;
}

ShowUsage(command);
Environment.ExitCode = string.IsNullOrWhiteSpace(command) ? 0 : 1;
}
catch (Exception ex) when (ex is FileNotFoundException
    or DirectoryNotFoundException
    or UnauthorizedAccessException
    or IOException
    or InvalidOperationException
    or SshException)
{
    KpLog.Warn(ex.Message);
    Console.Error.WriteLine(SanitizeReason(ex.Message));
    Environment.ExitCode = 1;
}

static void ConfigureConsoleEncoding()
{
    try
    {
        Console.OutputEncoding = Encoding.UTF8;
    }
    catch (Exception)
    {
        // Some redirected or restricted consoles reject encoding changes.
    }
}

static IConfigurationRoot LoadConfiguration()
{
    return new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile(
            KelpieRuntimePaths.GetConfigFilePath(AppContext.BaseDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName),
            optional: true,
            reloadOnChange: false)
        .AddJsonFile(
            KelpieRuntimePaths.GetConfigFilePath(AppContext.BaseDirectory, KelpieRuntimePaths.KelpieConfigFileName),
            optional: true,
            reloadOnChange: false)
        .Build();
}

static KelpieMcpServerOptions LoadCommandOptions()
{
    return KelpieMcpServerOptions.FromConfiguration(LoadConfiguration());
}

static SshConnectionProfileCatalog LoadProfileCatalog()
{
    var profilesDirectory = KelpieRuntimePaths.GetProfilesDirectory(AppContext.BaseDirectory);
    var loadResult = SshConnectionProfileFileLoader.LoadDirectoryWithErrors(profilesDirectory);
    WriteProfileLoadWarnings(loadResult.Errors);

    return new SshConnectionProfileCatalog(loadResult.Profiles);
}

static void WriteProfileLoadWarnings(IReadOnlyCollection<SshConnectionProfileLoadError> errors)
{
    foreach (var error in errors)
    {
        var message = $"SSH profile skipped. profile={error.ProfileName}, reason={error.Reason}, file={error.FilePath}, message={error.Message}";
        KpLog.Warn(message);
        Console.Error.WriteLine(message);
    }
}

static void RunConfigCheck(string[] args)
{
    if (args.Length != 2
        || (!string.Equals(args[1], "--check", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(args[1], "check", StringComparison.OrdinalIgnoreCase)))
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  kelpie config --check");
        Environment.ExitCode = 1;
        return;
    }

    var result = new CheckResultWriter();
    var configPath = KelpieRuntimePaths.GetConfigFilePath(AppContext.BaseDirectory, KelpieRuntimePaths.KelpieConfigFileName);
    var mcpConfigPath = KelpieRuntimePaths.GetConfigFilePath(AppContext.BaseDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName);

    var kelpieConfigDocument = CheckJsonFile(result, "Kelpie config file", "Kelpie config JSON", configPath);
    if (kelpieConfigDocument is not null)
    {
        using (kelpieConfigDocument)
        {
            var root = kelpieConfigDocument.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                result.Write("Editor", false, "configuration root must be an object");
            }
            else if (root.TryGetProperty("editor", out _))
            {
                result.Write("Editor", false, "legacy key `editor` is used; rename it to `Editor`");
            }
            else
            {
                result.Write("Editor", true);
            }

            CheckLogRotation(result, root);
        }
    }

    var mcpConfigDocument = CheckJsonFile(result, "MCP config file", "MCP config JSON", mcpConfigPath);
    if (mcpConfigDocument is not null)
    {
        using (mcpConfigDocument)
        {
            var root = mcpConfigDocument.RootElement;
            CheckMcpConfig(result, root);
            CheckLogRotation(result, root);
        }
    }

    result.WriteGroup("Directories:", new[]
    {
        CheckDirectory("config", KelpieRuntimePaths.GetConfigDirectory(AppContext.BaseDirectory)),
        CheckDirectory("profiles", KelpieRuntimePaths.GetProfilesDirectory(AppContext.BaseDirectory)),
        CheckDirectory("logs", KelpieRuntimePaths.GetLogDirectory(AppContext.BaseDirectory)),
        CheckDirectory("bin", KelpieRuntimePaths.GetBinDirectory(AppContext.BaseDirectory)),
        CheckDirectory("keys", KelpieRuntimePaths.GetKeysDirectory(AppContext.BaseDirectory)),
        CheckDirectory("dat", KelpieRuntimePaths.GetDataDirectory(AppContext.BaseDirectory)),
    });

    result.WriteSummary();
    Environment.ExitCode = result.HasErrors ? 1 : 0;
}

static JsonDocument? CheckJsonFile(CheckResultWriter result, string fileItemName, string jsonItemName, string path)
{
    if (!File.Exists(path))
    {
        result.Write(fileItemName, false, $"file was not found: {path}");
        result.Write(jsonItemName, false, "file was not found");
        return null;
    }

    result.Write(fileItemName, true);

    try
    {
        var document = JsonDocument.Parse(File.ReadAllText(path));
        result.Write(jsonItemName, true);
        return document;
    }
    catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
    {
        result.Write(jsonItemName, false, SanitizeReason(ex.Message));
        return null;
    }
}

static void CheckMcpConfig(CheckResultWriter result, JsonElement root)
{
    if (!TryGetJsonProperty(root, "Server", out var server)
        || server.ValueKind != JsonValueKind.Object)
    {
        result.Write("Server", false, "Server section is not configured");
        result.Write("Server.ControlPipeName", false, "Server section is not configured");
        result.Write("Server.Port", false, "Server section is not configured");
        return;
    }

    result.Write("Server", true);

    result.Write(
        "Server.ControlPipeName",
        TryGetStringProperty(server, "ControlPipeName", out var controlPipeName) && !string.IsNullOrWhiteSpace(controlPipeName),
        "Server:ControlPipeName is not configured");

    if (!TryGetJsonProperty(server, "Port", out var portElement))
    {
        result.Write("Server.Port", true);
        return;
    }

    var portOk = portElement.ValueKind == JsonValueKind.Number
        ? portElement.TryGetInt32(out var port) && port is > 0 and <= 65535
        : portElement.ValueKind == JsonValueKind.String
            && int.TryParse(portElement.GetString(), out port)
            && port is > 0 and <= 65535;
    result.Write("Server.Port", portOk, "port must be between 1 and 65535");
}

static void CheckLogRotation(CheckResultWriter result, JsonElement root)
{
    if (!TryGetJsonProperty(root, "LogRotation", out var rotation))
    {
        result.Write("LogRotation", true);
        return;
    }

    if (rotation.ValueKind != JsonValueKind.Object)
    {
        result.Write("LogRotation", false, "LogRotation must be an object");
        return;
    }

    result.Write("LogRotation", true);

    var maxFileBytesOk = !TryGetJsonProperty(rotation, "MaxFileBytes", out var maxFileBytes)
        || (maxFileBytes.ValueKind == JsonValueKind.Number
            && maxFileBytes.TryGetInt64(out var maxFileBytesValue)
            && maxFileBytesValue > 0);
    result.Write("LogRotation.MaxFileBytes", maxFileBytesOk, "value must be a positive integer");

    var retainedFileCountOk = !TryGetJsonProperty(rotation, "RetainedFileCount", out var retainedFileCount)
        || (retainedFileCount.ValueKind == JsonValueKind.Number
            && retainedFileCount.TryGetInt32(out var retainedFileCountValue)
            && retainedFileCountValue >= 0);
    result.Write("LogRotation.RetainedFileCount", retainedFileCountOk, "value must be a non-negative integer");
}

static CheckItem CheckDirectory(string name, string path)
{
    return Directory.Exists(path)
        ? CheckItem.Ok(name)
        : CheckItem.Ng(name, $"directory was not found: {path}");
}

static void CheckProfile(string profileName)
{
    var result = new CheckResultWriter();

    if (string.IsNullOrWhiteSpace(profileName))
    {
        result.Write("Profile name", false, "profile name is required");
        result.WriteSummary();
        Environment.ExitCode = 1;
        return;
    }

    if (ContainsWildcard(profileName))
    {
        result.Write("Profile name", false, "profile check requires a single profile name; wildcards are not supported");
        result.WriteSummary();
        Environment.ExitCode = 1;
        return;
    }

    string profilePath;
    try
    {
        profilePath = GetProfileCheckPath(profileName);
    }
    catch (ArgumentException ex)
    {
        result.Write("Profile name", false, SanitizeReason(ex.Message));
        result.WriteSummary();
        Environment.ExitCode = 1;
        return;
    }

    var document = CheckJsonFile(result, "Profile file", "Profile JSON", profilePath);
    if (document is null)
    {
        result.WriteSummary();
        Environment.ExitCode = 1;
        return;
    }

    document.Dispose();

    SshConnectionProfile profile;
    try
    {
        profile = SshConnectionProfileFileLoader.LoadFile(profilePath);
        profile.Validate();
        result.Write("Profile schema", true);
    }
    catch (Exception ex) when (ex is InvalidOperationException or JsonException or IOException or UnauthorizedAccessException)
    {
        result.Write("Profile schema", false, SanitizeReason(ex.Message));
        result.WriteSummary();
        Environment.ExitCode = 1;
        return;
    }

    CheckLoadedProfile(result, profile);
    result.Write("Pending backup", !File.Exists(GetProfileBackupPath(profilePath)), $"pending backup exists: {GetProfileBackupPath(profilePath)}");

    result.WriteSummary();
    Environment.ExitCode = result.HasErrors ? 1 : 0;
}

static string GetProfileCheckPath(string profileName)
{
    if (profileName.Contains('/', StringComparison.Ordinal) || profileName.Contains('\\', StringComparison.Ordinal))
    {
        throw new ArgumentException("Profile name must not contain path separators.");
    }

    if (profileName.Any(c => Path.GetInvalidFileNameChars().Contains(c)))
    {
        throw new ArgumentException("Profile name contains invalid file-name characters.");
    }

    return Path.GetFullPath(Path.Combine(
        KelpieRuntimePaths.GetProfilesDirectory(AppContext.BaseDirectory),
        profileName + ".json"));
}

static void CheckLoadedProfile(CheckResultWriter result, SshConnectionProfile profile)
{
    result.Write("Host.Address", !string.IsNullOrWhiteSpace(profile.Host), "host address is required");
    result.Write("Host.Port", profile.Port is > 0 and <= 65535, "port must be between 1 and 65535");
    result.Write("User", CheckUserName(profile.UserName, out var userReason), userReason);
    result.Write("Auth.Method", IsSupportedAuthMethod(profile.AuthenticationMethod), "authentication method must be privateKey or password");
    CheckAuthSecret(result, "Auth", profile.AuthenticationMethod, profile.PrivateKeyPath, profile.PasswordSecretName);
    result.Write("Platform.OsFamily", !string.IsNullOrWhiteSpace(profile.OsFamily), "OS family is required");
    result.Write("Platform.PackageManager", !string.IsNullOrWhiteSpace(profile.PackageManager), "package manager is required");
    result.Write(
        "Host.HostKeyFingerprintSha256",
        !string.IsNullOrWhiteSpace(profile.HostKeyFingerprintSha256),
        "host key fingerprint is not pinned; verify the first connection out of band and record Host.HostKeyFingerprintSha256");
    result.Write("Mode", Enum.IsDefined(profile.Mode), "mode is invalid");

    var commandProviders = CommandProcessingProviderCatalog.CreateDefault()
        .Where(provider => provider.Supports(profile))
        .Select(provider => CheckItem.Ok(provider.GetType().Name))
        .ToArray();
    result.WriteGroup(
        "Command providers:",
        commandProviders,
        emptyOk: false,
        emptyReason: "no command provider supports this profile");

    result.WriteGroup("Capabilities:", profile.Capabilities.List().Select(CheckPolicyName));
    result.WriteGroup("Roles:", profile.Roles.Select(CheckNonEmptyValue));
    result.WriteGroup("Allowed roots:", profile.AllowedRootRules.Select(CheckAllowedRoot));
    result.WriteGroup("Special paths:", profile.SpecialPaths.Select(CheckSpecialPath));
    result.WriteGroup("Users:", profile.Users.Select(CheckUser));
}

static void CheckAuthSecret(
    CheckResultWriter result,
    string itemPrefix,
    string authenticationMethod,
    string? privateKeyPath,
    string? passwordSecretName)
{
    if (string.Equals(authenticationMethod, "privateKey", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(privateKeyPath))
        {
            result.Write($"{itemPrefix}.PrivateKeyFile", false, "private key file is required");
            return;
        }

        result.Write(
            $"{itemPrefix}.PrivateKeyFile",
            File.Exists(privateKeyPath),
            $"private key file was not found: {privateKeyPath}");
        return;
    }

    if (string.Equals(authenticationMethod, "password", StringComparison.OrdinalIgnoreCase))
    {
        result.Write(
            $"{itemPrefix}.PasswordSecretName",
            !string.IsNullOrWhiteSpace(passwordSecretName),
            "password secret name is required");
    }
}

static CheckItem CheckPolicyName(string policyName)
{
    return string.IsNullOrWhiteSpace(policyName)
        ? CheckItem.Ng(policyName, "capability name must not be empty")
        : CheckItem.Ok(policyName);
}

static CheckItem CheckNonEmptyValue(string value)
{
    return string.IsNullOrWhiteSpace(value)
        ? CheckItem.Ng("(empty)", "value must not be empty")
        : CheckItem.Ok(value);
}

static CheckItem CheckAllowedRoot(AllowedRootRule rule)
{
    if (string.IsNullOrWhiteSpace(rule.Path))
    {
        return CheckItem.Ng("(empty)", "allowed root must not be empty");
    }

    if (ContainsParentTraversalSegment(rule.Path))
    {
        return CheckItem.Ng(rule.Path, "path traversal is not allowed");
    }

    if (rule.Access == AllowedRootAccess.None)
    {
        return CheckItem.Ng(rule.Path, "allowed root access must not be empty");
    }

    return CheckItem.Ok(rule.Path);
}

static CheckItem CheckSpecialPath(SpecialPathRule rule)
{
    if (string.IsNullOrWhiteSpace(rule.Pattern))
    {
        return CheckItem.Ng("(empty)", "special path pattern must not be empty");
    }

    if (ContainsParentTraversalSegment(rule.Pattern))
    {
        return CheckItem.Ng(rule.Pattern, "path traversal is not allowed");
    }

    return CheckItem.Ok(rule.Pattern);
}

static CheckItem CheckUser(SshConnectionUser user)
{
    return CheckUserName(user.UserName, out var reason)
        ? CheckItem.Ok(user.UserName)
        : CheckItem.Ng(string.IsNullOrWhiteSpace(user.UserName) ? "(empty)" : user.UserName, reason ?? "user name is invalid");
}

static bool CheckUserName(string userName, out string? reason)
{
    if (string.IsNullOrWhiteSpace(userName))
    {
        reason = "user name is required";
        return false;
    }

    if (string.Equals(userName, "root", StringComparison.OrdinalIgnoreCase))
    {
        reason = "root login is not allowed";
        return false;
    }

    reason = null;
    return true;
}

static bool IsSupportedAuthMethod(string authenticationMethod)
{
    return string.Equals(authenticationMethod, "privateKey", StringComparison.OrdinalIgnoreCase)
        || string.Equals(authenticationMethod, "password", StringComparison.OrdinalIgnoreCase);
}

static bool ContainsParentTraversalSegment(string value)
{
    var segments = value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal));
}

static bool TryGetStringProperty(JsonElement element, string propertyName, out string value)
{
    if (TryGetJsonProperty(element, propertyName, out var property)
        && property.ValueKind == JsonValueKind.String)
    {
        value = property.GetString() ?? string.Empty;
        return true;
    }

    value = string.Empty;
    return false;
}

static bool TryGetJsonProperty(JsonElement element, string propertyName, out JsonElement property)
{
    if (element.ValueKind != JsonValueKind.Object)
    {
        property = default;
        return false;
    }

    foreach (var item in element.EnumerateObject())
    {
        if (string.Equals(item.Name, propertyName, StringComparison.OrdinalIgnoreCase))
        {
            property = item.Value;
            return true;
        }
    }

    property = default;
    return false;
}

static string SanitizeReason(string reason)
{
    return string.Join(" ", reason.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

static void WarnLegacyEditorConfigIfNeeded()
{
    try
    {
        var configPath = KelpieRuntimePaths.GetConfigFilePath(AppContext.BaseDirectory, KelpieRuntimePaths.KelpieConfigFileName);
        if (!File.Exists(configPath))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        if (!document.RootElement.TryGetProperty("editor", out _))
        {
            return;
        }

        Console.WriteLine($"Warning: {KelpieRuntimePaths.KelpieConfigFileName} uses legacy key `editor`. Please rename it to `Editor`.");
    }
    catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
    {
        KpLog.Warn($"Failed to inspect Kelpie config for legacy editor key. reason={ex.GetType().Name}");
    }
}

static bool IsHelpCommand(string command)
{
    return string.IsNullOrWhiteSpace(command)
        || string.Equals(command, "help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "--help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "-h", StringComparison.OrdinalIgnoreCase);
}

static bool IsVersionCommand(string command)
{
    return string.Equals(command, "version", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "--version", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "-v", StringComparison.OrdinalIgnoreCase);
}

static bool IsCheckCommand(string[] args)
{
    if (!TryExtractPagerOption(args, out var checkArgs, out _))
    {
        checkArgs = args;
    }

    return args.Length >= 2
        && checkArgs.Length >= 2
        && ((string.Equals(checkArgs[0], "config", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(checkArgs[1], "--check", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(checkArgs[1], "check", StringComparison.OrdinalIgnoreCase)))
            || (string.Equals(checkArgs[0], "profile", StringComparison.OrdinalIgnoreCase)
                && string.Equals(checkArgs[1], "check", StringComparison.OrdinalIgnoreCase)));
}

static void WriteProfileUsage(TextWriter writer)
{
    writer.WriteLine("Usage:");
    writer.WriteLine("  kelpie profile create <profile> [--silent] [--no-backup] [--dry-run] [options]");
    writer.WriteLine("  kelpie profile edit <profile> [--no-backup]");
    writer.WriteLine("  kelpie profile edit <profile> set <dotPath> <value> [--no-backup] [--dry-run]");
    writer.WriteLine("  kelpie profile edit <profile> add-root <path> <access> [--no-backup] [--dry-run]");
    writer.WriteLine("  kelpie profile edit <profile> rm-root <path> [--no-backup] [--dry-run]");
    writer.WriteLine("  kelpie profile edit <profile> add-deny <pattern> [--no-backup] [--dry-run]");
    writer.WriteLine("  kelpie profile edit <profile> rm-deny <pattern> [--no-backup] [--dry-run]");
    writer.WriteLine("  kelpie profile delete <profile-pattern> [--no-backup] [--dry-run]");
    writer.WriteLine("  kelpie profile clean <profile-pattern> [--dry-run]");
    writer.WriteLine("  kelpie profile commit <profile-pattern> [--dry-run]");
    writer.WriteLine("  kelpie profile rollback <profile-pattern> [--dry-run]");
    writer.WriteLine("  kelpie profile trust-host-key <profile> [--no-backup] [--dry-run]");
    writer.WriteLine("  kelpie profile check <profile> [--pager|--no-pager]");
    writer.WriteLine("  kelpie profile show <profile-pattern> [--pager|--no-pager]");
    writer.WriteLine();
    writer.WriteLine("Options:");
    writer.WriteLine("  --pager     Page long terminal output.");
    writer.WriteLine("  --no-pager  Print all terminal output without paging.");
}

static void WriteProfileCreateUsage(TextWriter writer)
{
    writer.WriteLine("Usage:");
    writer.WriteLine("  kelpie profile create <profile> [--silent] [--no-backup] [--dry-run] [options]");
    writer.WriteLine();
    writer.WriteLine("Options:");
    writer.WriteLine("  --silent                         Create the profile without prompts.");
    writer.WriteLine("  --host-address <value>            Override Host.Address.");
    writer.WriteLine("  --port <value>                    Override Host.Port.");
    writer.WriteLine("  --ssh-user <value>                Override DefaultUser.");
    writer.WriteLine("  --auth-method <privateKey|password>");
    writer.WriteLine("  --private-key-file <value>");
    writer.WriteLine("  --password-secret-name <value>");
    writer.WriteLine("  --os-family <value>");
    writer.WriteLine("  --mode <ReadOnly|Safe|Maintenance|Expert>");
    writer.WriteLine("  --read-only-root <value>          Override read-only roots; repeatable.");
    writer.WriteLine("  --read-write-root <value>         Override read-write roots; repeatable.");
    writer.WriteLine("  --allowed-root <key=value[;...]>  Override allowed-root map entries; repeatable.");
    writer.WriteLine("  --deny-pattern <value>            Override deny patterns; repeatable.");
    writer.WriteLine("  --special-path <key=value[;...]>  Override special-path map entries; repeatable.");
    writer.WriteLine("  --dry-run                         Print planned changes without writing files.");
    writer.WriteLine("  --no-backup                       Overwrite without creating a .kelpie backup.");
}

static void ShowUsage(string command = "")
{
    if (!string.IsNullOrWhiteSpace(command))
    {
        Console.Error.WriteLine($"Unknown command: {command}");
    }

    var writer = string.IsNullOrWhiteSpace(command) ? Console.Out : Console.Error;
    writer.WriteLine("Usage:");
    writer.WriteLine("  kelpie init [--silent] [profile]");
    writer.WriteLine("  kelpie open <profile>");
    writer.WriteLine("  kelpie config --check [--pager|--no-pager]");
    writer.WriteLine("  kelpie login");
    writer.WriteLine("  kelpie login --console");
    writer.WriteLine("  kelpie logout");
    writer.WriteLine("  kelpie profiles");
    writer.WriteLine("  kelpie sessions");
    writer.WriteLine("  kelpie kill <handle>");
    writer.WriteLine("  kelpie profile create <profile> [--no-backup]");
    writer.WriteLine("  kelpie profile edit <profile> [--no-backup]");
    writer.WriteLine("  kelpie profile edit <profile> set <dotPath> <value> [--no-backup]");
    writer.WriteLine("  kelpie profile edit <profile> add-root <path> <access> [--no-backup]");
    writer.WriteLine("  kelpie profile edit <profile> rm-root <path> [--no-backup]");
    writer.WriteLine("  kelpie profile edit <profile> add-deny <pattern> [--no-backup]");
    writer.WriteLine("  kelpie profile edit <profile> rm-deny <pattern> [--no-backup]");
    writer.WriteLine("  kelpie profile delete <profile-pattern> [--no-backup]");
    writer.WriteLine("  kelpie profile clean <profile-pattern>");
    writer.WriteLine("  kelpie profile commit <profile-pattern>");
    writer.WriteLine("  kelpie profile rollback <profile-pattern>");
    writer.WriteLine("  kelpie profile trust-host-key <profile> [--no-backup]");
    writer.WriteLine("  kelpie profile check <profile> [--pager|--no-pager]");
    writer.WriteLine("  kelpie profile show <profile-pattern> [--pager|--no-pager]");
    writer.WriteLine("  kelpie status <profile>");
    writer.WriteLine("  kelpie diag <profile>");
    writer.WriteLine("  kelpie inventory <profile>");
    writer.WriteLine("  kelpie logs <profile> <service> [lines]");
    writer.WriteLine("  kelpie pkg check-updates <profile>");
    writer.WriteLine("  kelpie pkg info <profile> <package>");
    writer.WriteLine("  kelpie pkg search <profile> <query> [limit]");
    writer.WriteLine("  kelpie pkg list-installed <profile> <filter> [limit]");
    writer.WriteLine("  kelpie pkg simulate-install <profile> <package>");
    writer.WriteLine("  kelpie pkg simulate-remove <profile> <package>");
    writer.WriteLine("  kelpie pkg install <profile> <package> [--confirm <token>]");
    writer.WriteLine("  kelpie pkg remove <profile> <package> [--confirm <token>]");
    writer.WriteLine("  kelpie env keys <profile>");
    writer.WriteLine("  kelpie env peek <profile> <key>");
    writer.WriteLine("  kelpie env list <profile>");
    writer.WriteLine("  kelpie env persist <profile> <key> <value>");
    writer.WriteLine("  kelpie env remove <profile> <key>");
    writer.WriteLine("  kelpie version");
    writer.WriteLine("  kelpie help");
    writer.WriteLine();
    writer.WriteLine("Options:");
    writer.WriteLine("  --version, -v  Show version information.");
    writer.WriteLine("  --help, -h     Show command help.");
    writer.WriteLine("  --config-dir <dir>    Override the config directory.");
    writer.WriteLine("  --profiles-dir <dir>  Override the SSH profile directory.");
    writer.WriteLine("  --logs-dir <dir>      Override the log directory.");
    writer.WriteLine("  --bin-dir <dir>       Override the binary directory.");
    writer.WriteLine("  --keys-dir <dir>      Override the key directory.");
    writer.WriteLine("  --dat-dir <dir>       Override the runtime data directory.");
    writer.WriteLine("  --pager               Page long terminal output for supported commands.");
    writer.WriteLine("  --no-pager            Print all terminal output for supported commands.");
}

static void InitializeKelpieHome(string[] args)
{
    var silent = false;
    var profileArgs = new List<string>();
    foreach (var arg in args.Skip(1))
    {
        if (string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase))
        {
            silent = true;
            continue;
        }

        if (arg.StartsWith("-", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Unknown option: {arg}");
            WriteInitUsage();
            Environment.ExitCode = 1;
            return;
        }

        profileArgs.Add(arg);
    }

    if (profileArgs.Count > 1)
    {
        WriteInitUsage();
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        var profileName = profileArgs.Count > 0 ? profileArgs[0] : KelpieHomeInitializer.DefaultProfileName;
        var homeDirectory = KelpieRuntimePaths.GetHomeDirectory(AppContext.BaseDirectory);
        var mcpConfigPath = KelpieRuntimePaths.GetConfigFilePath(AppContext.BaseDirectory, KelpieRuntimePaths.KelpieMcpConfigFileName);
        var profilePath = KelpieHomeInitializer.GetProfilePath(homeDirectory, profileName);
        var mcpConfigOptions = !silent && !File.Exists(mcpConfigPath)
            ? ReadMcpConfigTemplateOptions(KelpieRuntimePaths.GetLogDirectory(AppContext.BaseDirectory))
            : null;
        var templateOptions = !silent && !File.Exists(profilePath)
            ? ReadProfileTemplateOptions(profileName)
            : null;
        var result = KelpieHomeInitializer.Initialize(
            homeDirectory,
            AppContext.BaseDirectory,
            profileName,
            templateOptions,
            mcpConfigOptions);
        Console.WriteLine($"Kelpie home: {result.HomeDirectory}");
        Console.WriteLine($"Profile: {result.ProfileName}");
        WriteInitializedPaths("Created directories", result.CreatedDirectories);
        WriteInitializedPaths("Created files", result.CreatedFiles);
        WriteInitializedPaths("Existing files", result.ExistingFiles);
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
    catch (IOException ex)
    {
        KpLog.Err($"Kelpie init failed. exceptionType={ex.GetType().FullName ?? "UnknownException"}");
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
    catch (UnauthorizedAccessException ex)
    {
        KpLog.Err($"Kelpie init failed. exceptionType={ex.GetType().FullName ?? "UnknownException"}");
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static void WriteInitUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  kelpie init [--silent] [profile]");
}

static void WriteInitializedPaths(string title, IReadOnlyCollection<string> paths)
{
    if (paths.Count == 0)
    {
        return;
    }

    Console.WriteLine($"{title}:");
    foreach (var path in paths)
    {
        Console.WriteLine($"  {path}");
    }
}

static bool TryExtractNoBackupOption(string[] args, out string[] remainingArgs, out bool noBackup)
{
    noBackup = false;
    var remaining = new List<string>(args.Length);
    foreach (var arg in args)
    {
        if (string.Equals(arg, "--no-backup", StringComparison.OrdinalIgnoreCase))
        {
            if (noBackup)
            {
                Console.Error.WriteLine("--no-backup was specified more than once.");
                remainingArgs = args;
                return false;
            }

            noBackup = true;
            continue;
        }

        remaining.Add(arg);
    }

    remainingArgs = remaining.ToArray();
    return true;
}

static bool TryExtractDryRunOption(string[] args, out string[] remainingArgs, out bool dryRun)
{
    dryRun = false;
    var remaining = new List<string>(args.Length);
    foreach (var arg in args)
    {
        if (string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase))
        {
            if (dryRun)
            {
                Console.Error.WriteLine("--dry-run was specified more than once.");
                remainingArgs = args;
                return false;
            }

            dryRun = true;
            continue;
        }

        remaining.Add(arg);
    }

    remainingArgs = remaining.ToArray();
    return true;
}

static bool TryExtractPagerOption(string[] args, out string[] remainingArgs, out PagerMode pagerMode)
{
    var pagerSpecified = false;
    var noPagerSpecified = false;
    var remaining = new List<string>(args.Length);

    foreach (var arg in args)
    {
        if (string.Equals(arg, "--pager", StringComparison.OrdinalIgnoreCase))
        {
            pagerSpecified = true;
            continue;
        }

        if (string.Equals(arg, "--no-pager", StringComparison.OrdinalIgnoreCase))
        {
            noPagerSpecified = true;
            continue;
        }

        remaining.Add(arg);
    }

    if (pagerSpecified && noPagerSpecified)
    {
        remainingArgs = args;
        pagerMode = PagerMode.Auto;
        return false;
    }

    remainingArgs = remaining.ToArray();
    pagerMode = pagerSpecified
        ? PagerMode.Force
        : noPagerSpecified
            ? PagerMode.Disabled
            : PagerMode.Auto;
    return true;
}

static void RunWithPager(PagerMode pagerMode, Action action)
{
    var originalOut = Console.Out;
    using var buffer = new StringWriter();

    try
    {
        Console.SetOut(buffer);
        action();
    }
    finally
    {
        Console.SetOut(originalOut);
    }

    WritePagedOutput(buffer.ToString(), pagerMode, originalOut);
}

static void WritePagedOutput(string output, PagerMode pagerMode, TextWriter writer)
{
    if (!ShouldPageOutput(pagerMode))
    {
        writer.Write(output);
        return;
    }

    var pageSize = GetPagerPageSize();
    var lines = ReadOutputLines(output);
    if (lines.Count <= pageSize)
    {
        writer.Write(output);
        return;
    }

    for (var i = 0; i < lines.Count; i++)
    {
        writer.WriteLine(lines[i]);
        if ((i + 1) % pageSize != 0 || i == lines.Count - 1)
        {
            continue;
        }

        if (!WaitForPagerContinue())
        {
            break;
        }
    }
}

static bool ShouldPageOutput(PagerMode pagerMode)
{
    if (pagerMode == PagerMode.Disabled)
    {
        return false;
    }

    if (Console.IsInputRedirected)
    {
        return false;
    }

    if (pagerMode == PagerMode.Force)
    {
        return true;
    }

    return !Console.IsOutputRedirected;
}

static int GetPagerPageSize()
{
    try
    {
        return Math.Max(1, Console.WindowHeight - 1);
    }
    catch (IOException)
    {
        return 23;
    }
    catch (PlatformNotSupportedException)
    {
        return 23;
    }
}

static IReadOnlyList<string> ReadOutputLines(string output)
{
    var lines = new List<string>();
    using var reader = new StringReader(output);
    while (reader.ReadLine() is { } line)
    {
        lines.Add(line);
    }

    return lines;
}

static bool WaitForPagerContinue()
{
    const string prompt = "-- more -- (Return to continue, q to quit)";

    Console.Error.Write(prompt);
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        ClearPagerPrompt(prompt.Length);

        if (key.Key == ConsoleKey.Q)
        {
            return false;
        }

        if (key.Key == ConsoleKey.Enter)
        {
            return true;
        }

        Console.Error.Write(prompt);
    }
}

static void ClearPagerPrompt(int promptLength)
{
    Console.Error.Write("\r");
    Console.Error.Write(new string(' ', promptLength));
    Console.Error.Write("\r");
}

static bool TryParseProfileCreateOptions(
    string[] args,
    out string[] remainingArgs,
    out ProfileCreateCommandOptions options)
{
    var remaining = new List<string>(args.Length);
    var silent = false;
    var noBackup = false;
    var dryRun = false;
    string? hostAddress = null;
    int? port = null;
    string? defaultUser = null;
    string? authMethod = null;
    string? privateKeyFile = null;
    string? passwordSecretName = null;
    string? osFamily = null;
    string? mode = null;
    List<string>? readOnlyRoots = null;
    List<string>? readWriteRoots = null;
    List<string>? denyPatterns = null;
    Dictionary<string, string>? allowedRootEntries = null;
    Dictionary<string, string>? specialPathEntries = null;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (string.Equals(arg, "--silent", StringComparison.OrdinalIgnoreCase))
        {
            if (silent)
            {
                Console.Error.WriteLine("--silent was specified more than once.");
                remainingArgs = args;
                options = ProfileCreateCommandOptions.Default;
                return false;
            }

            silent = true;
            continue;
        }

        if (string.Equals(arg, "--no-backup", StringComparison.OrdinalIgnoreCase))
        {
            if (noBackup)
            {
                Console.Error.WriteLine("--no-backup was specified more than once.");
                remainingArgs = args;
                options = ProfileCreateCommandOptions.Default;
                return false;
            }

            noBackup = true;
            continue;
        }

        if (string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase))
        {
            if (dryRun)
            {
                Console.Error.WriteLine("--dry-run was specified more than once.");
                remainingArgs = args;
                options = ProfileCreateCommandOptions.Default;
                return false;
            }

            dryRun = true;
            continue;
        }

        if (!arg.StartsWith("--", StringComparison.Ordinal))
        {
            remaining.Add(arg);
            continue;
        }

        if (!TryReadOptionValue(args, ref i, out var optionName, out var optionValue))
        {
            remainingArgs = args;
            options = ProfileCreateCommandOptions.Default;
            return false;
        }

        switch (optionName)
        {
            case "--host-address":
                if (!TrySetOnce(optionName, optionValue, ref hostAddress))
                {
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                break;

            case "--port":
                if (!int.TryParse(optionValue, out var parsedPort) || parsedPort is < 1 or > 65535)
                {
                    Console.Error.WriteLine("--port must be a number between 1 and 65535.");
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                if (port is not null)
                {
                    Console.Error.WriteLine("--port was specified more than once.");
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                port = parsedPort;
                break;

            case "--ssh-user":
                if (!TrySetOnce(optionName, optionValue, ref defaultUser))
                {
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                break;

            case "--auth-method":
                if (!IsAllowedChoice(optionValue, ["privateKey", "password"]))
                {
                    Console.Error.WriteLine("--auth-method must be one of: privateKey, password.");
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                authMethod = NormalizeChoice(optionValue, ["privateKey", "password"]);
                break;

            case "--private-key-file":
                if (!TrySetOnce(optionName, optionValue, ref privateKeyFile))
                {
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                break;

            case "--password-secret-name":
                if (!TrySetOnce(optionName, optionValue, ref passwordSecretName))
                {
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                break;

            case "--os-family":
                if (!TrySetOnce(optionName, optionValue, ref osFamily))
                {
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                break;

            case "--mode":
                if (!IsAllowedChoice(optionValue, ["ReadOnly", "Safe", "Maintenance", "Expert"]))
                {
                    Console.Error.WriteLine("--mode must be one of: ReadOnly, Safe, Maintenance, Expert.");
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                mode = NormalizeChoice(optionValue, ["ReadOnly", "Safe", "Maintenance", "Expert"]);
                break;

            case "--read-only-root":
                AddListOverride(optionValue, ref readOnlyRoots);
                break;

            case "--read-write-root":
                AddListOverride(optionValue, ref readWriteRoots);
                break;

            case "--allowed-root":
                if (!TryAddMapOverrides(optionName, optionValue, ref allowedRootEntries, NormalizeAllowedRootValue))
                {
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                break;

            case "--deny-pattern":
                AddListOverride(optionValue, ref denyPatterns);
                break;

            case "--special-path":
                if (!TryAddMapOverrides(optionName, optionValue, ref specialPathEntries, NormalizeSpecialPathValue))
                {
                    remainingArgs = args;
                    options = ProfileCreateCommandOptions.Default;
                    return false;
                }

                break;

            default:
                Console.Error.WriteLine($"Unknown option: {optionName}");
                remainingArgs = args;
                options = ProfileCreateCommandOptions.Default;
                return false;
        }
    }

    remainingArgs = remaining.ToArray();
    options = new ProfileCreateCommandOptions(
        Silent: silent,
        NoBackup: noBackup,
        DryRun: dryRun,
        HostAddress: hostAddress,
        Port: port,
        DefaultUser: defaultUser,
        AuthMethod: authMethod,
        PrivateKeyFile: privateKeyFile,
        PasswordSecretName: passwordSecretName,
        OsFamily: osFamily,
        Mode: mode,
        ReadOnlyRoots: readOnlyRoots,
        ReadWriteRoots: readWriteRoots,
        DenyPatterns: denyPatterns,
        AllowedRootEntries: allowedRootEntries,
        SpecialPathEntries: specialPathEntries);
    return true;
}

static bool TryReadOptionValue(
    string[] args,
    ref int index,
    out string optionName,
    out string optionValue)
{
    var arg = args[index];
    var separatorIndex = arg.IndexOf('=', StringComparison.Ordinal);
    if (separatorIndex >= 0)
    {
        optionName = NormalizeCreateOptionName(arg[..separatorIndex]);
        optionValue = arg[(separatorIndex + 1)..].Trim();
    }
    else
    {
        optionName = NormalizeCreateOptionName(arg);
        if (index + 1 >= args.Length)
        {
            Console.Error.WriteLine($"{optionName} requires a value.");
            optionValue = string.Empty;
            return false;
        }

        index++;
        optionValue = args[index].Trim();
    }

    if (string.IsNullOrWhiteSpace(optionValue))
    {
        Console.Error.WriteLine($"{optionName} requires a non-empty value.");
        return false;
    }

    return true;
}

static string NormalizeCreateOptionName(string value)
{
    var normalized = value.Trim();
    while (normalized.EndsWith(":", StringComparison.Ordinal))
    {
        normalized = normalized[..^1];
    }

    return normalized;
}

static bool TrySetOnce(string optionName, string optionValue, ref string? target)
{
    if (target is not null)
    {
        Console.Error.WriteLine($"{optionName} was specified more than once.");
        return false;
    }

    target = optionValue;
    return true;
}

static bool IsAllowedChoice(string value, IReadOnlyCollection<string> allowedValues)
{
    return allowedValues.Any(allowedValue => string.Equals(allowedValue, value, StringComparison.OrdinalIgnoreCase));
}

static string NormalizeChoice(string value, IReadOnlyCollection<string> allowedValues)
{
    return allowedValues.First(allowedValue => string.Equals(allowedValue, value, StringComparison.OrdinalIgnoreCase));
}

static void AddListOverride(string value, ref List<string>? values)
{
    values ??= [];
    if (string.Equals(value, "-", StringComparison.Ordinal))
    {
        values.Clear();
        return;
    }

    values.Add(value);
}

static bool TryAddMapOverrides(
    string optionName,
    string optionValue,
    ref Dictionary<string, string>? values,
    Func<string, string> normalizeValue)
{
    values ??= new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var rawEntry in optionValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        var separatorIndex = rawEntry.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex == rawEntry.Length - 1)
        {
            Console.Error.WriteLine($"{optionName} entries must use <key>=<value>.");
            return false;
        }

        var key = rawEntry[..separatorIndex].Trim();
        var value = rawEntry[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            Console.Error.WriteLine($"{optionName} entries must use non-empty <key>=<value> pairs.");
            return false;
        }

        values[key] = normalizeValue(value);
    }

    return true;
}

static string NormalizeAllowedRootValue(string value)
{
    var normalized = value.Trim();
    if (string.Equals(normalized, "ReadOnly", StringComparison.OrdinalIgnoreCase))
    {
        return "$ReadOnly";
    }

    if (string.Equals(normalized, "ReadWrite", StringComparison.OrdinalIgnoreCase))
    {
        return "$ReadWrite";
    }

    return normalized;
}

static string NormalizeSpecialPathValue(string value)
{
    if (string.Equals(value, "deny", StringComparison.OrdinalIgnoreCase))
    {
        return "Deny";
    }

    if (string.Equals(value, "confirm", StringComparison.OrdinalIgnoreCase))
    {
        return "Confirm";
    }

    if (string.Equals(value, "allow", StringComparison.OrdinalIgnoreCase))
    {
        return "Allow";
    }

    return value.Trim();
}

static KelpieProfileTemplateOptions CreateSilentProfileTemplateOptions(string profileName, ProfileCreateCommandOptions options)
{
    var defaults = KelpieProfileTemplateOptions.CreateDefault(profileName);
    var readOnlyRoots = options.ReadOnlyRoots
        ?? (options.AllowedRootEntries is null ? defaults.ReadOnlyRoots : []);
    var readWriteRoots = options.ReadWriteRoots
        ?? (options.AllowedRootEntries is null ? defaults.ReadWriteRoots : []);
    var denyPatterns = options.DenyPatterns
        ?? (options.SpecialPathEntries is null ? defaults.DenyPatterns : []);

    return defaults with
    {
        HostAddress = options.HostAddress ?? defaults.HostAddress,
        Port = options.Port ?? defaults.Port,
        AuthMethod = options.AuthMethod ?? defaults.AuthMethod,
        PrivateKeyFile = options.PrivateKeyFile ?? defaults.PrivateKeyFile,
        PasswordSecretName = options.PasswordSecretName ?? defaults.PasswordSecretName,
        DefaultUser = options.DefaultUser ?? defaults.DefaultUser,
        Mode = options.Mode ?? defaults.Mode,
        OsFamily = options.OsFamily ?? defaults.OsFamily,
        ReadOnlyRoots = readOnlyRoots,
        ReadWriteRoots = readWriteRoots,
        DenyPatterns = denyPatterns,
        AllowedRootEntries = options.AllowedRootEntries ?? defaults.AllowedRootEntries,
        SpecialPathEntries = options.SpecialPathEntries ?? defaults.SpecialPathEntries,
    };
}

static void CreateProfile(string profileName, ProfileCreateCommandOptions options)
{
    if (string.IsNullOrWhiteSpace(profileName))
    {
        WriteProfileCreateUsage(Console.Error);
        Environment.ExitCode = 1;
        return;
    }

    if (ContainsWildcard(profileName))
    {
        Console.Error.WriteLine("Profile create requires a single profile name. Wildcards are not supported.");
        Environment.ExitCode = 1;
        return;
    }

    if (!options.Silent && !options.DryRun && options.HasTemplateOverrides)
    {
        Console.Error.WriteLine("Profile create template options require --silent.");
        Environment.ExitCode = 1;
        return;
    }

    try
    {
        var homeDirectory = KelpieRuntimePaths.GetHomeDirectory(AppContext.BaseDirectory);
        var profilePath = KelpieHomeInitializer.GetProfilePath(homeDirectory, profileName);
        if (File.Exists(GetProfileBackupPath(profilePath)))
        {
            WritePendingProfileTransactionError(profileName, profilePath);
            Environment.ExitCode = 1;
            return;
        }

        var overwrite = File.Exists(profilePath);
        if (overwrite && !options.DryRun && !ReadYesNoDefaultYes($"Profile already exists: {profileName}. Overwrite? [Y/n]: "))
        {
            Console.WriteLine("Profile create was canceled.");
            return;
        }

        if (!overwrite)
        {
            KelpieHomeInitializer.GetCreatableProfilePath(homeDirectory, profileName);
        }

        var templateOptions = options.Silent || options.DryRun
            ? CreateSilentProfileTemplateOptions(profileName, options)
            : ReadProfileTemplateOptions(profileName);
        if (options.DryRun)
        {
            Console.WriteLine("Dry run: profile create");
            Console.WriteLine(overwrite ? $"Would overwrite profile: {profileName}" : $"Would create profile: {profileName}");
            Console.WriteLine($"Profile file: {profilePath}");
            if (overwrite)
            {
                if (options.NoBackup)
                {
                    Console.WriteLine("Would not create backup because --no-backup is specified.");
                }
                else
                {
                    Console.WriteLine($"Would create backup: {GetProfileBackupPath(profilePath)}");
                }
            }

            Console.WriteLine("Would write:");
            Console.Write(KelpieHomeInitializer.CreateProfileJson(profileName, templateOptions));
            Console.WriteLine("No files were changed.");
            return;
        }

        ProfileTransaction? transaction = null;
        try
        {
            transaction = overwrite && !options.NoBackup ? BeginProfileTransaction(profilePath) : null;
            if (overwrite)
            {
                File.Delete(profilePath);
            }

            profilePath = KelpieHomeInitializer.CreateProfile(homeDirectory, profileName, templateOptions);
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }

        Console.WriteLine($"Created profile: {profileName}");
        Console.WriteLine($"Profile file: {profilePath}");
        if (transaction is not null)
        {
            FinishProfileTransaction(profileName, transaction);
        }
        else if (overwrite && options.NoBackup)
        {
            Console.WriteLine($"Committed profile: {profileName}");
        }
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
    catch (IOException ex)
    {
        KpLog.Err($"Kelpie profile create failed. exceptionType={ex.GetType().FullName ?? "UnknownException"}");
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
    catch (UnauthorizedAccessException ex)
    {
        KpLog.Err($"Kelpie profile create failed. exceptionType={ex.GetType().FullName ?? "UnknownException"}");
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static void CommitProfile(string profileName, bool dryRun)
{
    if (ContainsWildcard(profileName))
    {
        CommitProfilesByPattern(profileName, dryRun);
        return;
    }

    var profilePath = GetExistingOrPendingProfilePath(profileName);
    if (profilePath is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    var backupPath = GetProfileBackupPath(profilePath);
    if (!File.Exists(backupPath))
    {
        Console.Error.WriteLine($"Pending profile backup was not found: {backupPath}");
        Environment.ExitCode = 1;
        return;
    }

    if (dryRun)
    {
        Console.WriteLine("Dry run: profile commit");
        Console.WriteLine($"Would commit profile: {profileName}");
        Console.WriteLine($"Would remove backup: {backupPath}");
        Console.WriteLine("No files were changed.");
        return;
    }

    try
    {
        File.Delete(backupPath);
        Console.WriteLine($"Committed profile: {profileName}");
        Console.WriteLine($"Removed backup: {backupPath}");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static void TrustHostKey(string profileName, bool noBackup, bool dryRun)
{
    if (string.IsNullOrWhiteSpace(profileName))
    {
        Console.Error.WriteLine("Profile name is required.");
        Environment.ExitCode = 1;
        return;
    }

    if (ContainsWildcard(profileName))
    {
        Console.Error.WriteLine("Profile trust-host-key requires a single profile name. Wildcards are not supported.");
        Environment.ExitCode = 1;
        return;
    }

    var profilePath = GetExistingProfilePath(profileName);
    if (profilePath is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    if (File.Exists(GetProfileBackupPath(profilePath)))
    {
        WritePendingProfileTransactionError(profileName, profilePath);
        Environment.ExitCode = 1;
        return;
    }

    var profile = SshConnectionProfileFileLoader.LoadFile(profilePath);
    if (!string.IsNullOrWhiteSpace(profile.HostKeyFingerprintSha256))
    {
        Console.WriteLine($"Host key is already pinned for profile: {profile.Name}");
        Console.WriteLine($"Host key SHA256: {FormatHostKeyFingerprint(profile.HostKeyFingerprintSha256)}");
        return;
    }

    Console.WriteLine($"Reading SSH host key fingerprint for profile: {profile.Name}");
    Console.WriteLine($"Host: {profile.Host}");
    Console.WriteLine($"Port: {profile.Port}");
    var fingerprint = new SshNetHostKeyFingerprintReader().ReadSha256(profile);

    Console.WriteLine("Received SSH host key fingerprint:");
    Console.WriteLine(fingerprint);
    Console.WriteLine("Only trust this key if you verified it through your VPS provider console or another trusted channel.");

    var editService = CreateProfileEditService();
    if (dryRun)
    {
        RunDryRunProfileEdit(
            profile.Name,
            profilePath,
            path => editService.SetHostKeyFingerprint(path, fingerprint),
            noBackup);
        return;
    }

    if (!ReadTrustConfirmation(profile.Name))
    {
        Console.WriteLine("Host key trust was canceled. No files were changed.");
        Environment.ExitCode = 1;
        return;
    }

    RunTransactionalProfileEdit(
        profile.Name,
        profilePath,
        () => editService.SetHostKeyFingerprint(profilePath, fingerprint),
        noBackup);
}

static bool ReadTrustConfirmation(string profileName)
{
    Console.Error.Write($"Type TRUST to record this fingerprint for `{profileName}`: ");
    var value = Console.ReadLine();
    return string.Equals(value?.Trim(), "TRUST", StringComparison.Ordinal);
}

static void CommitProfilesByPattern(string profilePattern, bool dryRun)
{
    var targets = ResolveProfileTargets(profilePattern, ProfileTargetKind.Pending, rejectPendingBackups: false);
    if (targets is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Matched pending profiles: {targets.Count}");
    foreach (var target in targets)
    {
        Console.WriteLine($"  {target.ProfileName}");
    }

    if (dryRun)
    {
        Console.WriteLine("Dry run: profile commit");
        foreach (var target in targets)
        {
            Console.WriteLine($"Would remove backup: {GetProfileBackupPath(target.ProfilePath)}");
        }

        Console.WriteLine("No files were changed.");
        return;
    }

    if (!ReadYesNoDefaultYes($"Commit {targets.Count} profiles matching `{profilePattern.Trim()}`? [Y/n]: "))
    {
        Console.WriteLine("Profile commit was canceled.");
        return;
    }

    try
    {
        foreach (var target in targets)
        {
            File.Delete(GetProfileBackupPath(target.ProfilePath));
        }

        Console.WriteLine($"Committed profiles: {targets.Count}");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static void CleanProfile(string profileName, bool dryRun)
{
    if (ContainsWildcard(profileName))
    {
        CleanProfilesByPattern(profileName, dryRun);
        return;
    }

    var profilePath = GetExistingOrPendingProfilePath(profileName);
    if (profilePath is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    var backupPath = GetProfileBackupPath(profilePath);
    if (dryRun)
    {
        Console.WriteLine("Dry run: profile clean");
        Console.WriteLine($"Would clean profile: {profileName}");
        WriteWouldDeleteIfExists("profile file", profilePath);
        WriteWouldDeleteIfExists("backup", backupPath);
        Console.WriteLine("No files were changed.");
        return;
    }

    if (!ReadYesNoDefaultYes($"Clean profile and backup: {profileName}? [Y/n]: "))
    {
        Console.WriteLine("Profile clean was canceled.");
        return;
    }

    try
    {
        var removedProfile = DeleteFileIfExists(profilePath);
        var removedBackup = DeleteFileIfExists(backupPath);

        if (!removedProfile && !removedBackup)
        {
            Console.Error.WriteLine($"SSH profile was not found: {profileName}");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine($"Cleaned profile: {profileName}");
        if (removedProfile)
        {
            Console.WriteLine($"Removed profile file: {profilePath}");
        }

        if (removedBackup)
        {
            Console.WriteLine($"Removed backup: {backupPath}");
        }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static void CleanProfilesByPattern(string profilePattern, bool dryRun)
{
    var targets = ResolveProfileTargets(profilePattern, ProfileTargetKind.ExistingOrPending, rejectPendingBackups: false);
    if (targets is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Matched profiles: {targets.Count}");
    foreach (var target in targets)
    {
        Console.WriteLine($"  {target.ProfileName}");
    }

    if (dryRun)
    {
        Console.WriteLine("Dry run: profile clean");
        foreach (var target in targets)
        {
            WriteWouldDeleteIfExists("profile file", target.ProfilePath);
            WriteWouldDeleteIfExists("backup", GetProfileBackupPath(target.ProfilePath));
        }

        Console.WriteLine("No files were changed.");
        return;
    }

    if (!ReadYesNoDefaultYes($"Clean {targets.Count} profiles and backups matching `{profilePattern.Trim()}`? [Y/n]: "))
    {
        Console.WriteLine("Profile clean was canceled.");
        return;
    }

    try
    {
        foreach (var target in targets)
        {
            DeleteFileIfExists(target.ProfilePath);
            DeleteFileIfExists(GetProfileBackupPath(target.ProfilePath));
        }

        Console.WriteLine($"Cleaned profiles: {targets.Count}");
        foreach (var target in targets)
        {
            Console.WriteLine($"  {target.ProfileName}: {target.ProfilePath}");
        }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static void RollbackProfile(string profileName, bool dryRun)
{
    if (ContainsWildcard(profileName))
    {
        RollbackProfilesByPattern(profileName, dryRun);
        return;
    }

    var profilePath = GetExistingOrPendingProfilePath(profileName);
    if (profilePath is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    var backupPath = GetProfileBackupPath(profilePath);
    if (!File.Exists(backupPath))
    {
        Console.Error.WriteLine($"Pending profile backup was not found: {backupPath}");
        Environment.ExitCode = 1;
        return;
    }

    if (dryRun)
    {
        Console.WriteLine("Dry run: profile rollback");
        Console.WriteLine($"Would roll back profile: {profileName}");
        Console.WriteLine($"Would restore backup: {backupPath}");
        Console.WriteLine($"Would write profile file: {profilePath}");
        Console.WriteLine("No files were changed.");
        return;
    }

    try
    {
        File.Move(backupPath, profilePath, overwrite: true);
        Console.WriteLine($"Rolled back profile: {profileName}");
        Console.WriteLine($"Profile file: {profilePath}");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static void RollbackProfilesByPattern(string profilePattern, bool dryRun)
{
    var targets = ResolveProfileTargets(profilePattern, ProfileTargetKind.Pending, rejectPendingBackups: false);
    if (targets is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Matched pending profiles: {targets.Count}");
    foreach (var target in targets)
    {
        Console.WriteLine($"  {target.ProfileName}");
    }

    if (dryRun)
    {
        Console.WriteLine("Dry run: profile rollback");
        foreach (var target in targets)
        {
            Console.WriteLine($"Would restore backup: {GetProfileBackupPath(target.ProfilePath)}");
            Console.WriteLine($"Would write profile file: {target.ProfilePath}");
        }

        Console.WriteLine("No files were changed.");
        return;
    }

    if (!ReadYesNoDefaultYes($"Rollback {targets.Count} profiles matching `{profilePattern.Trim()}`? [Y/n]: "))
    {
        Console.WriteLine("Profile rollback was canceled.");
        return;
    }

    try
    {
        foreach (var target in targets)
        {
            File.Move(GetProfileBackupPath(target.ProfilePath), target.ProfilePath, overwrite: true);
        }

        Console.WriteLine($"Rolled back profiles: {targets.Count}");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static void DeleteProfile(string profileName, bool noBackup, bool dryRun)
{
    if (ContainsWildcard(profileName))
    {
        DeleteProfilesByPattern(profileName, noBackup, dryRun);
        return;
    }

    var profilePath = GetExistingOrPendingProfilePath(profileName);
    if (profilePath is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    if (File.Exists(GetProfileBackupPath(profilePath)))
    {
        Console.WriteLine($"Warning: profile backup is already pending: {GetProfileBackupPath(profilePath)}");
        Console.WriteLine($"Run `kelpie profile commit {profileName}` or `kelpie profile rollback {profileName}`.");
        return;
    }

    if (dryRun)
    {
        Console.WriteLine("Dry run: profile delete");
        Console.WriteLine($"Would delete profile: {profileName}");
        Console.WriteLine($"Would delete profile file: {profilePath}");
        if (noBackup)
        {
            Console.WriteLine("Would not create backup because --no-backup is specified.");
        }
        else
        {
            Console.WriteLine($"Would create backup: {GetProfileBackupPath(profilePath)}");
        }

        Console.WriteLine("No files were changed.");
        return;
    }

    if (!ReadYesNoDefaultYes($"Delete profile: {profileName}? [Y/n]: "))
    {
        Console.WriteLine("Profile delete was canceled.");
        return;
    }

    ProfileTransaction? transaction = null;
    try
    {
        transaction = noBackup ? null : BeginProfileTransaction(profilePath);
        File.Delete(profilePath);
        Console.WriteLine($"Deleted profile: {profileName}");
        Console.WriteLine($"Profile file: {profilePath}");
        if (transaction is not null)
        {
            FinishProfileTransaction(profileName, transaction);
        }
        else
        {
            Console.WriteLine($"Committed profile: {profileName}");
        }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        transaction?.Rollback();
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static void DeleteProfilesByPattern(string profilePattern, bool noBackup, bool dryRun)
{
    var pendingTargets = ResolvePendingProfileTargets(profilePattern);
    if (pendingTargets is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    var targets = ResolveProfileTargets(
        profilePattern,
        ProfileTargetKind.Existing,
        rejectPendingBackups: false,
        allowNoMatches: pendingTargets.Count > 0);
    if (targets is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    if (pendingTargets.Count > 0)
    {
        Console.WriteLine("Warning: matching profile backups are already pending and will be skipped:");
        foreach (var pendingTarget in pendingTargets)
        {
            Console.WriteLine($"  {pendingTarget.ProfileName}: {GetProfileBackupPath(pendingTarget.ProfilePath)}");
        }

        var pendingProfileNames = pendingTargets
            .Select(target => target.ProfileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        targets = targets
            .Where(target => !pendingProfileNames.Contains(target.ProfileName))
            .ToArray();
    }

    if (targets.Count == 0)
    {
        return;
    }

    Console.WriteLine($"Matched profiles: {targets.Count}");
    foreach (var target in targets)
    {
        Console.WriteLine($"  {target.ProfileName}");
    }

    if (dryRun)
    {
        Console.WriteLine("Dry run: profile delete");
        foreach (var target in targets)
        {
            Console.WriteLine($"Would delete profile file: {target.ProfilePath}");
            if (noBackup)
            {
                Console.WriteLine("Would not create backup because --no-backup is specified.");
            }
            else
            {
                Console.WriteLine($"Would create backup: {GetProfileBackupPath(target.ProfilePath)}");
            }
        }

        Console.WriteLine("No files were changed.");
        return;
    }

    if (!ReadYesNoDefaultYes($"Delete {targets.Count} profiles matching `{profilePattern.Trim()}`? [Y/n]: "))
    {
        Console.WriteLine("Profile delete was canceled.");
        return;
    }

    var transactions = new List<ProfileTransaction>();
    try
    {
        foreach (var target in targets)
        {
            if (!noBackup)
            {
                var transaction = BeginProfileTransaction(target.ProfilePath);
                transactions.Add(transaction);
            }

            File.Delete(target.ProfilePath);
        }

        Console.WriteLine($"Deleted profiles: {targets.Count}");
        foreach (var target in targets)
        {
            Console.WriteLine($"  {target.ProfileName}: {target.ProfilePath}");
        }

        if (noBackup)
        {
            Console.WriteLine($"Committed profiles: {targets.Count}");
        }
        else
        {
            FinishProfileTransactions(targets, transactions);
        }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        foreach (var transaction in transactions.AsEnumerable().Reverse())
        {
            transaction.Rollback();
        }

        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static IReadOnlyList<ProfileTarget>? ResolveProfileTargets(
    string profilePattern,
    ProfileTargetKind targetKind,
    bool rejectPendingBackups,
    bool allowNoMatches = false)
{
    var pattern = profilePattern?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(pattern))
    {
        Console.Error.WriteLine("Profile pattern is required.");
        return null;
    }

    if (!IsValidProfileWildcardPattern(pattern))
    {
        Console.Error.WriteLine("Profile pattern must be a file-name pattern without path separators.");
        return null;
    }

    var profilesDirectory = KelpieRuntimePaths.GetProfilesDirectory(AppContext.BaseDirectory);
    if (!Directory.Exists(profilesDirectory))
    {
        Console.Error.WriteLine("Kelpie home is not initialized. Run `kelpie init` first.");
        return null;
    }

    var regex = CreateWildcardRegex(pattern);

    if (rejectPendingBackups)
    {
        var pendingProfiles = EnumeratePendingProfileNames(profilesDirectory)
            .Where(profileName => regex.IsMatch(profileName))
            .OrderBy(profileName => profileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (pendingProfiles.Length > 0)
        {
            Console.Error.WriteLine("Pending profile backups match the pattern:");
            foreach (var pendingProfile in pendingProfiles)
            {
                var profilePath = Path.Combine(profilesDirectory, pendingProfile + ".json");
                Console.Error.WriteLine($"  {GetProfileBackupPath(profilePath)}");
            }

            Console.Error.WriteLine("Run `kelpie profile commit <profile>` or `kelpie profile rollback <profile>` first.");
            return null;
        }
    }

    var targets = EnumerateProfileTargets(profilesDirectory, targetKind)
        .Where(target => regex.IsMatch(target.ProfileName))
        .OrderBy(target => target.ProfileName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (targets.Length == 0 && !allowNoMatches)
    {
        Console.Error.WriteLine($"No SSH profiles matched: {pattern}");
        return null;
    }

    return targets;
}

static IEnumerable<ProfileTarget> EnumerateProfileTargets(string profilesDirectory, ProfileTargetKind targetKind)
{
    if (targetKind == ProfileTargetKind.Pending)
    {
        return EnumeratePendingProfileNames(profilesDirectory)
            .Select(profileName => new ProfileTarget(
                profileName,
                Path.GetFullPath(Path.Combine(profilesDirectory, profileName + ".json"))));
    }

    if (targetKind == ProfileTargetKind.ExistingOrPending)
    {
        return Directory
            .EnumerateFiles(profilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(profilePath => new ProfileTarget(
                Path.GetFileNameWithoutExtension(profilePath),
                Path.GetFullPath(profilePath)))
            .Concat(EnumeratePendingProfileNames(profilesDirectory)
                .Select(profileName => new ProfileTarget(
                    profileName,
                    Path.GetFullPath(Path.Combine(profilesDirectory, profileName + ".json")))))
            .GroupBy(target => target.ProfileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
    }

    return Directory
        .EnumerateFiles(profilesDirectory, "*.json", SearchOption.TopDirectoryOnly)
        .Select(profilePath => new ProfileTarget(
            Path.GetFileNameWithoutExtension(profilePath),
            Path.GetFullPath(profilePath)));
}

static IReadOnlyList<ProfileTarget>? ResolvePendingProfileTargets(string profilePattern)
{
    return ResolveProfileTargets(profilePattern, ProfileTargetKind.Pending, rejectPendingBackups: false, allowNoMatches: true);
}

static IEnumerable<string> EnumeratePendingProfileNames(string profilesDirectory)
{
    return Directory
        .EnumerateFiles(profilesDirectory, "*.json.kelpie", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName)
        .Where(fileName => fileName is not null && fileName.EndsWith(".json.kelpie", StringComparison.OrdinalIgnoreCase))
        .Select(fileName => fileName![..^".json.kelpie".Length]);
}

static bool ContainsWildcard(string value)
{
    return value.Contains('*', StringComparison.Ordinal)
        || value.Contains('?', StringComparison.Ordinal);
}

static bool IsValidProfileWildcardPattern(string pattern)
{
    if (pattern.Contains('/', StringComparison.Ordinal) || pattern.Contains('\\', StringComparison.Ordinal))
    {
        return false;
    }

    foreach (var invalidChar in Path.GetInvalidFileNameChars())
    {
        if (invalidChar is '*' or '?')
        {
            continue;
        }

        if (pattern.Contains(invalidChar, StringComparison.Ordinal))
        {
            return false;
        }
    }

    return true;
}

static Regex CreateWildcardRegex(string pattern)
{
    var escaped = Regex.Escape(pattern)
        .Replace("\\*", ".*", StringComparison.Ordinal)
        .Replace("\\?", ".", StringComparison.Ordinal);
    return new Regex("^" + escaped + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}

static void RunProfileEdit(string[] args)
{
    if (!TryExtractNoBackupOption(args, out var editArgs, out var noBackup))
    {
        WriteProfileEditUsage();
        Environment.ExitCode = 1;
        return;
    }

    if (!TryExtractDryRunOption(editArgs, out editArgs, out var dryRun))
    {
        WriteProfileEditUsage();
        Environment.ExitCode = 1;
        return;
    }

    args = editArgs;

    if (args.Length < 3)
    {
        WriteProfileEditUsage();
        Environment.ExitCode = 1;
        return;
    }

    var profileName = args[2];
    if (ContainsWildcard(profileName))
    {
        Console.Error.WriteLine("Profile edit requires a single profile name. Wildcards are not supported.");
        Environment.ExitCode = 1;
        return;
    }

    var profilePath = GetExistingProfilePath(profileName);
    if (profilePath is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    if (File.Exists(GetProfileBackupPath(profilePath)))
    {
        WritePendingProfileTransactionError(profileName, profilePath);
        Environment.ExitCode = 1;
        return;
    }

    var editService = CreateProfileEditService();

    if (args.Length == 3)
    {
        if (dryRun)
        {
            Console.Error.WriteLine("Profile editor mode does not support --dry-run. Use set/add-root/rm-root/add-deny/rm-deny.");
            Environment.ExitCode = 1;
            return;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Editor mode requires an interactive console.");
            Environment.ExitCode = 1;
            return;
        }

        var editorCommand = ProfileEditorCommandResolver.Resolve(
            LoadConfiguration()["Editor"],
            Environment.GetEnvironmentVariable,
            OperatingSystem.IsWindows());
        RunTransactionalProfileEdit(
            profileName,
            profilePath,
            () => editService.EditWithEditor(profilePath, editorCommand, ReadProfileEditRecoveryAction),
            noBackup);
        return;
    }

    var operation = args[3];
    if (string.Equals(operation, "set", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 6)
        {
            WriteProfileEditUsage();
            Environment.ExitCode = 1;
            return;
        }

        RunProfileEditOperation(profileName, profilePath, path => editService.SetScalar(path, args[4], args[5]), noBackup, dryRun);
        return;
    }

    if (string.Equals(operation, "add-root", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 6)
        {
            WriteProfileEditUsage();
            Environment.ExitCode = 1;
            return;
        }

        RunProfileEditOperation(profileName, profilePath, path => editService.AddRoot(path, args[4], args[5]), noBackup, dryRun);
        return;
    }

    if (string.Equals(operation, "rm-root", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 5)
        {
            WriteProfileEditUsage();
            Environment.ExitCode = 1;
            return;
        }

        RunProfileEditOperation(profileName, profilePath, path => editService.RemoveRoot(path, args[4]), noBackup, dryRun);
        return;
    }

    if (string.Equals(operation, "add-deny", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 5)
        {
            WriteProfileEditUsage();
            Environment.ExitCode = 1;
            return;
        }

        RunProfileEditOperation(profileName, profilePath, path => editService.AddDeny(path, args[4]), noBackup, dryRun);
        return;
    }

    if (string.Equals(operation, "rm-deny", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 5)
        {
            WriteProfileEditUsage();
            Environment.ExitCode = 1;
            return;
        }

        RunProfileEditOperation(profileName, profilePath, path => editService.RemoveDeny(path, args[4]), noBackup, dryRun);
        return;
    }

    WriteProfileEditUsage();
    Environment.ExitCode = 1;
}

static SshProfileEditService CreateProfileEditService()
{
    return new SshProfileEditService(new ProcessEditorLauncher());
}

static string? GetExistingProfilePath(string profileName)
{
    try
    {
        var homeDirectory = KelpieRuntimePaths.GetHomeDirectory(AppContext.BaseDirectory);
        var profilePath = KelpieHomeInitializer.GetProfilePath(homeDirectory, profileName);
        if (File.Exists(profilePath))
        {
            return profilePath;
        }

        Console.Error.WriteLine($"SSH profile was not found: {profileName}");
        Console.Error.WriteLine($"Use `kelpie profile create {profileName}` to create it.");
        return null;
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return null;
    }
}

static string? GetExistingOrPendingProfilePath(string profileName)
{
    try
    {
        var homeDirectory = KelpieRuntimePaths.GetHomeDirectory(AppContext.BaseDirectory);
        var profilePath = KelpieHomeInitializer.GetProfilePath(homeDirectory, profileName);
        if (File.Exists(profilePath) || File.Exists(GetProfileBackupPath(profilePath)))
        {
            return profilePath;
        }

        Console.Error.WriteLine($"SSH profile was not found: {profileName}");
        return null;
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return null;
    }
}

static string GetProfileBackupPath(string profilePath)
{
    return profilePath + ".kelpie";
}

static bool DeleteFileIfExists(string path)
{
    if (!File.Exists(path))
    {
        return false;
    }

    File.Delete(path);
    return true;
}

static void WriteWouldDeleteIfExists(string label, string path)
{
    if (File.Exists(path))
    {
        Console.WriteLine($"Would delete {label}: {path}");
    }
    else
    {
        Console.WriteLine($"Would not delete missing {label}: {path}");
    }
}

static ProfileTransaction BeginProfileTransaction(string profilePath)
{
    var backupPath = GetProfileBackupPath(profilePath);
    if (File.Exists(backupPath))
    {
        throw new IOException($"Pending profile backup already exists: {backupPath}");
    }

    File.Copy(profilePath, backupPath, overwrite: false);
    return new ProfileTransaction(profilePath, backupPath);
}

static void FinishProfileTransaction(string profileName, ProfileTransaction transaction)
{
    if (ReadYesNoDefaultYes("Commit profile? [Y/n]: "))
    {
        transaction.Commit();
        Console.WriteLine($"Committed profile: {profileName}");
        return;
    }

    Console.WriteLine($"Profile backup is pending: {transaction.BackupPath}");
    Console.WriteLine($"Run `kelpie profile commit {profileName}` or `kelpie profile rollback {profileName}`.");
}

static void FinishProfileTransactions(IReadOnlyList<ProfileTarget> targets, IReadOnlyList<ProfileTransaction> transactions)
{
    if (ReadYesNoDefaultYes("Commit profiles? [Y/n]: "))
    {
        foreach (var transaction in transactions)
        {
            transaction.Commit();
        }

        Console.WriteLine($"Committed profiles: {targets.Count}");
        return;
    }

    Console.WriteLine("Profile backups are pending:");
    foreach (var transaction in transactions)
    {
        Console.WriteLine($"  {transaction.BackupPath}");
    }

    Console.WriteLine("Run `kelpie profile commit <profile>` or `kelpie profile rollback <profile>` for each pending profile.");
}

static void WritePendingProfileTransactionError(string profileName, string profilePath)
{
    var backupPath = GetProfileBackupPath(profilePath);
    Console.Error.WriteLine($"Pending profile backup exists: {backupPath}");
    Console.Error.WriteLine($"Run `kelpie profile commit {profileName}` or `kelpie profile rollback {profileName}` first.");
}

static void RunProfileEditOperation(
    string profileName,
    string profilePath,
    Func<string, ProfileEditResult> edit,
    bool noBackup,
    bool dryRun)
{
    if (dryRun)
    {
        RunDryRunProfileEdit(profileName, profilePath, edit, noBackup);
        return;
    }

    RunTransactionalProfileEdit(profileName, profilePath, () => edit(profilePath), noBackup);
}

static void RunDryRunProfileEdit(string profileName, string profilePath, Func<string, ProfileEditResult> edit, bool noBackup)
{
    var tempPath = Path.Combine(
        Path.GetTempPath(),
        $"kelpie-profile-dry-run-{Guid.NewGuid():N}.json");
    try
    {
        File.Copy(profilePath, tempPath, overwrite: false);
        var result = edit(tempPath);
        if (!result.Success)
        {
            Console.Error.WriteLine(result.ErrorMessage);
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("Dry run: profile edit");
        Console.WriteLine($"Would update profile: {profileName}");
        Console.WriteLine($"Profile file: {profilePath}");
        if (noBackup)
        {
            Console.WriteLine("Would not create backup because --no-backup is specified.");
        }
        else
        {
            Console.WriteLine($"Would create backup: {GetProfileBackupPath(profilePath)}");
        }

        Console.WriteLine("Would write:");
        Console.Write(File.ReadAllText(tempPath));
        Console.WriteLine("No files were changed.");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
    finally
    {
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }
    }
}

static void RunTransactionalProfileEdit(string profileName, string profilePath, Func<ProfileEditResult> edit, bool noBackup)
{
    ProfileTransaction? transaction = null;
    try
    {
        transaction = noBackup ? null : BeginProfileTransaction(profilePath);
        var result = edit();
        WriteProfileEditResult(profileName, result, transaction);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        transaction?.Rollback();
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
}

static bool ReadYesNoDefaultYes(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var value = Console.ReadLine();
        if (value is null)
        {
            return false;
        }

        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || string.Equals(normalized, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(normalized, "n", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Console.Error.WriteLine("Enter Y or N.");
    }
}

static void WriteProfileEditResult(string profileName, ProfileEditResult result, ProfileTransaction? transaction)
{
    if (!result.Success)
    {
        transaction?.Rollback();
        Console.Error.WriteLine(result.ErrorMessage);
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Updated profile: {profileName}");
    Console.WriteLine($"Profile file: {Path.GetFullPath(result.ProfilePath)}");
    if (transaction is not null)
    {
        FinishProfileTransaction(profileName, transaction);
    }
    else
    {
        Console.WriteLine($"Committed profile: {profileName}");
    }
}

static ProfileEditRecoveryAction ReadProfileEditRecoveryAction(string validationError)
{
    Console.Error.WriteLine("Profile validation failed:");
    Console.Error.WriteLine(validationError);

    while (true)
    {
        Console.Error.Write("Re-edit profile? [Y/n]: ");
        var value = Console.ReadLine();
        if (value is null)
        {
            return ProfileEditRecoveryAction.Abort;
        }

        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || string.Equals(normalized, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return ProfileEditRecoveryAction.Retry;
        }

        if (string.Equals(normalized, "n", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase))
        {
            return ProfileEditRecoveryAction.Abort;
        }

        Console.Error.WriteLine("Enter Y to re-edit or N to abort.");
    }
}

static void WriteProfileEditUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  kelpie profile edit <profile> [--no-backup]");
    Console.Error.WriteLine("  kelpie profile edit <profile> set <dotPath> <value> [--no-backup] [--dry-run]");
    Console.Error.WriteLine("  kelpie profile edit <profile> add-root <path> <access> [--no-backup] [--dry-run]");
    Console.Error.WriteLine("  kelpie profile edit <profile> rm-root <path> [--no-backup] [--dry-run]");
    Console.Error.WriteLine("  kelpie profile edit <profile> add-deny <pattern> [--no-backup] [--dry-run]");
    Console.Error.WriteLine("  kelpie profile edit <profile> rm-deny <pattern> [--no-backup] [--dry-run]");
    Console.Error.WriteLine("  kelpie profile delete <profile-pattern> [--no-backup] [--dry-run]");
    Console.Error.WriteLine("  kelpie profile clean <profile-pattern> [--dry-run]");
    Console.Error.WriteLine("  kelpie profile commit <profile-pattern> [--dry-run]");
    Console.Error.WriteLine("  kelpie profile rollback <profile-pattern> [--dry-run]");
}

static KelpieMcpConfigTemplateOptions ReadMcpConfigTemplateOptions(string defaultLogDirectory)
{
    var defaults = KelpieMcpConfigTemplateOptions.CreateDefault(defaultLogDirectory);

    Console.WriteLine("Create MCP server configuration.");
    Console.WriteLine("Press Enter to use the default value.");

    var logDirectory = ReadPrompt("MCP log directory", defaults.LogDirectory);
    var controlPipeName = ReadPrompt("MCP control pipe name", defaults.ControlPipeName);

    return new KelpieMcpConfigTemplateOptions(
        LogDirectory: logDirectory,
        ControlPipeName: controlPipeName);
}

static KelpieProfileTemplateOptions ReadProfileTemplateOptions(string profileName)
{
    var defaults = KelpieProfileTemplateOptions.CreateDefault(profileName);

    Console.WriteLine("Create SSH profile template.");
    Console.WriteLine("Press Enter to use the default value.");

    var hostAddress = ReadPrompt("Host address", defaults.HostAddress);
    var port = ReadPortPrompt("Port", defaults.Port);
    var defaultUser = ReadPrompt("SSH user", defaults.DefaultUser);
    var authMethod = ReadChoicePrompt("Authentication method", defaults.AuthMethod, ["privateKey", "password"]);
    var privateKeyFile = string.Equals(authMethod, "privateKey", StringComparison.OrdinalIgnoreCase)
        ? ReadPrompt("Private key file", defaults.PrivateKeyFile ?? $"{profileName}_ed25519")
        : defaults.PrivateKeyFile;
    var passwordSecretName = string.Equals(authMethod, "password", StringComparison.OrdinalIgnoreCase)
        ? ReadPrompt("Password secret name", defaults.PasswordSecretName ?? $"kelpie:{profileName}")
        : defaults.PasswordSecretName;
    var osFamily = ReadPrompt("OS family", defaults.OsFamily);
    var mode = ReadChoicePrompt("Mode", defaults.Mode, ["ReadOnly", "Safe", "Maintenance", "Expert"]);
    var readOnlyRoots = ReadOptionalPromptList("Read-only root", defaults.ReadOnlyRoots);
    var readWriteRoots = ReadOptionalPromptList("Read-write root", defaults.ReadWriteRoots);
    var denyPatterns = ReadOptionalPromptList("Deny pattern", defaults.DenyPatterns);

    return new KelpieProfileTemplateOptions(
        HostAddress: hostAddress,
        Port: port,
        AuthMethod: authMethod,
        PrivateKeyFile: privateKeyFile,
        PasswordSecretName: passwordSecretName,
        DefaultUser: defaultUser,
        Mode: mode,
        OsFamily: osFamily,
        ReadOnlyRoots: readOnlyRoots,
        ReadWriteRoots: readWriteRoots,
        DenyPatterns: denyPatterns);
}

static string ReadPrompt(string title, string defaultValue)
{
    while (true)
    {
        Console.Write($"{title} [{defaultValue}]: ");
        var value = Console.ReadLine();
        if (value is null)
        {
            return defaultValue;
        }

        var trimmed = value.Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        return defaultValue;
    }
}

static IReadOnlyList<string> ReadOptionalPromptList(string title, IReadOnlyCollection<string> defaultValues)
{
    return ProfilePromptListReader.Read(Console.In, Console.Out, title, defaultValues);
}

static int ReadPortPrompt(string title, int defaultValue)
{
    while (true)
    {
        var value = ReadPrompt(title, defaultValue.ToString());
        if (int.TryParse(value, out var port) && port is >= 1 and <= 65535)
        {
            return port;
        }

        if (Console.IsInputRedirected)
        {
            throw new ArgumentException("Port must be a number between 1 and 65535.");
        }

        Console.Error.WriteLine("Port must be a number between 1 and 65535.");
    }
}

static string ReadChoicePrompt(string title, string defaultValue, IReadOnlyCollection<string> allowedValues)
{
    while (true)
    {
        var value = ReadPrompt($"{title} ({string.Join("/", allowedValues)})", defaultValue);
        var matchedValue = allowedValues.FirstOrDefault(
            allowedValue => string.Equals(allowedValue, value, StringComparison.OrdinalIgnoreCase));
        if (matchedValue is not null)
        {
            return matchedValue;
        }

        if (Console.IsInputRedirected)
        {
            throw new ArgumentException($"{title} must be one of: {string.Join(", ", allowedValues)}.");
        }

        Console.Error.WriteLine($"{title} must be one of: {string.Join(", ", allowedValues)}.");
    }
}

static void StartLoginConsole(string profileName)
{
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("`kelpie login --console` is supported on Windows only.");
        Environment.ExitCode = 1;
        return;
    }

    var executablePath = Environment.ProcessPath
        ?? throw new InvalidOperationException("Kelpie executable path was not found.");
    var title = $"Kelpie {profileName}";
    var startArguments = $"/c start \"{EscapeCmdArgument(title)}\" \"{EscapeCmdArgument(executablePath)}\" login --console-session";

    using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
    {
        FileName = "cmd.exe",
        Arguments = startArguments,
        UseShellExecute = false,
        CreateNoWindow = true,
    });

    if (process is null)
    {
        throw new InvalidOperationException("Failed to start login window.");
    }

    KpLog.Info($"Kelpie login console started. profile={profileName}");
    Console.WriteLine($"Kelpie login console started: {profileName}");
}

static string EscapeCmdArgument(string value)
{
    return value.Replace("\"", "\"\"", StringComparison.Ordinal);
}

static void ShowVersion()
{
    var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    Console.WriteLine($"kelpie {version ?? "unknown"}");
}

static void ShowProfiles(SshConnectionProfileCatalog catalog)
{
    var profiles = catalog.List();
    if (profiles.Count == 0)
    {
        Console.WriteLine("No SSH profiles found.");
        return;
    }

    foreach (var profile in profiles)
    {
        Console.WriteLine(profile.Name);
    }
}

static void OpenProfile(SshConnectionProfileCatalog catalog, string profileName)
{
    if (!TryResolveProfile(catalog, profileName, out var profile))
    {
        Environment.ExitCode = 1;
        return;
    }

    SaveOpenProfileName(profile.Name);
    Console.WriteLine($"Opened profile: {profile.Name}");
    Console.WriteLine("Use `kelpie login` to start a session.");
}

static void ShowProfile(SshConnectionProfileCatalog catalog, string profileName)
{
    if (!TryResolveProfile(catalog, profileName, out var profile))
    {
        Environment.ExitCode = 1;
        return;
    }

    WriteProfileSummary(profile, includeAuthenticationDetails: true);
}

static void ShowProfilesByPattern(SshConnectionProfileCatalog catalog, string profilePattern)
{
    var pattern = profilePattern?.Trim() ?? string.Empty;
    if (string.IsNullOrWhiteSpace(pattern))
    {
        Console.Error.WriteLine("Profile pattern is required.");
        Environment.ExitCode = 1;
        return;
    }

    if (!IsValidProfileWildcardPattern(pattern))
    {
        Console.Error.WriteLine("Profile pattern must be a file-name pattern without path separators.");
        Environment.ExitCode = 1;
        return;
    }

    var regex = CreateWildcardRegex(pattern);
    var profiles = catalog
        .List()
        .Where(profile => regex.IsMatch(profile.Name))
        .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (profiles.Length == 0)
    {
        Console.Error.WriteLine($"No SSH profiles matched: {pattern}");
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Matched profiles: {profiles.Length}");
    for (var i = 0; i < profiles.Length; i++)
    {
        if (i > 0)
        {
            Console.WriteLine();
        }

        WriteProfileSummary(profiles[i], includeAuthenticationDetails: true);
    }
}

static async Task ShowStatusAsync(
    SshConnectionProfileCatalog catalog,
    KelpieMcpServerOptions commandOptions,
    string profileName)
{
    if (!TryResolveProfile(catalog, profileName, out var profile))
    {
        Environment.ExitCode = 1;
        return;
    }

    WriteProfileSummary(profile, includeAuthenticationDetails: true);
    Console.WriteLine();
    await KelpieServerCommandRunner.StatusAsync(commandOptions);
    Console.WriteLine();
    await KelpieServerCommandRunner.SessionsAsync(commandOptions, setExitCodeWhenUnavailable: false);
}

static async Task RunDiagnosticsAsync(SshConnectionProfileCatalog catalog, string profileName)
{
    if (!TryResolveProfile(catalog, profileName, out var profile))
    {
        Environment.ExitCode = 1;
        return;
    }

    var service = CreateSshCommandService(profile);
    var commandNames = new[]
    {
        "get_system_info",
        "get_disk_usage",
        "get_memory_usage",
        "get_listening_ports",
        "get_failed_services",
    };

    foreach (var commandName in commandNames)
    {
        await ExecuteAndPrintAsync(service, profile, commandName);
    }
}

static async Task RunInventoryAsync(SshConnectionProfileCatalog catalog, string profileName)
{
    if (!TryResolveProfile(catalog, profileName, out var profile))
    {
        Environment.ExitCode = 1;
        return;
    }

    await ExecuteAndPrintAsync(CreateSshCommandService(profile), profile, "target_inventory");
}

static async Task RunLogsAsync(
    SshConnectionProfileCatalog catalog,
    string profileName,
    string serviceName,
    string lines)
{
    if (string.IsNullOrWhiteSpace(serviceName))
    {
        Console.Error.WriteLine("Service name is required.");
        Environment.ExitCode = 1;
        return;
    }

    if (!TryResolveProfile(catalog, profileName, out var profile))
    {
        Environment.ExitCode = 1;
        return;
    }

    var service = CreateSshCommandService(profile);
    await ExecuteAndPrintAsync(
        service,
        profile,
        "tail_log",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["service"] = serviceName,
            ["lines"] = lines,
        });
}

static async Task RunPackageAsync(SshConnectionProfileCatalog catalog, string[] args)
{
    var subcommand = args.Length > 1 ? args[1] : string.Empty;
    if (IsHelpCommand(subcommand))
    {
        WritePackageUsage(Console.Out);
        return;
    }

    if (string.Equals(subcommand, "check-updates", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 3)
        {
            WritePackageUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        await ExecutePackageCommandAsync(catalog, args[2], "pkg_check_updates");
        return;
    }

    if (string.Equals(subcommand, "info", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 4)
        {
            WritePackageUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        await ExecutePackageCommandAsync(
            catalog,
            args[2],
            "pkg_info",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package"] = args[3],
            });
        return;
    }

    if (string.Equals(subcommand, "search", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length is not 4 and not 5)
        {
            WritePackageUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        await ExecutePackageCommandAsync(
            catalog,
            args[2],
            "pkg_search",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = args[3],
                ["limit"] = args.Length == 5 ? args[4] : "20",
            });
        return;
    }

    if (string.Equals(subcommand, "list-installed", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length is not 4 and not 5)
        {
            WritePackageUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        await ExecutePackageCommandAsync(
            catalog,
            args[2],
            "pkg_list_installed",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["filter"] = args[3],
                ["limit"] = args.Length == 5 ? args[4] : "50",
            });
        return;
    }

    if (string.Equals(subcommand, "simulate-install", StringComparison.OrdinalIgnoreCase)
        || string.Equals(subcommand, "simulate-remove", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 4)
        {
            WritePackageUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        await ExecutePackageCommandAsync(
            catalog,
            args[2],
            string.Equals(subcommand, "simulate-install", StringComparison.OrdinalIgnoreCase)
                ? "pkg_simulate_install"
                : "pkg_simulate_remove",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package"] = args[3],
            });
        return;
    }

    if (string.Equals(subcommand, "install", StringComparison.OrdinalIgnoreCase)
        || string.Equals(subcommand, "remove", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 4
            || !TryExtractConfirmationOption(args.Skip(4).ToArray(), out var confirmation))
        {
            WritePackageUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        var commandName = string.Equals(subcommand, "install", StringComparison.OrdinalIgnoreCase)
            ? "pkg_install"
            : "pkg_remove";

        if (string.IsNullOrWhiteSpace(confirmation))
        {
            PreviewPackageConfirmation(catalog, args[2], commandName, args[3], subcommand);
            return;
        }

        var requiredConfirmation = commandName + ":" + args[3];
        if (!string.Equals(confirmation, requiredConfirmation, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Confirmation is required: {requiredConfirmation}");
            Environment.ExitCode = 1;
            return;
        }

        await ExecutePackageCommandAsync(
            catalog,
            args[2],
            commandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package"] = args[3],
            });
        return;
    }

    WritePackageUsage(Console.Error);
    Environment.ExitCode = 1;
}

static async Task ExecutePackageCommandAsync(
    SshConnectionProfileCatalog catalog,
    string profileName,
    string commandName,
    IReadOnlyDictionary<string, string>? arguments = null)
{
    KpLog.Info($"Kelpie CLI pkg command requested. command={commandName}, profile={profileName}");
    if (!TryResolveProfile(catalog, profileName, out var profile))
    {
        Environment.ExitCode = 1;
        return;
    }

    await ExecuteAndPrintAsync(CreateSshCommandService(profile), profile, commandName, arguments);
}

static void PreviewPackageConfirmation(
    SshConnectionProfileCatalog catalog,
    string profileName,
    string commandName,
    string packageName,
    string subcommand)
{
    if (!TryResolveProfile(catalog, profileName, out var profile))
    {
        Environment.ExitCode = 1;
        return;
    }

    SshCommandPreview preview;
    try
    {
        preview = CreateSshCommandPreviewService().Preview(
            profile,
            commandName,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["package"] = packageName,
            });
    }
    catch (Exception ex) when (ex is InvalidOperationException or KelpiePolicyError or SshException)
    {
        KpLog.Warn(ex.Message);
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
        return;
    }

    var confirmation = commandName + ":" + packageName;
    Console.WriteLine($"# {preview.CommandName}");
    Console.WriteLine("Requires confirmation: true");
    Console.WriteLine($"Confirmation: {confirmation}");
    Console.WriteLine($"Command preview: {preview.CommandText}");
    Console.WriteLine("Not executed.");
    Console.WriteLine($"Run after review: kelpie pkg {subcommand} {profile.Name} {packageName} --confirm {confirmation}");
}

static bool TryExtractConfirmationOption(string[] args, out string? confirmation)
{
    confirmation = null;
    if (args.Length == 0)
    {
        return true;
    }

    if (args.Length == 1 && args[0].StartsWith("--confirm=", StringComparison.OrdinalIgnoreCase))
    {
        confirmation = args[0]["--confirm=".Length..];
        return !string.IsNullOrWhiteSpace(confirmation);
    }

    if (args.Length == 2 && string.Equals(args[0], "--confirm", StringComparison.OrdinalIgnoreCase))
    {
        confirmation = args[1];
        return !string.IsNullOrWhiteSpace(confirmation);
    }

    return false;
}

static void WritePackageUsage(TextWriter writer)
{
    writer.WriteLine("Usage:");
    writer.WriteLine("  kelpie pkg check-updates <profile>");
    writer.WriteLine("  kelpie pkg info <profile> <package>");
    writer.WriteLine("  kelpie pkg search <profile> <query> [limit]");
    writer.WriteLine("  kelpie pkg list-installed <profile> <filter> [limit]");
    writer.WriteLine("  kelpie pkg simulate-install <profile> <package>");
    writer.WriteLine("  kelpie pkg simulate-remove <profile> <package>");
    writer.WriteLine("  kelpie pkg install <profile> <package> [--confirm <token>]");
    writer.WriteLine("  kelpie pkg remove <profile> <package> [--confirm <token>]");
}

static async Task RunEnvironmentAsync(SshConnectionProfileCatalog catalog, string[] args)
{
    var subcommand = args.Length > 1 ? args[1] : string.Empty;
    if (string.Equals(subcommand, "keys", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 3)
        {
            WriteEnvironmentUsage();
            Environment.ExitCode = 1;
            return;
        }

        var profileName = args[2];
        KpLog.Info($"Kelpie CLI env keys requested. profile={profileName}");
        if (!TryResolveProfile(catalog, profileName, out var profile))
        {
            Environment.ExitCode = 1;
            return;
        }

        await ExecuteEnvironmentAndPrintAsync(
            CreateSshCommandService(profile),
            service => service.GetEnvironmentKeysAsync(profile));
        return;
    }

    if (string.Equals(subcommand, "peek", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 4)
        {
            WriteEnvironmentUsage();
            Environment.ExitCode = 1;
            return;
        }

        var profileName = args[2];
        var key = args[3];
        KpLog.Info($"Kelpie CLI env peek requested. profile={profileName}, key={key}");
        if (!TryResolveProfile(catalog, profileName, out var profile))
        {
            Environment.ExitCode = 1;
            return;
        }

        await ExecuteEnvironmentAndPrintAsync(
            CreateSshCommandService(profile),
            service => service.PeekEnvironmentValueAsync(profile, key));
        return;
    }

    if (string.Equals(subcommand, "list", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 3)
        {
            WriteEnvironmentUsage();
            Environment.ExitCode = 1;
            return;
        }

        var profileName = args[2];
        KpLog.Info($"Kelpie CLI env list requested. profile={profileName}");
        if (!TryResolveProfile(catalog, profileName, out var profile))
        {
            Environment.ExitCode = 1;
            return;
        }

        await ExecuteEnvironmentAndPrintAsync(
            CreateSshCommandService(profile),
            service => service.ListPersistentEnvironmentKeysAsync(profile));
        return;
    }

    if (string.Equals(subcommand, "persist", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 5)
        {
            WriteEnvironmentUsage();
            Environment.ExitCode = 1;
            return;
        }

        var profileName = args[2];
        var key = args[3];
        var value = args[4];
        KpLog.Info($"Kelpie CLI env persist requested. profile={profileName}, key={key}");
        if (!TryResolveProfile(catalog, profileName, out var profile))
        {
            Environment.ExitCode = 1;
            return;
        }

        await ExecuteEnvironmentAndPrintAsync(
            CreateSshCommandService(profile),
            service => service.PersistEnvironmentValueAsync(profile, key, value));
        return;
    }

    if (string.Equals(subcommand, "remove", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 4)
        {
            WriteEnvironmentUsage();
            Environment.ExitCode = 1;
            return;
        }

        var profileName = args[2];
        var key = args[3];
        KpLog.Info($"Kelpie CLI env remove requested. profile={profileName}, key={key}");
        if (!TryResolveProfile(catalog, profileName, out var profile))
        {
            Environment.ExitCode = 1;
            return;
        }

        await ExecuteEnvironmentAndPrintAsync(
            CreateSshCommandService(profile),
            service => service.RemovePersistentEnvironmentValueAsync(profile, key));
        return;
    }

    WriteEnvironmentUsage();
    Environment.ExitCode = 1;
}

static void WriteEnvironmentUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  kelpie env keys <profile>");
    Console.Error.WriteLine("  kelpie env peek <profile> <key>");
    Console.Error.WriteLine("  kelpie env list <profile>");
    Console.Error.WriteLine("  kelpie env persist <profile> <key> <value>");
    Console.Error.WriteLine("  kelpie env remove <profile> <key>");
}

static async Task ExecuteEnvironmentAndPrintAsync(
    SshCommandService service,
    Func<SshCommandService, Task<SshCommandResult>> executeAsync)
{
    SshCommandResult result;
    try
    {
        result = await executeAsync(service);
    }
    catch (Exception ex) when (ex is InvalidOperationException or KelpiePolicyError or SshException)
    {
        KpLog.Warn(ex.Message);
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
        return;
    }

    Console.Write(result.StandardOutput);

    if (!string.IsNullOrEmpty(result.StandardError))
    {
        Console.Error.Write(result.StandardError);
    }

    if (result.ExitCode != 0)
    {
        Console.Error.WriteLine($"ExitCode: {result.ExitCode}");
        Environment.ExitCode = result.ExitCode;
    }
}

static async Task RunInteractiveLoginAsync(SshConnectionProfile profile)
{
    var passwordProvider = CreateCliPasswordProvider(profile);

    await using var session = new SshNetInteractiveShellSession(profile, passwordProvider);
    var initialOutput = await session.ConnectAsync();

    Console.WriteLine($"Connected profile: {profile.Name}");
    Console.WriteLine("Type `exit` to close the remote shell.");
    if (!string.IsNullOrEmpty(initialOutput))
    {
        Console.Write(initialOutput);
    }

    while (true)
    {
        Console.Write($"kelpie:{profile.Name}> ");
        var input = Console.ReadLine();
        if (input is null)
        {
            Console.WriteLine();
            return;
        }

        var commandText = input.Trim();
        if (string.IsNullOrWhiteSpace(commandText))
        {
            continue;
        }

        var shouldClose = string.Equals(commandText, "exit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(commandText, "logout", StringComparison.OrdinalIgnoreCase);
        await SendInteractiveCommandAsync(session, profile, commandText);
        if (shouldClose)
        {
            Console.WriteLine($"Session closed: {profile.Name}");
            return;
        }
    }
}

static ISshPasswordProvider? CreateCliPasswordProvider(SshConnectionProfile profile)
{
    if (!string.Equals(profile.AuthenticationMethod, "password", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    if (string.IsNullOrWhiteSpace(profile.PasswordSecretName))
    {
        throw new InvalidOperationException("SSH password secret name is required.");
    }

    var password = ReadPasswordFromConsole(profile.Name);
    var store = new InMemorySshPasswordSessionStore();
    store.SetPasswordSession(profile.Name, profile.PasswordSecretName, password);
    return store;
}

static string ReadPasswordFromConsole(string profileName)
{
    Console.Error.Write($"Password for {profileName}: ");
    if (Console.IsInputRedirected)
    {
        return Console.ReadLine() ?? string.Empty;
    }

    var builder = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.Error.WriteLine();
            return builder.ToString();
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (builder.Length > 0)
            {
                builder.Length--;
            }

            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            builder.Append(key.KeyChar);
        }
    }
}

static SshCommandService CreateSshCommandService(SshConnectionProfile profile)
{
    var passwordProvider = CreateCliPasswordProvider(profile);
    return new SshCommandService(
        CommandProcessingProviderCatalog.CreateDefault(),
        passwordProvider is null ? new SshNetCommandRunner() : new SshNetCommandRunner(passwordProvider));
}

static SshCommandService CreateSshCommandPreviewService()
{
    return new SshCommandService(
        CommandProcessingProviderCatalog.CreateDefault(),
        new SshNetCommandRunner());
}

static async Task ExecuteAndPrintAsync(
    SshCommandService service,
    SshConnectionProfile profile,
    string commandName,
    IReadOnlyDictionary<string, string>? arguments = null)
{
    Console.WriteLine($"# {commandName}");
    SshCommandResult result;
    try
    {
        result = await service.ExecuteAsync(profile, commandName, arguments);
    }
    catch (Exception ex) when (ex is InvalidOperationException or KelpiePolicyError or SshException)
    {
        KpLog.Warn(ex.Message);
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
        return;
    }

    Console.Write(result.StandardOutput);

    if (!string.IsNullOrEmpty(result.StandardError))
    {
        Console.Error.Write(result.StandardError);
    }

    if (result.ExitCode != 0)
    {
        Console.Error.WriteLine($"ExitCode: {result.ExitCode}");
        Environment.ExitCode = result.ExitCode;
    }
}

static async Task SendInteractiveCommandAsync(
    SshNetInteractiveShellSession session,
    SshConnectionProfile profile,
    string commandText)
{
    try
    {
        RawShellCommandPolicy.Default.EnsureAllowed(profile, commandText, KelpieExecutionChannel.Cli);
        var output = await session.SendLineAsync(commandText);
        Console.Write(output);
    }
    catch (KelpiePolicyError ex)
    {
        KpLog.Warn(ex.Message);
        Console.Error.WriteLine(ex.Message);
    }
    catch (Exception ex)
    {
        KpLog.Err($"Interactive SSH command failed. profile={profile.Name}, exceptionType={ex.GetType().FullName ?? "UnknownException"}");
        Console.Error.WriteLine(ex.Message);
    }
}

static bool TryResolveProfile(
    SshConnectionProfileCatalog catalog,
    string profileName,
    out SshConnectionProfile profile)
{
    if (string.IsNullOrWhiteSpace(profileName))
    {
        Console.Error.WriteLine("Profile name is required.");
        profile = default!;
        return false;
    }

    if (catalog.TryGet(profileName, out profile!))
    {
        return true;
    }

    Console.Error.WriteLine($"SSH profile was not found: {profileName}");
    return false;
}

static void WriteProfileSummary(SshConnectionProfile profile, bool includeAuthenticationDetails)
{
    Console.WriteLine($"Profile: {profile.Name}");
    Console.WriteLine($"Host: {profile.Host}");
    Console.WriteLine($"Port: {profile.Port}");
    Console.WriteLine($"Host key SHA256: {FormatHostKeyFingerprint(profile.HostKeyFingerprintSha256)}");
    WriteHostKeyPinWarning(profile);
    Console.WriteLine($"User: {profile.UserName}");
    Console.WriteLine($"OS family: {profile.OsFamily}");
    Console.WriteLine($"Package manager: {profile.PackageManager}");
    Console.WriteLine($"Command OS family: {OsFamilyAliasResolver.Resolve(profile.OsFamily)}");
    WriteCommandProcessingProviders(profile);
    WriteCapabilities(profile.Capabilities);
    WriteRoles(profile.Roles);
    Console.WriteLine($"Effective mode: {profile.Mode}");
    WriteAllowedRoots(profile);
    WriteSpecialPaths(profile.SpecialPaths);
    WriteServices(profile.Services);
    WriteUsers(profile.Users);

    if (!includeAuthenticationDetails)
    {
        return;
    }

    Console.WriteLine($"Authentication: {profile.AuthenticationMethod}");
    if (string.Equals(profile.AuthenticationMethod, "privateKey", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Private key: {FormatConfiguredSecret(profile.PrivateKeyPath)}");
    }
    else if (string.Equals(profile.AuthenticationMethod, "password", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Password secret: {FormatConfiguredSecret(profile.PasswordSecretName)}");
        Console.WriteLine("Password session: use kelpiemcp password <profile> for the running KelpieMCPServer session.");
    }
}

static string FormatHostKeyFingerprint(string? fingerprint)
{
    return string.IsNullOrWhiteSpace(fingerprint)
        ? "(not pinned)"
        : fingerprint.Trim();
}

static void WriteHostKeyPinWarning(SshConnectionProfile profile)
{
    if (!string.IsNullOrWhiteSpace(profile.HostKeyFingerprintSha256))
    {
        return;
    }

    Console.Error.WriteLine(
        $"Warning: SSH host key is not pinned for profile `{profile.Name}`. Verify the first connection out of band, then set Host.HostKeyFingerprintSha256.");
    Console.Error.WriteLine(
        "TOFU note: record the verified SHA256 fingerprint in the profile before relying on this target.");
}

static void WriteAllowedRoots(SshConnectionProfile profile)
{
    Console.WriteLine("Allowed roots:");

    if (profile.AllowedRootRules.Count > 0)
    {
        var pathWidth = profile.AllowedRootRules.Max(rule => rule.Path.Length);
        foreach (var rule in profile.AllowedRootRules)
        {
            Console.WriteLine($"  {rule.Path.PadRight(pathWidth)}  => {AllowedRootAccessText.Format(rule.Access)}");
        }

        return;
    }

    if (profile.AllowedRoots.Count > 0)
    {
        foreach (var root in profile.AllowedRoots)
        {
            Console.WriteLine($"  {root}");
        }

        return;
    }

    Console.WriteLine("  (empty list)");
}

static void WriteCommandProcessingProviders(SshConnectionProfile profile)
{
    var providerNames = CommandProcessingProviderCatalog.CreateDefault()
        .Where(provider => provider.Supports(profile))
        .Select(provider => provider.GetType().Name)
        .ToArray();

    WriteIndentedList("Command providers:", providerNames);
}

static void WriteCapabilities(PolicySet capabilities)
{
    WriteIndentedList("Capabilities:", capabilities.List());
}

static void WriteRoles(IReadOnlyCollection<string> roles)
{
    WriteIndentedList("Roles:", roles);
}

static void WriteSpecialPaths(IReadOnlyCollection<SpecialPathRule> specialPaths)
{
    Console.WriteLine("Special paths:");

    if (specialPaths.Count == 0)
    {
        Console.WriteLine("  (empty list)");
        return;
    }

    var patternWidth = specialPaths.Max(rule => rule.Pattern.Length);
    foreach (var rule in specialPaths)
    {
        Console.WriteLine($"  {rule.Pattern.PadRight(patternWidth)}  => {rule.Action}");
    }
}

static void WriteServices(SshConnectionServices services)
{
    var serviceSummaries = new List<string>();
    if (services.Nginx is not null)
    {
        serviceSummaries.Add($"Nginx {FormatNginxService(services.Nginx)}");
    }

    WriteIndentedList("Services:", serviceSummaries);
}

static void WriteUsers(IReadOnlyCollection<SshConnectionUser> users)
{
    Console.WriteLine("Users:");

    if (users.Count == 0)
    {
        Console.WriteLine("  (empty list)");
        return;
    }

    var userNameWidth = users.Max(user => user.UserName.Length);
    foreach (var user in users)
    {
        var roles = user.Roles.Count == 0
            ? "(empty list)"
            : string.Join("|", user.Roles);
        Console.WriteLine($"  {user.UserName.PadRight(userNameWidth)}  => {roles}");
    }
}

static string FormatNginxService(NginxServiceSettings nginx)
{
    var values = new List<string>();
    if (!string.IsNullOrWhiteSpace(nginx.User))
    {
        values.Add($"User={nginx.User}");
    }

    if (!string.IsNullOrWhiteSpace(nginx.Group))
    {
        values.Add($"Group={nginx.Group}");
    }

    if (nginx.Port.HasValue)
    {
        values.Add($"Port={nginx.Port.Value}");
    }

    if (!string.IsNullOrWhiteSpace(nginx.Root))
    {
        values.Add($"Root={nginx.Root}");
    }

    return values.Count == 0
        ? "(configured)"
        : string.Join(" ", values);
}

static void WriteIndentedList(string title, IReadOnlyCollection<string> values)
{
    Console.WriteLine(title);

    if (values.Count == 0)
    {
        Console.WriteLine("  (empty list)");
        return;
    }

    foreach (var value in values)
    {
        Console.WriteLine($"  {value}");
    }
}

static string FormatConfiguredSecret(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? "(not configured)" : "(configured)";
}

static string GetOpenProfileStatePath()
{
    return KelpieRuntimePaths.GetConfigFilePath(AppContext.BaseDirectory, "kelpie-open-profile.txt");
}

static string GetClientStatePath()
{
    return KelpieRuntimePaths.GetClientStatePath(AppContext.BaseDirectory);
}

static string? LoadOpenProfileName()
{
    return LoadClientState().OpenProfile;
}

static void SaveOpenProfileName(string profileName)
{
    var state = LoadClientState() with { OpenProfile = profileName };
    SaveClientState(state);
}

static KelpieClientState LoadClientState()
{
    TryMigrateLegacyClientState();
    var statePath = GetClientStatePath();
    if (File.Exists(statePath))
    {
        try
        {
            return JsonSerializer.Deserialize<KelpieClientState>(File.ReadAllText(statePath)) ?? new KelpieClientState();
        }
        catch (JsonException ex)
        {
            KpLog.Warn($"Failed to read Kelpie state JSON. path={statePath}, reason={ex.GetType().Name}");
        }
        catch (IOException ex)
        {
            KpLog.Warn($"Failed to read Kelpie state file. path={statePath}, reason={ex.GetType().Name}");
        }
        catch (UnauthorizedAccessException ex)
        {
            KpLog.Warn($"Failed to read Kelpie state file. path={statePath}, reason={ex.GetType().Name}");
        }
    }

    return LoadLegacyClientState();
}

static void TryMigrateLegacyClientState()
{
    try
    {
        if (KelpieRuntimePaths.MigrateLegacyClientStateFile(AppContext.BaseDirectory))
        {
            KpLog.Info("Migrated legacy Kelpie client state to kelpie_client_state.json.");
        }
    }
    catch (IOException ex)
    {
        KpLog.Warn($"Failed to migrate legacy Kelpie client state. reason={ex.GetType().Name}");
    }
    catch (UnauthorizedAccessException ex)
    {
        KpLog.Warn($"Failed to migrate legacy Kelpie client state. reason={ex.GetType().Name}");
    }
}

static KelpieClientState LoadLegacyClientState()
{
    return new KelpieClientState(ReadLegacyStateValue(GetOpenProfileStatePath()));
}

static string? ReadLegacyStateValue(string statePath)
{
    try
    {
        return File.Exists(statePath)
            ? File.ReadAllText(statePath).Trim()
            : null;
    }
    catch (IOException ex)
    {
        KpLog.Warn($"Failed to read legacy Kelpie state file. path={statePath}, reason={ex.GetType().Name}");
        return null;
    }
    catch (UnauthorizedAccessException ex)
    {
        KpLog.Warn($"Failed to read legacy Kelpie state file. path={statePath}, reason={ex.GetType().Name}");
        return null;
    }
}

static void SaveClientState(KelpieClientState state)
{
    TryMigrateLegacyClientState();
    var statePath = GetClientStatePath();
    var stateDirectory = Path.GetDirectoryName(statePath);
    if (!string.IsNullOrWhiteSpace(stateDirectory))
    {
        Directory.CreateDirectory(stateDirectory);
    }

    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
    };
    File.WriteAllText(statePath, JsonSerializer.Serialize(state, options));
}

public sealed record KelpieClientState(string? OpenProfile = null);

sealed record ProfileCreateCommandOptions(
    bool Silent,
    bool NoBackup,
    bool DryRun,
    string? HostAddress,
    int? Port,
    string? DefaultUser,
    string? AuthMethod,
    string? PrivateKeyFile,
    string? PasswordSecretName,
    string? OsFamily,
    string? Mode,
    IReadOnlyList<string>? ReadOnlyRoots,
    IReadOnlyList<string>? ReadWriteRoots,
    IReadOnlyList<string>? DenyPatterns,
    IReadOnlyDictionary<string, string>? AllowedRootEntries,
    IReadOnlyDictionary<string, string>? SpecialPathEntries)
{
    public bool HasTemplateOverrides =>
        HostAddress is not null
        || Port is not null
        || DefaultUser is not null
        || AuthMethod is not null
        || PrivateKeyFile is not null
        || PasswordSecretName is not null
        || OsFamily is not null
        || Mode is not null
        || ReadOnlyRoots is not null
        || ReadWriteRoots is not null
        || DenyPatterns is not null
        || AllowedRootEntries is not null
        || SpecialPathEntries is not null;

    public static ProfileCreateCommandOptions Default { get; } = new(
        Silent: false,
        NoBackup: false,
        DryRun: false,
        HostAddress: null,
        Port: null,
        DefaultUser: null,
        AuthMethod: null,
        PrivateKeyFile: null,
        PasswordSecretName: null,
        OsFamily: null,
        Mode: null,
        ReadOnlyRoots: null,
        ReadWriteRoots: null,
        DenyPatterns: null,
        AllowedRootEntries: null,
        SpecialPathEntries: null);
}

sealed record ProfileTarget(string ProfileName, string ProfilePath);

enum ProfileTargetKind
{
    Existing,
    Pending,
    ExistingOrPending,
}

sealed class ProfileTransaction
{
    public ProfileTransaction(string profilePath, string backupPath)
    {
        ProfilePath = profilePath;
        BackupPath = backupPath;
    }

    public string ProfilePath { get; }

    public string BackupPath { get; }

    public void Commit()
    {
        File.Delete(BackupPath);
    }

    public void Rollback()
    {
        if (File.Exists(BackupPath))
        {
            File.Move(BackupPath, ProfilePath, overwrite: true);
        }
    }
}

sealed record CheckItem(string Name, bool IsOk, string? Reason)
{
    public static CheckItem Ok(string name)
    {
        return new CheckItem(name, true, null);
    }

    public static CheckItem Ng(string name, string reason)
    {
        return new CheckItem(name, false, reason);
    }
}

enum PagerMode
{
    Auto,
    Force,
    Disabled,
}

sealed class CheckResultWriter
{
    public bool HasErrors { get; private set; }

    private int _okCount;
    private int _totalCount;

    public void Write(string itemName, bool isOk, string? reason = null)
    {
        Count(isOk);
        if (isOk)
        {
            Console.WriteLine($"{itemName}: OK");
            return;
        }

        HasErrors = true;
        Console.WriteLine($"{itemName}: NG ({reason ?? "check failed"})");
    }

    public void WriteGroup(
        string title,
        IEnumerable<CheckItem> items,
        bool emptyOk = true,
        string emptyReason = "empty list")
    {
        Console.WriteLine(title);

        var itemArray = items.ToArray();
        if (itemArray.Length == 0)
        {
            WriteIndented("(empty list)", emptyOk, emptyReason);
            return;
        }

        foreach (var item in itemArray)
        {
            WriteIndented(item.Name, item.IsOk, item.Reason);
        }
    }

    public void WriteSummary()
    {
        var ngCount = _totalCount - _okCount;
        Console.WriteLine($"Check summary: OK={_okCount}/{_totalCount} NG={ngCount}/{_totalCount}");
    }

    private void WriteIndented(string itemName, bool isOk, string? reason)
    {
        Count(isOk);
        if (isOk)
        {
            Console.WriteLine($"  {itemName}: OK");
            return;
        }

        HasErrors = true;
        Console.WriteLine($"  {itemName}: NG ({reason ?? "check failed"})");
    }

    private void Count(bool isOk)
    {
        _totalCount++;
        if (isOk)
        {
            _okCount++;
        }
    }
}
