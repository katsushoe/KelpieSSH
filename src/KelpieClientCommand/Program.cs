using System.Reflection;
using System.Text;
using System.Text.Json;
using KelpieServerCommand;
using Kelpie.Core;
using KelpieSSH.Application.Ssh;
using KelpieSSH.Infrastructure.Ssh;
using Microsoft.Extensions.Configuration;
using Renci.SshNet.Common;

KpLogSetup.Configure(
    AppContext.BaseDirectory,
    "kelpie.log",
    KelpieRuntimePaths.KelpieConfigFileName,
    "kelpie");
KpLog.Info("Kelpie CLI starting.");

var command = args.Length > 0 ? args[0] : string.Empty;
WarnLegacyEditorConfigIfNeeded();

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

if (string.Equals(command, "gui", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI gui mode requested.");
    SaveClientMode("gui");
    StartDesktop(openProfileName: null);
    Console.WriteLine("Kelpie mode: gui");
    return;
}

if (string.Equals(command, "cli", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI cli mode requested.");
    SaveClientMode("cli");
    Console.WriteLine("Kelpie mode: cli");
    return;
}

if (string.Equals(command, "login", StringComparison.OrdinalIgnoreCase))
{
    KpLog.Info("Kelpie CLI login requested.");
    var loginOption = args.Length > 1 ? args[1] : string.Empty;
    var openInConsole = string.Equals(loginOption, "--console", StringComparison.OrdinalIgnoreCase);
    var openInDesktop = string.Equals(loginOption, "--desktop", StringComparison.OrdinalIgnoreCase);
    var forceConsoleSession = string.Equals(loginOption, "--console-session", StringComparison.OrdinalIgnoreCase);

    if (string.Equals(loginOption, "--window", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("`kelpie login --window` was renamed to `kelpie login --console`.");
        Environment.ExitCode = 1;
        return;
    }

    if (args.Length > 2
        || (args.Length > 1 && !openInConsole && !openInDesktop && !forceConsoleSession))
    {
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

    if (openInDesktop || (!forceConsoleSession && IsGuiMode()))
    {
        StartDesktop(openProfileName);
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
        var createProfileName = args.Length > 2 ? args[2] : string.Empty;
        if (IsHelpCommand(createProfileName))
        {
            WriteProfileCreateUsage(Console.Out);
            return;
        }

        if (args.Length != 3)
        {
            WriteProfileCreateUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        CreateProfile(createProfileName);
        return;
    }

    if (string.Equals(subcommand, "edit", StringComparison.OrdinalIgnoreCase))
    {
        RunProfileEdit(args);
        return;
    }

    if (string.Equals(subcommand, "commit", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 3)
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        CommitProfile(args[2]);
        return;
    }

    if (string.Equals(subcommand, "rollback", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length != 3)
        {
            WriteProfileUsage(Console.Error);
            Environment.ExitCode = 1;
            return;
        }

        RollbackProfile(args[2]);
        return;
    }

    if (!string.Equals(subcommand, "show", StringComparison.OrdinalIgnoreCase))
    {
        WriteProfileUsage(Console.Error);
        Environment.ExitCode = 1;
        return;
    }

    var profileName = args.Length > 2 ? args[2] : string.Empty;
    ShowProfile(LoadProfileCatalog(), profileName);
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

if (string.Equals(command, "logs", StringComparison.OrdinalIgnoreCase))
{
    var profileName = args.Length > 1 ? args[1] : string.Empty;
    var service = args.Length > 2 ? args[2] : string.Empty;
    var lines = args.Length > 3 ? args[3] : "100";
    KpLog.Info($"Kelpie CLI logs requested. profile={profileName}, service={service}, lines={lines}");
    await RunLogsAsync(LoadProfileCatalog(), profileName, service, lines);
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

    return new SshConnectionProfileCatalog(
        SshConnectionProfileFileLoader.LoadDirectory(profilesDirectory));
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

static void WriteProfileUsage(TextWriter writer)
{
    writer.WriteLine("Usage:");
    writer.WriteLine("  kelpie profile create <profile>");
    writer.WriteLine("  kelpie profile edit <profile>");
    writer.WriteLine("  kelpie profile edit <profile> set <dotPath> <value>");
    writer.WriteLine("  kelpie profile edit <profile> add-root <path> <access>");
    writer.WriteLine("  kelpie profile edit <profile> rm-root <path>");
    writer.WriteLine("  kelpie profile edit <profile> add-deny <pattern>");
    writer.WriteLine("  kelpie profile edit <profile> rm-deny <pattern>");
    writer.WriteLine("  kelpie profile commit <profile>");
    writer.WriteLine("  kelpie profile rollback <profile>");
    writer.WriteLine("  kelpie profile show <profile>");
}

static void WriteProfileCreateUsage(TextWriter writer)
{
    writer.WriteLine("Usage:");
    writer.WriteLine("  kelpie profile create <profile>");
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
    writer.WriteLine("  kelpie gui");
    writer.WriteLine("  kelpie cli");
    writer.WriteLine("  kelpie login");
    writer.WriteLine("  kelpie login --console");
    writer.WriteLine("  kelpie login --desktop");
    writer.WriteLine("  kelpie logout");
    writer.WriteLine("  kelpie profiles");
    writer.WriteLine("  kelpie sessions");
    writer.WriteLine("  kelpie kill <handle>");
    writer.WriteLine("  kelpie profile create <profile>");
    writer.WriteLine("  kelpie profile edit <profile>");
    writer.WriteLine("  kelpie profile edit <profile> set <dotPath> <value>");
    writer.WriteLine("  kelpie profile edit <profile> add-root <path> <access>");
    writer.WriteLine("  kelpie profile edit <profile> rm-root <path>");
    writer.WriteLine("  kelpie profile edit <profile> add-deny <pattern>");
    writer.WriteLine("  kelpie profile edit <profile> rm-deny <pattern>");
    writer.WriteLine("  kelpie profile commit <profile>");
    writer.WriteLine("  kelpie profile rollback <profile>");
    writer.WriteLine("  kelpie profile show <profile>");
    writer.WriteLine("  kelpie status <profile>");
    writer.WriteLine("  kelpie diag <profile>");
    writer.WriteLine("  kelpie logs <profile> <service> [lines]");
    writer.WriteLine("  kelpie env keys <profile>");
    writer.WriteLine("  kelpie env peek <profile> <key>");
    writer.WriteLine("  kelpie env set <profile> <key> <value> -- <command>");
    writer.WriteLine("  kelpie env list <profile>");
    writer.WriteLine("  kelpie env persist <profile> <key> <value>");
    writer.WriteLine("  kelpie env remove <profile> <key>");
    writer.WriteLine("  kelpie version");
    writer.WriteLine("  kelpie help");
    writer.WriteLine();
    writer.WriteLine("Options:");
    writer.WriteLine("  --version, -v  Show version information.");
    writer.WriteLine("  --help, -h     Show command help.");
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
        var mcpConfigPath = Path.Combine(homeDirectory, "config", KelpieRuntimePaths.KelpieMcpConfigFileName);
        var profilePath = KelpieHomeInitializer.GetProfilePath(homeDirectory, profileName);
        var mcpConfigOptions = !silent && !File.Exists(mcpConfigPath)
            ? ReadMcpConfigTemplateOptions(Path.Combine(homeDirectory, "logs"))
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

static void CreateProfile(string profileName)
{
    if (string.IsNullOrWhiteSpace(profileName))
    {
        WriteProfileCreateUsage(Console.Error);
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
        if (overwrite && !ReadYesNoDefaultYes($"Profile already exists: {profileName}. Overwrite? [Y/n]: "))
        {
            Console.WriteLine("Profile create was canceled.");
            return;
        }

        if (!overwrite)
        {
            KelpieHomeInitializer.GetCreatableProfilePath(homeDirectory, profileName);
        }

        var templateOptions = ReadProfileTemplateOptions(profileName);
        ProfileTransaction? transaction = null;
        try
        {
            transaction = overwrite ? BeginProfileTransaction(profilePath) : null;
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

static void CommitProfile(string profileName)
{
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

static void RollbackProfile(string profileName)
{
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

static void RunProfileEdit(string[] args)
{
    if (args.Length < 3)
    {
        WriteProfileEditUsage();
        Environment.ExitCode = 1;
        return;
    }

    var profileName = args[2];
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

    var editService = new SshProfileEditService(new ProcessEditorLauncher());

    if (args.Length == 3)
    {
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
            () => editService.EditWithEditor(profilePath, editorCommand, ReadProfileEditRecoveryAction));
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

        RunTransactionalProfileEdit(profileName, profilePath, () => editService.SetScalar(profilePath, args[4], args[5]));
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

        RunTransactionalProfileEdit(profileName, profilePath, () => editService.AddRoot(profilePath, args[4], args[5]));
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

        RunTransactionalProfileEdit(profileName, profilePath, () => editService.RemoveRoot(profilePath, args[4]));
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

        RunTransactionalProfileEdit(profileName, profilePath, () => editService.AddDeny(profilePath, args[4]));
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

        RunTransactionalProfileEdit(profileName, profilePath, () => editService.RemoveDeny(profilePath, args[4]));
        return;
    }

    WriteProfileEditUsage();
    Environment.ExitCode = 1;
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

static void WritePendingProfileTransactionError(string profileName, string profilePath)
{
    var backupPath = GetProfileBackupPath(profilePath);
    Console.Error.WriteLine($"Pending profile backup exists: {backupPath}");
    Console.Error.WriteLine($"Run `kelpie profile commit {profileName}` or `kelpie profile rollback {profileName}` first.");
}

static void RunTransactionalProfileEdit(string profileName, string profilePath, Func<ProfileEditResult> edit)
{
    ProfileTransaction? transaction = null;
    try
    {
        transaction = BeginProfileTransaction(profilePath);
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

static void WriteProfileEditResult(string profileName, ProfileEditResult result, ProfileTransaction transaction)
{
    if (!result.Success)
    {
        transaction.Rollback();
        Console.Error.WriteLine(result.ErrorMessage);
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Updated profile: {profileName}");
    Console.WriteLine($"Profile file: {Path.GetFullPath(result.ProfilePath)}");
    FinishProfileTransaction(profileName, transaction);
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
    Console.Error.WriteLine("  kelpie profile edit <profile>");
    Console.Error.WriteLine("  kelpie profile edit <profile> set <dotPath> <value>");
    Console.Error.WriteLine("  kelpie profile edit <profile> add-root <path> <access>");
    Console.Error.WriteLine("  kelpie profile edit <profile> rm-root <path>");
    Console.Error.WriteLine("  kelpie profile edit <profile> add-deny <pattern>");
    Console.Error.WriteLine("  kelpie profile edit <profile> rm-deny <pattern>");
    Console.Error.WriteLine("  kelpie profile commit <profile>");
    Console.Error.WriteLine("  kelpie profile rollback <profile>");
}

static KelpieMcpConfigTemplateOptions ReadMcpConfigTemplateOptions(string defaultLogDirectory)
{
    var defaults = KelpieMcpConfigTemplateOptions.CreateDefault(defaultLogDirectory);

    Console.WriteLine("Create MCP server configuration.");
    Console.WriteLine("Press Enter to use the default value.");

    var logDirectory = ReadPrompt("MCP log directory", defaults.LogDirectory);
    var port = ReadPortPrompt("MCP server port", defaults.Port);
    var controlPipeName = ReadPrompt("MCP control pipe name", defaults.ControlPipeName);

    return new KelpieMcpConfigTemplateOptions(
        LogDirectory: logDirectory,
        Port: port,
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
    var values = new List<string>();
    Console.Write($"{title} [Return to skip]: ");
    var firstValue = Console.ReadLine();
    if (firstValue is null)
    {
        return [];
    }

    var trimmedFirstValue = firstValue.Trim();
    if (string.IsNullOrWhiteSpace(trimmedFirstValue))
    {
        return [];
    }

    if (string.Equals(trimmedFirstValue, "-", StringComparison.Ordinal))
    {
        return [];
    }

    values.Add(trimmedFirstValue);

    while (true)
    {
        Console.Write($"{title} [Return to skip]: ");
        var value = Console.ReadLine();
        if (value is null)
        {
            break;
        }

        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            break;
        }

        if (string.Equals(trimmed, "-", StringComparison.Ordinal))
        {
            return [];
        }

        values.Add(trimmed);
    }

    return values;
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

static void StartDesktop(string? openProfileName)
{
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("Kelpie GUI is supported on Windows only.");
        Environment.ExitCode = 1;
        return;
    }

    var desktopPath = ResolveDesktopPath();
    var startInfo = new System.Diagnostics.ProcessStartInfo
    {
        FileName = desktopPath,
        UseShellExecute = false,
        CreateNoWindow = false,
    };

    if (!string.IsNullOrWhiteSpace(openProfileName))
    {
        startInfo.ArgumentList.Add("--open");
        startInfo.ArgumentList.Add(openProfileName);
    }

    using var process = System.Diagnostics.Process.Start(startInfo);
    if (process is null)
    {
        throw new InvalidOperationException("Failed to start KelpieDesktop.");
    }

    KpLog.Info($"KelpieDesktop started. profile={openProfileName ?? "(none)"}");
    Console.WriteLine(string.IsNullOrWhiteSpace(openProfileName)
        ? "Kelpie GUI started."
        : $"Kelpie GUI started: {openProfileName}");
}

static string ResolveDesktopPath()
{
    foreach (var candidatePath in GetDesktopPathCandidates())
    {
        if (File.Exists(candidatePath))
        {
            return candidatePath;
        }
    }

    throw new FileNotFoundException("KelpieDesktop executable was not found.");
}

static IEnumerable<string> GetDesktopPathCandidates()
{
    var baseDirectory = AppContext.BaseDirectory;
    yield return Path.Combine(baseDirectory, "desktop", "KelpieDesktop.exe");
    yield return Path.Combine(baseDirectory, "KelpieDesktop.exe");

    var sourceRoot = GetSourceRoot(baseDirectory);
    if (sourceRoot is null)
    {
        yield break;
    }

    yield return Path.Combine(sourceRoot, "KelpieDesktop", "bin", "Debug", "net8.0-windows", "KelpieDesktop.exe");
}

static string? GetSourceRoot(string baseDirectory)
{
    var directory = new DirectoryInfo(baseDirectory);
    while (directory is not null)
    {
        if (string.Equals(directory.Name, "src", StringComparison.OrdinalIgnoreCase))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return null;
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

    if (string.Equals(subcommand, "set", StringComparison.OrdinalIgnoreCase))
    {
        if (args.Length < 7)
        {
            WriteEnvironmentUsage();
            Environment.ExitCode = 1;
            return;
        }

        var separatorIndex = Array.IndexOf(args, "--", 5);
        if (separatorIndex < 0 || separatorIndex == args.Length - 1)
        {
            WriteEnvironmentUsage();
            Environment.ExitCode = 1;
            return;
        }

        var profileName = args[2];
        var key = args[3];
        var value = args[4];
        var commandText = string.Join(' ', args.Skip(separatorIndex + 1));
        KpLog.Info($"Kelpie CLI env set requested. profile={profileName}, key={key}");
        if (!TryResolveProfile(catalog, profileName, out var profile))
        {
            Environment.ExitCode = 1;
            return;
        }

        await ExecuteEnvironmentAndPrintAsync(
            CreateSshCommandService(profile),
            service => service.SetEnvironmentValueAsync(profile, key, value, commandText));
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
    Console.Error.WriteLine("  kelpie env set <profile> <key> <value> -- <command>");
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
    catch (InvalidOperationException ex)
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

static string GetClientModeStatePath()
{
    return KelpieRuntimePaths.GetConfigFilePath(AppContext.BaseDirectory, "kelpie-client-mode.txt");
}

static string GetStormStatePath()
{
    return Path.Combine(KelpieRuntimePaths.GetDataDirectory(AppContext.BaseDirectory), "storm_state.dat");
}

static string? LoadOpenProfileName()
{
    return LoadClientState().OpenProfile;
}

static bool IsGuiMode()
{
    return string.Equals(LoadClientState().ClientMode, "gui", StringComparison.OrdinalIgnoreCase);
}

static void SaveOpenProfileName(string profileName)
{
    var state = LoadClientState() with { OpenProfile = profileName };
    SaveClientState(state);
}

static void SaveClientMode(string mode)
{
    var state = LoadClientState() with { ClientMode = mode };
    SaveClientState(state);
}

static KelpieClientState LoadClientState()
{
    var statePath = GetStormStatePath();
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

static KelpieClientState LoadLegacyClientState()
{
    return new KelpieClientState(
        ReadLegacyStateValue(GetOpenProfileStatePath()),
        ReadLegacyStateValue(GetClientModeStatePath()));
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
    var statePath = GetStormStatePath();
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

public sealed record KelpieClientState(
    string? OpenProfile = null,
    string? ClientMode = null);

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
