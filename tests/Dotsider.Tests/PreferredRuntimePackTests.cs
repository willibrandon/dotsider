using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="AssemblyAnalyzer.PreferredRuntimePack"/> detection
/// across different assembly types.
/// </summary>
[TestClass]
public sealed class PreferredRuntimePackTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>Verifies that a regular library detects NETCore.App as its runtime pack.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DetectRuntimePack_RichLibrary_ReturnsNETCoreApp()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.AreEqual("Microsoft.NETCore.App", analyzer.PreferredRuntimePack);
    }

    /// <summary>Verifies that a minimal API project detects AspNetCore.App as its runtime pack.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DetectRuntimePack_MinimalApi_ReturnsAspNetCoreApp()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.MinimalApiDll);
        Assert.AreEqual("Microsoft.AspNetCore.App", analyzer.PreferredRuntimePack);
    }

    /// <summary>Verifies that a NativeAOT exe with no metadata falls back to NETCore.App.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DetectRuntimePack_NoMetadata_ReturnsNETCoreApp()
    {
        // NativeAOT exe has no metadata — should fall back to NETCore.App
        Assert.IsNotNull(Samples.NativeAotConsoleExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        Assert.AreEqual("Microsoft.NETCore.App", analyzer.PreferredRuntimePack);
    }
}
