using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

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
    /// Verifies a package containing an actual runtime facade preserves every metadata model and
    /// resolves one of the facade's real type forwarders to the same implementation assembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void OpenDll_RealForwarderFacade_MatchesStandaloneAssembly()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var facadePath = Path.Combine(runtimeDirectory, "System.Collections.dll");
        Assert.IsTrue(File.Exists(facadePath), $"Runtime facade not found: {facadePath}");
        AssertRealForwarder(facadePath, "System.Collections.Generic", "List`1");

        var directory = Path.Combine(
            Path.GetTempPath(),
            "dotsider-forwarder-package-" + Guid.NewGuid().ToString("N"));
        var packagePath = Path.Combine(directory, "RuntimeFacade.1.0.0.nupkg");
        Directory.CreateDirectory(directory);
        try
        {
            CreateRuntimeFacadePackage(packagePath, facadePath);
            using var package = new NuGetPackageAnalyzer(packagePath);
            var entry = Assert.ContainsSingle(package.DllFiles);
            using var packaged = package.OpenDll(entry);
            using var standalone = new AssemblyAnalyzer(facadePath);

            Assert.AreEqual("RuntimeFacade", package.PackageId);
            Assert.AreSequenceEqual(standalone.TypeDefs, packaged.TypeDefs);
            Assert.AreSequenceEqual(standalone.TypeRefs, packaged.TypeRefs);
            Assert.AreSequenceEqual(standalone.MethodDefs, packaged.MethodDefs);
            Assert.AreSequenceEqual(standalone.FieldDefs, packaged.FieldDefs);
            Assert.AreSequenceEqual(standalone.MemberRefs, packaged.MemberRefs);

            var standaloneHome = ImplementationAssemblyResolver.Resolve(
                facadePath,
                "System.Collections",
                "System.Collections.Generic.List`1",
                standalone.TargetFramework,
                standalone.PreferredRuntimePack);
            var packagedHome = ImplementationAssemblyResolver.Resolve(
                packaged.FilePath,
                "System.Collections",
                "System.Collections.Generic.List`1",
                packaged.TargetFramework,
                packaged.PreferredRuntimePack);
            var standaloneFile = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(standaloneHome);
            var packagedFile = Assert.IsExactInstanceOfType<ResolvedAssembly.FromFile>(packagedHome);
            Assert.AreEqual(
                Path.GetFileName(standaloneFile.Path),
                Path.GetFileName(packagedFile.Path),
                StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual("System.Private.CoreLib.dll", Path.GetFileName(packagedFile.Path));
        }
        finally
        {
            ImplementationAssemblyResolver.ClearCache();
            Directory.Delete(directory, recursive: true);
        }
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

    private static void AssertRealForwarder(string assemblyPath, string namespaceName, string typeName)
    {
        const TypeAttributes Forwarder = (TypeAttributes)0x0020_0000;
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        Assert.Contains(
            handle =>
            {
                var exportedType = reader.GetExportedType(handle);
                return (exportedType.Attributes & Forwarder) != 0 &&
                    reader.GetString(exportedType.Namespace) == namespaceName &&
                    reader.GetString(exportedType.Name) == typeName;
            },
            reader.ExportedTypes);
    }

    private static void CreateRuntimeFacadePackage(string packagePath, string facadePath)
    {
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        var manifest = archive.CreateEntry("RuntimeFacade.nuspec");
        using (var writer = new StreamWriter(manifest.Open()))
        {
            writer.Write(
                "<package><metadata><id>RuntimeFacade</id><version>1.0.0</version>" +
                "<authors>Dotsider.Tests</authors><description>Runtime facade regression</description>" +
                "</metadata></package>");
        }

        var facade = archive.CreateEntry("lib/net10.0/System.Collections.dll");
        using var source = File.OpenRead(facadePath);
        using var destination = facade.Open();
        source.CopyTo(destination);
    }
}
