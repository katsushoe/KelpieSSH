using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Text.Json;
using KelpieSSH.Application.Tests.Ssh;

[assembly: SupportedOSPlatform("windows")]

if (args.Length is < 1 or > 2 || (args.Length == 2 && args[1] != "--allow-current-identity"))
{
    Console.Error.WriteLine("Usage: KelpieSSH.ServiceIdentityProbe <results-directory> [--allow-current-identity]");
    return 1;
}

var directory = Path.GetFullPath(args[0]);
var report = new ProbeReport { IsSystem = WindowsIdentity.GetCurrent().IsSystem };
try
{
    Directory.CreateDirectory(directory);
    AppDomain.CurrentDomain.UnhandledException += (_, error) => Console.Error.WriteLine(error.ExceptionObject);
    TaskScheduler.UnobservedTaskException += (_, error) => Console.Error.WriteLine(error.Exception);
    SaveState("Starting");
    if (!report.IsSystem && args.Length != 2)
    {
        throw new InvalidOperationException("The scheduled task did not run as LocalSystem.");
    }

    var tests = new TrustStoreOperationalTests();
    (string Name, Action Run)[] cases =
    [
        ("MissingKey", tests.MissingKey_ShouldRejectLoadAndSaveWithoutReplacingStore),
        ("InvalidKeyLength", () => tests.InvalidOrUnrelatedKey_ShouldRejectLoadAndSaveWithoutReplacingFiles(16)),
        ("UnrelatedKey", () => tests.InvalidOrUnrelatedKey_ShouldRejectLoadAndSaveWithoutReplacingFiles(32)),
        ("BackupRestore", tests.BackupPair_ShouldRestoreOriginalTrustWithRestrictedKeyAcl),
        ("AccessDenied", tests.WindowsKeyAccessDenied_ShouldRejectLoadAndSaveWithoutReplacingStore)
    ];
    using var log = new StreamWriter(Path.Combine(directory, "test.log")) { AutoFlush = true };
    foreach (var item in cases)
    {
        SaveState(item.Name);
        var elapsed = Stopwatch.StartNew();
        var outcome = "Passed";
        try
        {
            item.Run();
            report.Passed++;
        }
        catch (Exception error)
        {
            outcome = "Failed";
            report.Failed++;
            log.WriteLine(error);
        }
        report.Cases.Add(new ProbeCase(item.Name, outcome, elapsed.Elapsed.TotalSeconds));
        log.WriteLine($"{item.Name}: {outcome}");
    }
    report.ExitCode = report.Failed == 0 ? 0 : 1;
    if (report.Failed != 0)
    {
        report.Error = $"{report.Failed} operational case(s) failed. Inspect test.log.";
    }
}
catch (Exception error)
{
    report.ExitCode = 1;
    report.Error = error.ToString();
    Console.Error.WriteLine(error);
}
finally
{
    try
    {
        SaveState("Finished");
        WriteJson("result.json");
    }
    catch (Exception error)
    {
        report.ExitCode = 1;
        Console.Error.WriteLine($"Could not persist probe results: {error}");
    }
}
return report.ExitCode;

void SaveState(string stage)
{
    report.Stage = stage;
    report.UpdatedUtc = DateTimeOffset.UtcNow;
    WriteJson("worker-state.json");
}

void WriteJson(string name)
{
    var path = Path.Combine(directory, name);
    var temporary = path + ".tmp";
    using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
    {
        JsonSerializer.Serialize(stream, report);
        stream.Flush(flushToDisk: true);
    }
    File.Move(temporary, path, overwrite: true);
}

internal sealed class ProbeReport
{
    public bool IsSystem { get; init; }
    public int ProcessId { get; } = Environment.ProcessId;
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; }
    public string Stage { get; set; } = "Starting";
    public int ExitCode { get; set; } = 1;
    public int Passed { get; set; }
    public int Failed { get; set; }
    public string? Error { get; set; }
    public List<ProbeCase> Cases { get; } = [];
}

internal sealed record ProbeCase(string Name, string Outcome, double DurationSeconds);
