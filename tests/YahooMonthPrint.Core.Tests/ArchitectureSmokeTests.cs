using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace YahooMonthPrint.Core.Tests;

public sealed class ArchitectureSmokeTests
{
    private static readonly string[] ForbiddenCoreNamespaceRoots = ["System.Net", "System.Windows"];

    [Fact]
    public void ProductionProjectDependenciesMatchAllowedGraph()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var projectPaths = Directory
            .GetFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] expectedProjects =
        [
            "src/YahooMonthPrint.App/YahooMonthPrint.App.csproj",
            "src/YahooMonthPrint.Core/YahooMonthPrint.Core.csproj",
            "src/YahooMonthPrint.Printing/YahooMonthPrint.Printing.csproj",
            "src/YahooMonthPrint.YahooCalDav/YahooMonthPrint.YahooCalDav.csproj",
        ];
        Assert.Equal(expectedProjects, projectPaths);

        var coreProjectPath = Path.Combine(
            repositoryRoot,
            "src/YahooMonthPrint.Core/YahooMonthPrint.Core.csproj");
        var corePackageReferences = XDocument
            .Load(coreProjectPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .ToArray();
        Assert.Empty(corePackageReferences);

        var actualEdges = projectPaths
            .SelectMany(projectPath => ReadProjectReferenceEdges(repositoryRoot, projectPath))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expectedEdges =
        [
            "src/YahooMonthPrint.App/YahooMonthPrint.App.csproj -> src/YahooMonthPrint.Core/YahooMonthPrint.Core.csproj",
            "src/YahooMonthPrint.App/YahooMonthPrint.App.csproj -> src/YahooMonthPrint.Printing/YahooMonthPrint.Printing.csproj",
            "src/YahooMonthPrint.App/YahooMonthPrint.App.csproj -> src/YahooMonthPrint.YahooCalDav/YahooMonthPrint.YahooCalDav.csproj",
            "src/YahooMonthPrint.Printing/YahooMonthPrint.Printing.csproj -> src/YahooMonthPrint.Core/YahooMonthPrint.Core.csproj",
            "src/YahooMonthPrint.YahooCalDav/YahooMonthPrint.YahooCalDav.csproj -> src/YahooMonthPrint.Core/YahooMonthPrint.Core.csproj",
        ];

        Assert.Equal(expectedEdges, actualEdges);
    }

    [Fact]
    public void CoreMetadataHasNoNetworkOrWpfTypeReferences()
    {
        using var assemblyStream = File.OpenRead(typeof(CoreAssemblyMarker).Assembly.Location);
        using var peReader = new PEReader(assemblyStream);
        var metadataReader = peReader.GetMetadataReader();

        var forbiddenReferences = metadataReader.TypeReferences
            .Select(metadataReader.GetTypeReference)
            .Select(reference =>
                $"{metadataReader.GetString(reference.Namespace)}.{metadataReader.GetString(reference.Name)}")
            .Where(typeName => ForbiddenCoreNamespaceRoots.Any(root =>
                typeName.Equals(root, StringComparison.Ordinal)
                || typeName.StartsWith($"{root}.", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }

    private static IEnumerable<string> ReadProjectReferenceEdges(
        string repositoryRoot,
        string projectRelativePath)
    {
        var projectPath = Path.Combine(repositoryRoot, projectRelativePath);
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var project = XDocument.Load(projectPath);

        return project
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include!)))
            .Select(referencePath => Path.GetRelativePath(repositoryRoot, referencePath).Replace('\\', '/'))
            .Select(referencePath => $"{projectRelativePath} -> {referencePath}");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "YahooMonthPrint.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
