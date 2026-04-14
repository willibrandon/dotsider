using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Nu Get Package Analyzer.
/// </summary>
[Collection("SampleAssemblies")]
public class NuGetPackageAnalyzerTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies rich library nupkg has correct package id.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasCorrectPackageId()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.Equal("RichLibrary", pkg.PackageId);
    }

    /// <summary>
    /// Verifies rich library nupkg has correct version.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasCorrectVersion()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.Equal("2.5.1", pkg.PackageVersion);
    }

    /// <summary>
    /// Verifies rich library nupkg has files.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasFiles()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.NotEmpty(pkg.Files);
    }

    /// <summary>
    /// Verifies rich library nupkg has dll files.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasDllFiles()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.NotEmpty(pkg.DllFiles);
        Assert.All(pkg.DllFiles, f => Assert.True(f.IsDll));
    }

    /// <summary>
    /// Verifies rich library nupkg has nuspec file.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibraryNupkg_HasNuspecFile()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.Contains(pkg.Files, f => f.Name.EndsWith(".nuspec"));
    }

    /// <summary>
    /// Verifies open dll returns working analyzer.
    /// </summary>
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

    /// <summary>
    /// Verifies open dll matches standalone assembly.
    /// </summary>
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

    /// <summary>
    /// Verifies has authors and description.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HasAuthorsAndDescription()
    {
        using var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        Assert.NotNull(pkg.Authors);
        Assert.NotNull(pkg.Description);
    }

    /// <summary>
    /// Verifies Dispose can be called multiple times without side effects.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Dispose_IsIdempotent()
    {
        var pkg = new NuGetPackageAnalyzer(samples.RichLibraryNupkg);
        pkg.Dispose();
        pkg.Dispose(); // should not throw
    }

    /// <summary>
    /// Verifies invalid path throws.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void InvalidPath_Throws()
    {
        Assert.ThrowsAny<Exception>(() => new NuGetPackageAnalyzer("/nonexistent/package.nupkg"));
    }
}
