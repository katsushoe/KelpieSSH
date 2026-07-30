using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;

namespace KelpieSSH.Application.Tests.Command;

public sealed class KelpieServerPackagingTests
{
    [Fact]
    public async Task Publish_WhenRunFromIsolatedDirectory_ShouldIncludeSshDependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "kelpiemcp-package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var projectPath = Path.Combine(
                repositoryRoot,
                "src",
                "KelpieServerCommand",
                "KelpieServerCommand.csproj");
            var publish = await RunAsync(
                repositoryRoot,
                "dotnet",
                ["publish", projectPath, "-c", "Release", "--no-restore", "-o", outputDirectory]);
            publish.ExitCode.Should().Be(0, publish.StandardError);

            File.Exists(Path.Combine(outputDirectory, "Renci.SshNet.dll")).Should().BeTrue();
            var dependencyManifest = await File.ReadAllTextAsync(
                Path.Combine(outputDirectory, "kelpiemcp.deps.json"));
            using var document = JsonDocument.Parse(dependencyManifest);
            dependencyManifest.Should().Contain("Renci.SshNet");

            var executable = Path.Combine(outputDirectory, "kelpiemcp.exe");
            var version = await RunAsync(outputDirectory, executable, ["--version"]);
            version.ExitCode.Should().Be(0, version.StandardError);
            version.StandardOutput.Should().Contain("kelpiemcp 0.3.12.1");
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "KelpieSSH.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static async Task<ProcessResult> RunAsync(
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new ProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
