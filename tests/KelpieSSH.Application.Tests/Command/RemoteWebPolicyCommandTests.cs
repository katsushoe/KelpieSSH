using FluentAssertions;
using KelpieServerCommand;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Command;

public sealed class RemoteWebPolicyCommandTests
{
    [Fact]
    public async Task RunAsync_Apply_ShouldSendOneManifestWithOneConfirmation()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{\"sites\":{\"/var/www\":{\"changes\":[{\"operation\":\"add\",\"path\":\"/a.html\",\"access\":\"Create\"}]}}}");
            var interaction = new ConfirmingInteraction();
            var executor = new FakeExecutor(
                Result(0, "{\"current\":\"{}\\n\",\"proposed\":\"{}\\n\",\"currentSha256\":\"" + new string('a', 64) + "\",\"backupName\":null}"),
                Result(0, "{\"changed\":true,\"changeCount\":1}\n"));

            var exitCode = await RemoteWebPolicyCommand.RunAsync(
                ["apply", "sample", path], interaction, executor, profileOverride: CreateProfile());

            exitCode.Should().Be(0);
            interaction.ReadCount.Should().Be(1);
            executor.Calls.Select(call => call.Action).Should().Equal("preview-apply", "apply-manifest");
            executor.Calls[1].Arguments.Should().HaveCount(2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_Add_ShouldRequireProfileAndApplyAfterInteractiveConfirmation()
    {
        var interaction = new ConfirmingInteraction();
        var executor = new FakeExecutor(
            Result(0, "{\"current\":\"{\\\"Sites\\\":{}}\\n\",\"proposed\":\"{\\\"Sites\\\":{}}\\n\",\"currentSha256\":\"" + new string('a', 64) + "\",\"backupName\":null}"),
            Result(0, "{\"changed\":true}\n"));

        var exitCode = await RemoteWebPolicyCommand.RunAsync(
            ["add", "sample", "/var/www", "/contact.php", "Update"],
            interaction,
            executor,
            profileOverride: CreateProfile());

        exitCode.Should().Be(0);
        executor.Calls.Select(call => call.Action).Should().Equal("preview-add", "apply-add");
        executor.Calls[0].Arguments.Should().HaveCount(3);
        executor.Calls[1].Arguments.Should().HaveCount(4);
        interaction.OutputText.Should().Contain("--- current").And.Contain("+++ proposed");
    }

    [Fact]
    public async Task RunAsync_List_ShouldReturnRemoteJsonWithoutConfirmation()
    {
        var interaction = new ConfirmingInteraction();
        var executor = new FakeExecutor(Result(0, "{\"Sites\":{}}\n"));

        var exitCode = await RemoteWebPolicyCommand.RunAsync(
            ["list", "sample"],
            interaction,
            executor,
            profileOverride: CreateProfile());

        exitCode.Should().Be(0);
        interaction.OutputText.Should().Contain("\"Sites\"");
        interaction.ReadCount.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_Remove_ShouldRejectNonInteractiveExecution()
    {
        var interaction = new ConfirmingInteraction(isInteractive: false);
        var executor = new FakeExecutor();

        var exitCode = await RemoteWebPolicyCommand.RunAsync(
            ["remove", "sample", "/var/www", "/contact.php"],
            interaction,
            executor,
            profileOverride: CreateProfile());

        exitCode.Should().Be(1);
        interaction.ErrorText.Should().Contain("ERROR: web-policy changes require an interactive human terminal.")
            .And.Contain("No changes were applied.");
        executor.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_Rollback_ShouldRejectConfirmationMismatch()
    {
        var interaction = new ConfirmingInteraction(confirm: false);
        var executor = new FakeExecutor(
            Result(0, "{\"current\":\"{}\\n\",\"proposed\":\"{}\\n\",\"currentSha256\":\"" + new string('a', 64) + "\",\"backupName\":\"backup.json\"}"));

        var exitCode = await RemoteWebPolicyCommand.RunAsync(
            ["rollback", "sample"],
            interaction,
            executor,
            profileOverride: CreateProfile());

        exitCode.Should().Be(1);
        interaction.ErrorText.Should().Contain("ERROR: Confirmation code did not match.")
            .And.Contain("No changes were applied.");
        executor.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task RunAsync_Add_ShouldReportRemotePermissionFailureWithoutSecrets()
    {
        var interaction = new ConfirmingInteraction();
        var executor = new FakeExecutor(Result(1, string.Empty, "sudo: a password is required"));

        var exitCode = await RemoteWebPolicyCommand.RunAsync(
            ["add", "sample", "/var/www", "/contact.php", "Update"],
            interaction,
            executor,
            profileOverride: CreateProfile());

        exitCode.Should().Be(1);
        interaction.ErrorText.Should().Contain("password is required")
            .And.Contain("No changes were applied.")
            .And.NotContain("InvalidOperationException")
            .And.NotContain(" at ");
        interaction.OutputText.ToLowerInvariant().Should().NotContain("secret");
    }

    [Fact]
    public async Task RunAsync_Apply_WhenEntryExists_ShouldReportPathAndNoChangesWithoutStackTrace()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{\"sites\":{\"/var/www\":{\"changes\":[{\"operation\":\"add\",\"path\":\"/products/help/index.html\",\"access\":\"Create\"}]}}}");
            var interaction = new ConfirmingInteraction();
            var executor = new FakeExecutor(
                Result(1, string.Empty, "ERROR: policy entry already exists: /products/help/index.html"));

            var exitCode = await RemoteWebPolicyCommand.RunAsync(
                ["apply", "sample", path], interaction, executor, profileOverride: CreateProfile());

            exitCode.Should().Be(1);
            interaction.ErrorText.Should().Be(
                "ERROR: policy entry already exists: /products/help/index.html" + Environment.NewLine
                + "No changes were applied." + Environment.NewLine);
            interaction.ErrorText.Should().NotContain("InvalidOperationException")
                .And.NotContain(" at ");
            executor.Calls.Should().ContainSingle(call => call.Action == "preview-apply");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_Apply_WhenRemoteApplyFails_ShouldReportNoChanges()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "{\"sites\":{\"/var/www\":{\"changes\":[{\"operation\":\"add\",\"path\":\"/a.html\",\"access\":\"Create\"}]}}}");
            var interaction = new ConfirmingInteraction();
            var executor = new FakeExecutor(
                Result(0, "{\"current\":\"{}\\n\",\"proposed\":\"{}\\n\",\"currentSha256\":\"" + new string('a', 64) + "\",\"backupName\":null}"),
                Result(1, string.Empty, "ERROR: current policy changed after preview"));

            var exitCode = await RemoteWebPolicyCommand.RunAsync(
                ["apply", "sample", path], interaction, executor, profileOverride: CreateProfile());

            exitCode.Should().Be(1);
            interaction.ErrorText.Should().Contain("ERROR: current policy changed after preview")
                .And.Contain("No changes were applied.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_List_WhenRemoteHelperIsOld_ShouldShowUpdateInstructions()
    {
        var interaction = new ConfirmingInteraction();
        var executor = new FakeExecutor(Result(1, string.Empty, "ERROR: unsupported action: policy"));

        var exitCode = await RemoteWebPolicyCommand.RunAsync(
            ["list", "sample", "/var/www"],
            interaction,
            executor,
            profileOverride: CreateProfile());

        exitCode.Should().Be(1);
        interaction.ErrorText.Should().Contain("helper is too old")
            .And.Contain("0.2.1.1 or later")
            .And.NotContain("unsupported action");
    }

    private static SshConnectionProfile CreateProfile()
    {
        return new SshConnectionProfile
        {
            Name = "sample",
            Host = string.Empty,
            UserName = string.Empty,
            OsFamily = string.Empty,
        };
    }

    private static SshCommandResult Result(int exitCode, string output, string error = "")
    {
        return new SshCommandResult(
            "human_remote_web_policy",
            string.Empty,
            exitCode,
            output,
            error,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            TimedOut: false);
    }

    private sealed class FakeExecutor(params SshCommandResult[] results) : IWebPolicyRemoteExecutor
    {
        private readonly Queue<SshCommandResult> _results = new(results);

        public List<(string Action, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public Task<SshCommandResult> ExecuteAsync(
            SshConnectionProfile profile,
            string action,
            IReadOnlyList<string> args,
            CancellationToken cancellationToken)
        {
            Calls.Add((action, args.ToArray()));
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class ConfirmingInteraction(
        bool isInteractive = true,
        bool confirm = true) : IWebPolicyInteraction
    {
        private readonly StringWriter _output = new();
        private readonly StringWriter _error = new();

        public bool IsInteractive { get; } = isInteractive;

        public TextWriter Output => _output;

        public TextWriter Error => _error;

        public string OutputText => _output.ToString();

        public string ErrorText => _error.ToString();

        public int ReadCount { get; private set; }

        public string? ReadLine()
        {
            ReadCount++;
            if (!confirm)
            {
                return "WRONG";
            }

            var marker = "Type ";
            var start = _output.ToString().LastIndexOf(marker, StringComparison.Ordinal) + marker.Length;
            return _output.ToString().Substring(start, 8);
        }
    }
}
