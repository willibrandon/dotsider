using System.Runtime.InteropServices;
using Dotsider.Core.Analysis;

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
    public void FindAssemblyInSharedFramework_SystemRuntime_ReturnsPath()
    {
        var path = DotNetRuntimeLocator.FindAssemblyInSharedFramework("System.Runtime", null);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.EndsWith(".dll", path);
    }

    /// <summary>Verifies that System.Private.CoreLib can be found in the shared framework.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_SystemPrivateCoreLib_ReturnsPath()
    {
        var path = DotNetRuntimeLocator.FindAssemblyInSharedFramework("System.Private.CoreLib", null);
        Assert.NotNull(path);
        Assert.True(File.Exists(path));
    }

    /// <summary>Verifies that a nonexistent assembly returns null.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_NonexistentAssembly_ReturnsNull()
    {
        var path = DotNetRuntimeLocator.FindAssemblyInSharedFramework("DoesNotExist.FakeAssembly", null);
        Assert.Null(path);
    }

    /// <summary>Verifies that targeting v10.0 returns a path containing "10.".</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_WithTargetFramework_MatchesVersion()
    {
        var path = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            "System.Runtime", ".NETCoreApp,Version=v10.0");
        Assert.NotNull(path);
        Assert.Contains("10.", path);
    }

    /// <summary>Verifies that NETCore.App preferred pack is probed first.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_PreferredPack_NETCoreApp_ProbesFirst()
    {
        var path = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            "System.Runtime", null, "Microsoft.NETCore.App");
        Assert.NotNull(path);
        Assert.Contains("Microsoft.NETCore.App", path);
    }

    /// <summary>Verifies that WindowsDesktop.App preferred pack is probed first on Windows.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_PreferredPack_WindowsDesktopApp_ProbesFirst()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            "WindowsDesktop.App is only available on Windows");

        var path = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            "WindowsBase", null, "Microsoft.WindowsDesktop.App");
        Assert.NotNull(path);
        Assert.Contains("Microsoft.WindowsDesktop.App", path);
    }

    /// <summary>Verifies that AspNetCore.App preferred pack is probed first when available.</summary>
    [Fact(Timeout = 30_000)]
    public void FindAssemblyInSharedFramework_PreferredPack_AspNetCoreApp_ProbesFirst()
    {
        var path = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            "Microsoft.AspNetCore", null, "Microsoft.AspNetCore.App");
        // ASP.NET Core runtime may or may not be installed — just verify no crash
        // and that when found, it's from the right pack
        if (path is not null)
            Assert.Contains("Microsoft.AspNetCore.App", path);
    }
}
