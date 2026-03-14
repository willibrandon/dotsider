using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class NuGetPackageAnalyzerTests(SampleAssemblyFixture samples)
{
    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasCorrectPackageId()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.Equal("RichLibrary", pkg.PackageId);
    }

    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasCorrectVersion()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.Equal("2.5.1", pkg.PackageVersion);
    }

    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasFiles()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.NotEmpty(pkg.Files);
    }

    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasDllFiles()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.NotEmpty(pkg.DllFiles);
        Assert.All(pkg.DllFiles, f => Assert.True(f.IsDll));
    }

    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasNuspecFile()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.Contains(pkg.Files, f => f.Name.EndsWith(".nuspec"));
    }

    [Fact(Timeout = 30_000)]
    public void OpenDll_ReturnsWorkingAnalyzer()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        var dll = pkg.DllFiles[0];
        using var analyzer = pkg.OpenDll(dll);
        Assert.NotNull(analyzer);
        Assert.True(analyzer.HasMetadata);
        Assert.Equal("RichLibrary", analyzer.AssemblyName);
    }

    [Fact(Timeout = 30_000)]
    public void OpenDll_MatchesStandaloneAssembly()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        var dll = pkg.DllFiles[0];
        using var fromPkg = pkg.OpenDll(dll);
        using var standalone = new AssemblyAnalyzer(samples.RichLibraryDll);
        // Both should have the same types
        Assert.Equal(standalone.TypeDefs.Count, fromPkg.TypeDefs.Count);
    }

    [Fact(Timeout = 30_000)]
    public void HasAuthorsAndDescription()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.NotNull(pkg.Authors);
        Assert.NotNull(pkg.Description);
    }

    [Fact(Timeout = 30_000)]
    public void Dispose_IsIdempotent()
    {
        var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        pkg.Dispose();
        pkg.Dispose(); // should not throw
    }

    [Fact(Timeout = 30_000)]
    public void InvalidPath_Throws()
    {
        Assert.ThrowsAny<Exception>(() => new NuGetPackageAnalyzer("/nonexistent/package.nupkg"));
    }
}
