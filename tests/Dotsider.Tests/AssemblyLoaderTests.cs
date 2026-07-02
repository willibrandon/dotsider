using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="AssemblyLoader.Open"/> covering managed DLLs, apphosts,
/// single-file bundles, and NativeAOT executables.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class AssemblyLoaderTests(SampleAssemblyFixture samples)
{
    /// <summary>Verifies that a managed DLL returns a Direct result with metadata.</summary>
    [Fact(Timeout = 30_000)]
    public void Open_ManagedDll_ReturnsDirect()
    {
        var result = AssemblyLoader.Open(samples.RichLibraryDll);
        Assert.IsType<AssemblyOpenResult.Direct>(result);
        var direct = (AssemblyOpenResult.Direct)result;
        Assert.True(direct.Analyzer.HasMetadata);
        direct.Analyzer.Dispose();
    }

    /// <summary>Verifies that an apphost exe returns ApphostWithCompanion with a valid companion path.</summary>
    [Fact(Timeout = 30_000)]
    public void Open_ApphostExe_ReturnsApphostWithCompanion()
    {
        var result = AssemblyLoader.Open(samples.HelloWorldExe);
        Assert.IsType<AssemblyOpenResult.ApphostWithCompanion>(result);
        var apphost = (AssemblyOpenResult.ApphostWithCompanion)result;
        Assert.False(apphost.HostAnalyzer.HasMetadata);
        Assert.EndsWith(".dll", apphost.CompanionDllPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(apphost.CompanionDllPath));
        apphost.HostAnalyzer.Dispose();
    }

    /// <summary>Verifies that a single-file bundle returns a BundleEntry with valid metadata.</summary>
    [Fact(Timeout = 30_000)]
    public void Open_SingleFileBundle_ReturnsBundleEntry()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);
        var result = AssemblyLoader.Open(samples.SelfContainedConsoleExe!);
        Assert.IsType<AssemblyOpenResult.BundleEntry>(result);
        var bundle = (AssemblyOpenResult.BundleEntry)result;
        Assert.True(bundle.EntryAnalyzer.HasMetadata);
        Assert.Equal("SelfContainedConsole", bundle.EntryAnalyzer.AssemblyName);
        Assert.Equal(samples.SelfContainedConsoleExe, bundle.BundlePath);
        Assert.Equal(samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.SourceBundlePath);
        // FilePath is the on-disk bundle path; DisplayName is the entry assembly name
        Assert.Equal(samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.FilePath);
        Assert.Equal("SelfContainedConsole.dll", bundle.EntryAnalyzer.DisplayName);
        bundle.EntryAnalyzer.Dispose();
    }

    /// <summary>Verifies that bundle-backed analyzers expose correct capabilities.</summary>
    [Fact(Timeout = 30_000)]
    public void Open_SingleFileBundle_HasCorrectCapabilities()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);
        var result = AssemblyLoader.Open(samples.SelfContainedConsoleExe!);
        var bundle = (AssemblyOpenResult.BundleEntry)result;
        Assert.True(bundle.EntryAnalyzer.IsBundleBacked);
        Assert.False(bundle.EntryAnalyzer.CanSaveInPlace);
        Assert.Equal(samples.SelfContainedConsoleExe, bundle.EntryAnalyzer.LaunchPath);
        bundle.EntryAnalyzer.Dispose();
    }

    /// <summary>Verifies that file-backed analyzers expose correct capabilities.</summary>
    [Fact(Timeout = 30_000)]
    public void Open_ManagedDll_HasCorrectCapabilities()
    {
        var result = AssemblyLoader.Open(samples.RichLibraryDll);
        var direct = (AssemblyOpenResult.Direct)result;
        Assert.False(direct.Analyzer.IsBundleBacked);
        Assert.True(direct.Analyzer.CanSaveInPlace);
        Assert.Equal(samples.RichLibraryDll, direct.Analyzer.LaunchPath);
        Assert.Equal(direct.Analyzer.FileName, direct.Analyzer.DisplayName);
        direct.Analyzer.Dispose();
    }

    /// <summary>Verifies that a NativeAOT exe returns a NativeAot result without metadata.</summary>
    [Fact(Timeout = 30_000)]
    public void Open_NativeAotExe_ReturnsNativeAot()
    {
        Assert.NotNull(samples.NativeAotConsoleExe);
        var result = AssemblyLoader.Open(samples.NativeAotConsoleExe!);
        Assert.IsType<AssemblyOpenResult.NativeAot>(result);
        var aot = (AssemblyOpenResult.NativeAot)result;
        Assert.False(aot.Analyzer.HasMetadata);
        Assert.NotNull(aot.Analyzer.NativeAotInfo);
        Assert.Equal(BinaryKind.NativeAot, aot.Analyzer.BinaryKind);
        aot.Analyzer.Dispose();
    }

    /// <summary>
    /// Verifies that a single-file bundle is never classified as Native AOT even though
    /// the ReadyToRun assemblies inside it contain RTR signatures — the bundle check
    /// runs before the Native AOT probe.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Open_SingleFileBundle_NotClassifiedAsNativeAot()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);
        var result = AssemblyLoader.Open(samples.SelfContainedConsoleExe!);
        Assert.IsType<AssemblyOpenResult.BundleEntry>(result);
        ((AssemblyOpenResult.BundleEntry)result).EntryAnalyzer.Dispose();
    }

    /// <summary>Verifies that a managed DLL is classified as Managed.</summary>
    [Fact(Timeout = 30_000)]
    public void Open_ManagedDll_HasManagedBinaryKind()
    {
        var result = AssemblyLoader.Open(samples.RichLibraryDll);
        var direct = (AssemblyOpenResult.Direct)result;
        Assert.Equal(BinaryKind.Managed, direct.Analyzer.BinaryKind);
        Assert.Null(direct.Analyzer.NativeAotInfo);
        direct.Analyzer.Dispose();
    }
}
