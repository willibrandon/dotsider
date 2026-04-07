using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="AssemblyAnalyzer.PreferredRuntimePack"/> detection
/// across different assembly types.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class PreferredRuntimePackTests(SampleAssemblyFixture samples)
{
    /// <summary>Verifies that a regular library detects NETCore.App as its runtime pack.</summary>
    [Fact(Timeout = 30_000)]
    public void DetectRuntimePack_RichLibrary_ReturnsNETCoreApp()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Equal("Microsoft.NETCore.App", analyzer.PreferredRuntimePack);
    }

    /// <summary>Verifies that a minimal API project detects AspNetCore.App as its runtime pack.</summary>
    [Fact(Timeout = 30_000)]
    public void DetectRuntimePack_MinimalApi_ReturnsAspNetCoreApp()
    {
        using var analyzer = new AssemblyAnalyzer(samples.MinimalApiDll);
        Assert.Equal("Microsoft.AspNetCore.App", analyzer.PreferredRuntimePack);
    }

    /// <summary>Verifies that a NativeAOT exe with no metadata falls back to NETCore.App.</summary>
    [Fact(Timeout = 30_000)]
    public void DetectRuntimePack_NoMetadata_ReturnsNETCoreApp()
    {
        // NativeAOT exe has no metadata — should fall back to NETCore.App
        Assert.NotNull(samples.NativeAotConsoleExe);
        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        Assert.Equal("Microsoft.NETCore.App", analyzer.PreferredRuntimePack);
    }
}
