using System.Xml.Linq;
using FluentAssertions;

namespace KelpieSSH.Application.Tests.Architecture;

public sealed class ProjectDependencyTests
{
    private static readonly IReadOnlyDictionary<string, string[]> ApprovedDependencies =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Kelpie.Core"] = [],
            ["KelpieClientCommand"] =
            [
                "Kelpie.Core",
                "KelpieServerCommand",
                "KelpieSSH.Application",
                "KelpieSSH.Infrastructure",
            ],
            ["KelpieMCPServer"] =
            [
                "Kelpie.Core",
                "KelpieSSH.Application",
                "KelpieSSH.Infrastructure",
            ],
            ["KelpieServerCommand"] =
            [
                "Kelpie.Core",
                "KelpieSSH.Application",
                "KelpieSSH.Infrastructure",
            ],
            ["KelpieSSH.Application"] = ["KelpieSSH.Domain"],
            ["KelpieSSH.Domain"] = [],
            ["KelpieSSH.Infrastructure"] =
            [
                "Kelpie.Core",
                "KelpieSSH.Application",
            ],
            ["KelpieWebPermissionHelper"] = [],
        };

    [Fact]
    public void ProductionProjects_ShouldMatchApprovedDependencyMap()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectFiles = Directory.GetFiles(
            Path.Combine(repositoryRoot, "src"),
            "*.csproj",
            SearchOption.AllDirectories);
        var actualDependencies = projectFiles.ToDictionary(
            projectFile => Path.GetFileNameWithoutExtension(projectFile),
            ReadProjectReferences,
            StringComparer.OrdinalIgnoreCase);

        actualDependencies.Keys.Should().BeEquivalentTo(
            ApprovedDependencies.Keys,
            "every production project must have an explicitly reviewed dependency rule");

        foreach (var project in ApprovedDependencies)
        {
            actualDependencies[project.Key].Should().BeEquivalentTo(
                project.Value,
                $"{project.Key} must only reference its approved inward or composition dependencies");
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

        throw new DirectoryNotFoundException("KelpieSSH repository root was not found.");
    }

    private static string[] ReadProjectReferences(string projectFile)
    {
        var document = XDocument.Load(projectFile);
        return document
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => Path.GetFileNameWithoutExtension(reference!))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
