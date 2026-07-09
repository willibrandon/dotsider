using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="SingleFileBundleReader"/> covering bundle detection,
/// manifest parsing, and entry assembly extraction.
/// </summary>
[TestClass]
public sealed class SingleFileBundleReaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>Verifies that a self-contained single-file exe is detected as a bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsBundle_SelfContainedExe_ReturnsTrue()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        Assert.IsGreaterThan(0, offset);
    }

    /// <summary>Verifies that a regular managed DLL is not detected as a bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsBundle_RegularDll_ReturnsFalse()
    {
        Assert.IsFalse(SingleFileBundleReader.IsBundle(Samples.RichLibraryDll, out _));
    }

    /// <summary>Verifies that a NativeAOT exe is not detected as a bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsBundle_NativeAotExe_ReturnsFalse()
    {
        Assert.IsNotNull(Samples.NativeAotConsoleExe);
        Assert.IsFalse(SingleFileBundleReader.IsBundle(Samples.NativeAotConsoleExe!, out _));
    }

    /// <summary>Verifies that the manifest has a positive file count matching its entries.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_SelfContainedExe_HasEntries()
    {
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(Samples.SelfContainedConsoleExe!, offset);
        Assert.IsGreaterThan(0, manifest.FileCount);
        Assert.HasCount(manifest.FileCount, manifest.Entries);
    }

    /// <summary>Verifies that System.Runtime.dll is included in the bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_SelfContainedExe_ContainsSystemRuntime()
    {
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(Samples.SelfContainedConsoleExe!, offset);
        Assert.Contains(e =>
            e.Type == BundleFileType.Assembly
            && Path.GetFileName(e.RelativePath).Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase), manifest.Entries);
    }

    /// <summary>Verifies that the entry assembly DLL is included in the bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_SelfContainedExe_ContainsEntryAssembly()
    {
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(Samples.SelfContainedConsoleExe!, offset);
        Assert.Contains(e =>
            e.Type == BundleFileType.Assembly
            && Path.GetFileName(e.RelativePath).Equals("SelfContainedConsole.dll", StringComparison.OrdinalIgnoreCase), manifest.Entries);
    }

    /// <summary>Verifies that extracted System.Runtime bytes form a valid PE (MZ header).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadAssembly_SystemRuntime_ReturnsPeBytes()
    {
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(Samples.SelfContainedConsoleExe!, offset);
        var bytes = SingleFileBundleReader.ReadAssembly(Samples.SelfContainedConsoleExe!, manifest, "System.Runtime");
        Assert.IsNotNull(bytes);
        // Verify it's a valid PE — MZ header
        Assert.IsGreaterThan(2, bytes.Length);
        Assert.AreEqual((byte)'M', bytes[0]);
        Assert.AreEqual((byte)'Z', bytes[1]);
    }

    /// <summary>Verifies that FindEntryAssembly returns the correct entry name.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FindEntryAssembly_SelfContainedExe_MatchesBasename()
    {
        var result = SingleFileBundleReader.FindEntryAssembly(Samples.SelfContainedConsoleExe!);
        Assert.IsNotNull(result);
        Assert.AreEqual("SelfContainedConsole.dll", result.Value.Name);
        Assert.IsGreaterThan(0, result.Value.Bytes.Length);
    }

    /// <summary>Verifies that the extracted entry assembly has valid metadata.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FindEntryAssembly_SelfContainedExe_HasValidMetadata()
    {
        var result = SingleFileBundleReader.FindEntryAssembly(Samples.SelfContainedConsoleExe!);
        Assert.IsNotNull(result);
        using var analyzer = new AssemblyAnalyzer(result.Value.Bytes, result.Value.Name);
        Assert.IsTrue(analyzer.HasMetadata);
        Assert.AreEqual("SelfContainedConsole", analyzer.AssemblyName);
    }
}
