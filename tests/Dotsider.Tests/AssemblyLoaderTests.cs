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

    /// <summary>Verifies that a NativeAOT exe returns a Direct result without metadata.</summary>
    [Fact(Timeout = 30_000)]
    public void Open_NativeAotExe_ReturnsDirect()
    {
        Assert.NotNull(samples.NativeAotConsoleExe);
        var result = AssemblyLoader.Open(samples.NativeAotConsoleExe!);
        Assert.IsType<AssemblyOpenResult.Direct>(result);
        var direct = (AssemblyOpenResult.Direct)result;
        Assert.False(direct.Analyzer.HasMetadata);
        direct.Analyzer.Dispose();
    }
}
