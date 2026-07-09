using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Nu Get Package Analyzer.
/// </summary>
[TestClass]
public class NuGetPackageAnalyzerTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies rich library nupkg has correct package id.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibraryNupkg_HasCorrectPackageId()
    {
        using var pkg = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);
        Assert.AreEqual("RichLibrary", pkg.PackageId);
    }

    /// <summary>
    /// Verifies rich library nupkg has correct version.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibraryNupkg_HasCorrectVersion()
    {
        using var pkg = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);
        Assert.AreEqual("2.5.1", pkg.PackageVersion);
    }

    /// <summary>
    /// Verifies rich library nupkg has files.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibraryNupkg_HasFiles()
    {
        using var pkg = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);
        Assert.IsNotEmpty(pkg.Files);
    }

    /// <summary>
    /// Verifies rich library nupkg has dll files.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibraryNupkg_HasDllFiles()
    {
        using var pkg = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);
        Assert.IsNotEmpty(pkg.DllFiles);
        TestAssert.All(pkg.DllFiles, f => Assert.IsTrue(f.IsDll));
    }

    /// <summary>
    /// Verifies rich library nupkg has nuspec file.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibraryNupkg_HasNuspecFile()
    {
        using var pkg = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);
        Assert.Contains(f => f.Name.EndsWith(".nuspec"), pkg.Files);
    }

    /// <summary>
    /// Verifies open dll returns working analyzer.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void OpenDll_ReturnsWorkingAnalyzer()
    {
        using var pkg = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);
        var dll = pkg.DllFiles[0];
        using var analyzer = pkg.OpenDll(dll);
        Assert.IsNotNull(analyzer);
        Assert.IsTrue(analyzer.HasMetadata);
        Assert.AreEqual("RichLibrary", analyzer.AssemblyName);
    }

    /// <summary>
    /// Verifies open dll matches standalone assembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void OpenDll_MatchesStandaloneAssembly()
    {
        using var pkg = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);
        var dll = pkg.DllFiles[0];
        using var fromPkg = pkg.OpenDll(dll);
        using var standalone = new AssemblyAnalyzer(Samples.RichLibraryDll);
        // Both should have the same types
        Assert.HasCount(standalone.TypeDefs.Count, fromPkg.TypeDefs);
    }

    /// <summary>
    /// Verifies has authors and description.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HasAuthorsAndDescription()
    {
        using var pkg = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);
        Assert.IsNotNull(pkg.Authors);
        Assert.IsNotNull(pkg.Description);
    }

    /// <summary>
    /// Verifies Dispose can be called multiple times without side effects.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dispose_IsIdempotent()
    {
        var pkg = new NuGetPackageAnalyzer(Samples.RichLibraryNupkg);
        pkg.Dispose();
        pkg.Dispose(); // should not throw
    }

    /// <summary>
    /// Verifies invalid path throws.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void InvalidPath_Throws()
    {
        Assert.Throws<Exception>(() => new NuGetPackageAnalyzer("/nonexistent/package.nupkg"));
    }
}
