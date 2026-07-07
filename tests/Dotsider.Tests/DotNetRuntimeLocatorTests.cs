using Dotsider.Core.Analysis;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="DotNetRuntimeLocator"/> covering .NET base path discovery
/// and shared framework assembly resolution.
/// </summary>
public sealed class DotNetRuntimeLocatorTests : IDisposable
{
    /// <summary>Initializes a new instance and clears the locator cache.</summary>
    public DotNetRuntimeLocatorTests() => DotNetRuntimeLocator.ClearCache();

    /// <summary>Clears the locator cache after each test.</summary>
    public void Dispose() => DotNetRuntimeLocator.ClearCache();

    /// <summary>Verifies that the .NET base path exists and contains a shared directory.</summary>
    [Fact(Timeout = 30_000)]
    public void FindDotNetBasePath_ReturnsValidDirectory()
    {
        var basePath = DotNetRuntimeLocator.FindDotNetBasePath();
        Assert.NotNull(basePath);
        Assert.True(Directory.Exists(basePath));
        Assert.True(Directory.Exists(Path.Combine(basePath, "shared")));
    }

    /// <summary>Verifies that System.Runtime can be found in the shared framework.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_SystemRuntime_ReturnsPathAndPack()
    {
        var result = DotNetRuntimeLocator.FindAssemblyInSharedFramework("System.Runtime", null);
        Assert.NotNull(result);
        Assert.True(File.Exists(result.Path));
        Assert.EndsWith(".dll", result.Path);
        Assert.NotEmpty(result.RuntimePack);
    }

    /// <summary>Verifies that System.Private.CoreLib can be found in the shared framework.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_SystemPrivateCoreLib_ReturnsPath()
    {
        var result = DotNetRuntimeLocator.FindAssemblyInSharedFramework("System.Private.CoreLib", null);
        Assert.NotNull(result);
        Assert.True(File.Exists(result.Path));
    }

    /// <summary>Verifies that a nonexistent assembly returns null.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_NonexistentAssembly_ReturnsNull()
    {
        var result = DotNetRuntimeLocator.FindAssemblyInSharedFramework("DoesNotExist.FakeAssembly", null);
        Assert.Null(result);
    }

    /// <summary>Verifies that targeting v10.0 returns a path containing "10.".</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_WithTargetFramework_MatchesVersion()
    {
        var result = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            "System.Runtime", ".NETCoreApp,Version=v10.0");
        Assert.NotNull(result);
        Assert.Contains("10.", result.Path);
    }

    /// <summary>Verifies that NETCore.App preferred pack is probed first.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_PreferredPack_NETCoreApp_ProbesFirst()
    {
        var result = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            "System.Runtime", null, "Microsoft.NETCore.App");
        Assert.NotNull(result);
        Assert.Contains("Microsoft.NETCore.App", result.Path);
        Assert.Equal("Microsoft.NETCore.App", result.RuntimePack);
    }

    /// <summary>Verifies that WindowsDesktop.App preferred pack is probed first on Windows.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_PreferredPack_WindowsDesktopApp_ProbesFirst()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            "WindowsDesktop.App is only available on Windows");

        var result = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            "WindowsBase", null, "Microsoft.WindowsDesktop.App");
        Assert.NotNull(result);
        Assert.Contains("Microsoft.WindowsDesktop.App", result.Path);
        Assert.Equal("Microsoft.WindowsDesktop.App", result.RuntimePack);
    }

    /// <summary>Verifies that AspNetCore.App preferred pack is probed first when available.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_PreferredPack_AspNetCoreApp_ProbesFirst()
    {
        var result = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            "Microsoft.AspNetCore", null, "Microsoft.AspNetCore.App");
        if (result is not null)
        {
            Assert.Contains("Microsoft.AspNetCore.App", result.Path);
            Assert.Equal("Microsoft.AspNetCore.App", result.RuntimePack);
        }
    }
}
