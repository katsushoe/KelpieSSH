using FluentAssertions;
using KelpieServerCommand;
using KelpieSSH.Application.Ssh;

namespace KelpieSSH.Application.Tests.Command;

public sealed class HelperUpdateCommandTests
{
    [Fact]
    public async Task RunAsync_WhenConfirmed_ShouldUploadPreviewAndApply()
    {
        var artifact = CreateArtifact();
        var interaction = new TestInteraction();
        var remote = new FakeRemote();
        try
        {
            var exitCode = await HelperUpdateCommand.RunAsync(
                ["update", "sample", artifact],
                interaction,
                remote,
                profileOverride: CreateProfile());

            exitCode.Should().Be(0);
            remote.Calls.Should().Equal("upload", "preview", "apply");
            interaction.OutputText.Should().Contain("Current:")
                .And.Contain("Proposed SHA-256:")
                .And.Contain("\"success\":true")
                .And.Contain("\"changed\":true")
                .And.Contain("\"message\":\"Helper update completed.\"");
        }
        finally
        {
            File.Delete(artifact);
        }
    }

    [Fact]
    public async Task RunAsync_WhenHashesMatch_ShouldSucceedWithoutConfirmationOrApply()
    {
        var artifact = CreateArtifact();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(artifact))).ToLowerInvariant();
        var interaction = new TestInteraction(confirm: false);
        var remote = new FakeRemote(currentHash: hash);
        try
        {
            var exitCode = await HelperUpdateCommand.RunAsync(
                ["update", "sample", artifact],
                interaction,
                remote,
                profileOverride: CreateProfile());

            exitCode.Should().Be(0);
            remote.Calls.Should().Equal("upload", "preview");
            interaction.OutputText.Should().Contain("\"success\":true")
                .And.Contain("\"changed\":false")
                .And.NotContain("Type ");
        }
        finally
        {
            File.Delete(artifact);
        }
    }

    [Fact]
    public async Task RunAsync_WhenNonInteractive_ShouldRejectBeforeUpload()
    {
        var artifact = CreateArtifact();
        var remote = new FakeRemote();
        try
        {
            var interaction = new TestInteraction(isInteractive: false);
            var exitCode = await HelperUpdateCommand.RunAsync(
                ["update", "sample", artifact],
                interaction,
                remote,
                profileOverride: CreateProfile());

            exitCode.Should().Be(1);
            interaction.ErrorText.Should().Contain("interactive human terminal");
            remote.Calls.Should().BeEmpty();
        }
        finally
        {
            File.Delete(artifact);
        }
    }

    [Fact]
    public async Task RunAsync_WhenUploadedHashDiffers_ShouldRejectBeforeConfirmation()
    {
        var artifact = CreateArtifact();
        var remote = new FakeRemote(stagedHashMatches: false);
        try
        {
            var interaction = new TestInteraction();
            var exitCode = await HelperUpdateCommand.RunAsync(
                ["update", "sample", artifact],
                interaction,
                remote,
                profileOverride: CreateProfile());

            exitCode.Should().Be(1);
            interaction.ErrorText.Should().Contain("SHA-256 did not match");
            remote.Calls.Should().Equal("upload", "preview");
        }
        finally
        {
            File.Delete(artifact);
        }
    }

    [Fact]
    public async Task RunAsync_WhenConfirmationDiffers_ShouldNotApply()
    {
        var artifact = CreateArtifact();
        var remote = new FakeRemote();
        try
        {
            var interaction = new TestInteraction(confirm: false);
            var exitCode = await HelperUpdateCommand.RunAsync(
                ["update", "sample", artifact],
                interaction,
                remote,
                profileOverride: CreateProfile());

            exitCode.Should().Be(1);
            interaction.ErrorText.Should().Contain("ERROR: Confirmation code did not match.")
                .And.Contain("No changes were applied.");
            remote.Calls.Should().Equal("upload", "preview");
        }
        finally
        {
            File.Delete(artifact);
        }
    }

    [Fact]
    public void BuildPrivilegedCommand_ShouldUseOnlyInternalEncodedRootTransaction()
    {
        var hash = new string('a', 64);

        var command = SshHelperUpdateRemote.BuildPrivilegedCommand(hash);

        command.Should().StartWith("sudo -n sh -c \"printf %s '")
            .And.Contain("| base64 -d | sh -s -- '" + hash + "'\"")
            .And.NotContain("/usr/bin/cp")
            .And.NotContain("/usr/bin/mv")
            .And.NotContain("local-artifact");
    }

    private static string CreateArtifact()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        return path;
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

    private sealed class FakeRemote(bool stagedHashMatches = true, string? currentHash = null) : IHelperUpdateRemote
    {
        public List<string> Calls { get; } = [];

        public Task UploadAsync(
            SshConnectionProfile profile,
            string localPath,
            CancellationToken cancellationToken)
        {
            Calls.Add("upload");
            return Task.CompletedTask;
        }

        public Task<HelperUpdatePreview> PreviewAsync(
            SshConnectionProfile profile,
            string expectedHash,
            CancellationToken cancellationToken)
        {
            Calls.Add("preview");
            return Task.FromResult(new HelperUpdatePreview(
                "helper 0.1.0.4",
                currentHash ?? new string('a', 64),
                stagedHashMatches));
        }

        public Task<HelperUpdateResult> ApplyAsync(
            SshConnectionProfile profile,
            string expectedHash,
            CancellationToken cancellationToken)
        {
            Calls.Add("apply");
            return Task.FromResult(new HelperUpdateResult(true, "Helper update completed."));
        }
    }

    private sealed class TestInteraction(
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

        public string? ReadLine()
        {
            if (!confirm)
            {
                return "WRONG";
            }

            var text = _output.ToString();
            var start = text.LastIndexOf("Type ", StringComparison.Ordinal) + 5;
            return text.Substring(start, 4).ToLowerInvariant();
        }
    }
}
