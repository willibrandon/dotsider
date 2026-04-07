using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="SingleFileBundleReader"/> covering bundle detection,
/// manifest parsing, and entry assembly extraction.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class SingleFileBundleReaderTests(SampleAssemblyFixture samples)
{
    /// <summary>Verifies that a self-contained single-file exe is detected as a bundle.</summary>
    [Fact(Timeout = 30_000)]
    public void IsBundle_SelfContainedExe_ReturnsTrue()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);
        Assert.True(SingleFileBundleReader.IsBundle(samples.SelfContainedConsoleExe!, out var offset));
        Assert.True(offset > 0);
    }

    /// <summary>Verifies that a regular managed DLL is not detected as a bundle.</summary>
    [Fact(Timeout = 30_000)]
    public void IsBundle_RegularDll_ReturnsFalse()
    {
        Assert.False(SingleFileBundleReader.IsBundle(samples.RichLibraryDll, out _));
    }

    /// <summary>Verifies that a NativeAOT exe is not detected as a bundle.</summary>
    [Fact(Timeout = 30_000)]
    public void IsBundle_NativeAotExe_ReturnsFalse()
    {
        Assert.NotNull(samples.NativeAotConsoleExe);
        Assert.False(SingleFileBundleReader.IsBundle(samples.NativeAotConsoleExe!, out _));
    }

    /// <summary>Verifies that the manifest has a positive file count matching its entries.</summary>
    [Fact(Timeout = 30_000)]
    public void ReadManifest_SelfContainedExe_HasEntries()
    {
        Assert.True(SingleFileBundleReader.IsBundle(samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(samples.SelfContainedConsoleExe!, offset);
        Assert.True(manifest.FileCount > 0);
        Assert.Equal(manifest.FileCount, manifest.Entries.Count);
    }

    /// <summary>Verifies that System.Runtime.dll is included in the bundle.</summary>
    [Fact(Timeout = 30_000)]
    public void ReadManifest_SelfContainedExe_ContainsSystemRuntime()
    {
        Assert.True(SingleFileBundleReader.IsBundle(samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(samples.SelfContainedConsoleExe!, offset);
        Assert.Contains(manifest.Entries, e =>
            e.Type == BundleFileType.Assembly
            && Path.GetFileName(e.RelativePath).Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Verifies that the entry assembly DLL is included in the bundle.</summary>
    [Fact(Timeout = 30_000)]
    public void ReadManifest_SelfContainedExe_ContainsEntryAssembly()
    {
        Assert.True(SingleFileBundleReader.IsBundle(samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(samples.SelfContainedConsoleExe!, offset);
        Assert.Contains(manifest.Entries, e =>
            e.Type == BundleFileType.Assembly
            && Path.GetFileName(e.RelativePath).Equals("SelfContainedConsole.dll", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Verifies that extracted System.Runtime bytes form a valid PE (MZ header).</summary>
    [Fact(Timeout = 30_000)]
    public void ReadAssembly_SystemRuntime_ReturnsPeBytes()
    {
        Assert.True(SingleFileBundleReader.IsBundle(samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(samples.SelfContainedConsoleExe!, offset);
        var bytes = SingleFileBundleReader.ReadAssembly(samples.SelfContainedConsoleExe!, manifest, "System.Runtime");
        Assert.NotNull(bytes);
        // Verify it's a valid PE — MZ header
        Assert.True(bytes.Length > 2);
        Assert.Equal((byte)'M', bytes[0]);
        Assert.Equal((byte)'Z', bytes[1]);
    }

    /// <summary>Verifies that FindEntryAssembly returns the correct entry name.</summary>
    [Fact(Timeout = 30_000)]
    public void FindEntryAssembly_SelfContainedExe_MatchesBasename()
    {
        var result = SingleFileBundleReader.FindEntryAssembly(samples.SelfContainedConsoleExe!);
        Assert.NotNull(result);
        Assert.Equal("SelfContainedConsole.dll", result.Value.Name);
        Assert.True(result.Value.Bytes.Length > 0);
    }

    /// <summary>Verifies that the extracted entry assembly has valid metadata.</summary>
    [Fact(Timeout = 30_000)]
    public void FindEntryAssembly_SelfContainedExe_HasValidMetadata()
    {
        var result = SingleFileBundleReader.FindEntryAssembly(samples.SelfContainedConsoleExe!);
        Assert.NotNull(result);
        using var analyzer = new AssemblyAnalyzer(result.Value.Bytes, result.Value.Name);
        Assert.True(analyzer.HasMetadata);
        Assert.Equal("SelfContainedConsole", analyzer.AssemblyName);
    }
}
