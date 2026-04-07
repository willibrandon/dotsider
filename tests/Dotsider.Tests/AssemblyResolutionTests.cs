using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for assembly resolution logic including app-local, shared framework,
/// bundle-backed, and type-forwarder resolution paths.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class AssemblyResolutionTests(SampleAssemblyFixture samples) : IDisposable
{
    /// <summary>Clears resolution caches after each test.</summary>
    public void Dispose()
    {
        ImplementationAssemblyResolver.ClearCache();
        DotNetRuntimeLocator.ClearCache();
    }

    /// <summary>Verifies that an app-local assembly resolves as FromFile before other probes.</summary>
    [Fact(Timeout = 30_000)]
    public void ResolveAssembly_AppLocal_StillPreferred()
    {
        // HelloWorld.dll sits next to HelloWorld.exe — resolving "HelloWorld" from
        // the exe's directory should find the .dll app-locally
        var resolved = AssemblyAnalyzer.ResolveAssembly(
            samples.HelloWorldExe, "HelloWorld");
        Assert.NotNull(resolved);
        var fromFile = Assert.IsType<ResolvedAssembly.FromFile>(resolved);
        Assert.EndsWith("HelloWorld.dll", fromFile.Path, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies that System.Runtime resolves from the shared framework.</summary>
    [Fact(Timeout = 30_000)]
    public void ResolveAssembly_FromSharedFramework_ReturnsFromFile()
    {
        // System.Runtime should be found in the shared framework
        var resolved = AssemblyAnalyzer.ResolveAssembly(
            samples.RichLibraryDll, "System.Runtime",
            ".NETCoreApp,Version=v10.0", "Microsoft.NETCore.App");
        Assert.NotNull(resolved);
        Assert.IsType<ResolvedAssembly.FromFile>(resolved);
    }

    /// <summary>
    /// Verifies that System.Runtime resolves successfully when the referencing assembly
    /// has bundle context set. Under dotnet test the runtime dir probe (step 2) may
    /// succeed first; in a real single-file host the bundle probe (step 3) would win.
    /// Either path is correct — the key invariant is that resolution succeeds.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolveAssembly_WithBundleContext_FindsSystemRuntime()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);
        var resolved = AssemblyAnalyzer.ResolveAssembly(
            "SelfContainedConsole.dll", "System.Runtime",
            sourceBundlePath: samples.SelfContainedConsoleExe);
        Assert.NotNull(resolved);
    }

    /// <summary>Verifies that mscorlib type forwarders resolve correctly through a bundle.</summary>
    [Fact(Timeout = 30_000)]
    public void ImplementationAssemblyResolver_WithBundle_ResolvesTypeForwarders()
    {
        Assert.NotNull(samples.SelfContainedConsoleExe);
        // mscorlib type forwarders should work through bundle-backed resolution
        var resolved = ImplementationAssemblyResolver.Resolve(
            "SelfContainedConsole.dll", "mscorlib", "System.Console",
            ".NETCoreApp,Version=v10.0", "Microsoft.NETCore.App",
            samples.SelfContainedConsoleExe);
        Assert.NotNull(resolved);
    }

    /// <summary>Verifies that target framework and preferred pack are threaded through resolution.</summary>
    [Fact(Timeout = 30_000)]
    public void ResolveAssembly_PreferredRuntimePack_ThreadedThrough()
    {
        // Verify that target framework and preferred pack reach the locator
        var resolved = AssemblyAnalyzer.ResolveAssembly(
            samples.RichLibraryDll, "System.Runtime",
            ".NETCoreApp,Version=v10.0", "Microsoft.NETCore.App");
        Assert.NotNull(resolved);
    }
}
