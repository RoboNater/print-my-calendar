using YahooMonthPrint.Core;

namespace YahooMonthPrint.Core.Tests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void CoreAssemblyHasNoProjectLayerDependencies()
    {
        var referencedAssemblies = typeof(CoreAssemblyMarker).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("PresentationFramework", referencedAssemblies);
        Assert.DoesNotContain("YahooMonthPrint.App", referencedAssemblies);
        Assert.DoesNotContain("YahooMonthPrint.Printing", referencedAssemblies);
        Assert.DoesNotContain("YahooMonthPrint.YahooCalDav", referencedAssemblies);
    }
}
