using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="AssemblyLoader.Open"/> covering managed DLLs, apphosts,
/// single-file bundles, and NativeAOT executables.
/// </summary>
[TestClass]
public sealed class AssemblyLoaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>Verifies that a managed DLL returns a Direct result with metadata.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Open_ManagedDll_ReturnsDirect()
    {
        var result = AssemblyLoader.Open(Samples.RichLibraryDll);
        Assert.IsExactInstanceOfType<AssemblyOpenResult.Direct>(result);
        var direct = (AssemblyOpenResult.Direct)result;
        Assert.IsTrue(direct.Analyzer.HasMetadata);
        direct.Analyzer.Dispose();
    }

    /// <summary>Verifies that an apphost exe returns ApphostWithCompanion with a valid companion path.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Open_ApphostExe_ReturnsApphostWithCompanion()
    {
        var result = AssemblyLoader.Open(Samples.HelloWorldExe);
        Assert.IsExactInstanceOfType<AssemblyOpenResult.ApphostWithCompanion>(result);
        var apphost = (AssemblyOpenResult.ApphostWithCompanion)result;
        Assert.IsFalse(apphost.HostAnalyzer.HasMetadata);
        Assert.EndsWith(".dll", apphost.CompanionDllPath, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(File.Exists(apphost.CompanionDllPath));
        apphost.HostAnalyzer.Dispose();
    }

    /// <summary>Verifies that a single-file bundle returns a BundleEntry with valid metadata.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Open_SingleFileBundle_ReturnsBundleEntry()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        var result = AssemblyLoader.Open(Samples.SelfContainedConsoleExe!);
        Assert.IsExactInstanceOfType<AssemblyOpenResult.BundleEntry>(result);
        var bundle = (AssemblyOpenResult.BundleEntry)result;
        Assert.IsTrue(bundle.EntryAnalyzer.HasMetadata);
        Assert.AreEqual("SelfContainedConsole", bundle.EntryAnalyzer.AssemblyName);
        Assert.AreEqual(Samples.SelfContainedConsoleExe, bundle.BundlePath);
        Assert.AreEqual(Samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.SourceBundlePath);
        // FilePath is the on-disk bundle path; DisplayName is the entry assembly name
        Assert.AreEqual(Samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.FilePath);
        Assert.AreEqual("SelfContainedConsole.dll", bundle.EntryAnalyzer.DisplayName);
        bundle.EntryAnalyzer.Dispose();
    }

    /// <summary>Verifies that bundle-backed analyzers expose correct capabilities.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Open_SingleFileBundle_HasCorrectCapabilities()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        var result = AssemblyLoader.Open(Samples.SelfContainedConsoleExe!);
        var bundle = (AssemblyOpenResult.BundleEntry)result;
        Assert.IsTrue(bundle.EntryAnalyzer.IsBundleBacked);
        Assert.IsFalse(bundle.EntryAnalyzer.CanSaveInPlace);
        Assert.AreEqual(Samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.LaunchPath);
        bundle.EntryAnalyzer.Dispose();
    }

    /// <summary>Verifies that file-backed analyzers expose correct capabilities.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Open_ManagedDll_HasCorrectCapabilities()
    {
        var result = AssemblyLoader.Open(Samples.RichLibraryDll);
        var direct = (AssemblyOpenResult.Direct)result;
        Assert.IsFalse(direct.Analyzer.IsBundleBacked);
        Assert.IsTrue(direct.Analyzer.CanSaveInPlace);
        Assert.AreEqual(Samples.RichLibraryDll, direct.Analyzer.LaunchPath);
        Assert.AreEqual(direct.Analyzer.FileName, direct.Analyzer.DisplayName);
        direct.Analyzer.Dispose();
    }

    /// <summary>Verifies that a NativeAOT exe returns a NativeAot result without metadata.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Open_NativeAotExe_ReturnsNativeAot()
    {
        Assert.IsNotNull(Samples.NativeAotConsoleExe);
        var result = AssemblyLoader.Open(Samples.NativeAotConsoleExe!);
        Assert.IsExactInstanceOfType<AssemblyOpenResult.NativeAot>(result);
        var aot = (AssemblyOpenResult.NativeAot)result;
        Assert.IsFalse(aot.Analyzer.HasMetadata);
        Assert.IsNotNull(aot.Analyzer.NativeAotInfo);
        Assert.AreEqual(BinaryKind.NativeAot, aot.Analyzer.BinaryKind);
        aot.Analyzer.Dispose();
    }

    /// <summary>
    /// Verifies that a single-file bundle is never classified as Native AOT even though
    /// the ReadyToRun assemblies inside it contain RTR signatures — the bundle check
    /// runs before the Native AOT probe.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Open_SingleFileBundle_NotClassifiedAsNativeAot()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        var result = AssemblyLoader.Open(Samples.SelfContainedConsoleExe!);
        Assert.IsExactInstanceOfType<AssemblyOpenResult.BundleEntry>(result);
        ((AssemblyOpenResult.BundleEntry)result).EntryAnalyzer.Dispose();
    }

    /// <summary>Verifies that a managed DLL is classified as Managed.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Open_ManagedDll_HasManagedBinaryKind()
    {
        var result = AssemblyLoader.Open(Samples.RichLibraryDll);
        var direct = (AssemblyOpenResult.Direct)result;
        Assert.AreEqual(BinaryKind.Managed, direct.Analyzer.BinaryKind);
        Assert.IsNull(direct.Analyzer.NativeAotInfo);
        direct.Analyzer.Dispose();
    }
}
