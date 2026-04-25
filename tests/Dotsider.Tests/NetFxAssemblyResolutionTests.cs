using System.Runtime.InteropServices;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Verifies that every metadata-backed resolution surface in dotsider — Dep Graph builder,
/// AssemblyAnalyzer.ResolveAssemblyByIdentity, ImplementationAssemblyResolver — produces the
/// same answer for any net48 reference and that .NET Core / .NET 5+ probe paths are unchanged.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class NetFxAssemblyResolutionTests(SampleAssemblyFixture samples) : IDisposable
{
    /// <summary>Clears resolution caches after each test so they don't leak across assertions.</summary>
    public void Dispose()
    {
        ImplementationAssemblyResolver.ClearCache();
        DotNetRuntimeLocator.ClearCache();
    }

    /// <summary>
    /// AssemblyAnalyzer.ResolveAssemblyByIdentity routes through NetFxBinder when a net48
    /// context is supplied: a redirected reference returns the bound identity + AppliedPolicy.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolveAssemblyByIdentity_NetFxRoot_RoutesThroughBinder()
    {
        SkipIfNotWindows();
        Assert.NotNull(samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.NotNull(ctx);

        var requested = new AssemblyRefInfo("Newtonsoft.Json", "12.0.0.0", "neutral", "30ad4fe6b2a6aeed");
        var resolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
            samples.NetFxBindingRedirectsExe!, requested,
            analyzer.TargetFramework, analyzer.PreferredRuntimePack, analyzer.SourceBundlePath, ctx);

        Assert.NotNull(resolution.Resolved);
        Assert.NotNull(resolution.AppliedPolicy);
        Assert.Equal(new Version(13, 0, 0, 0), resolution.AppliedPolicy!.BoundVersion);
        Assert.NotNull(resolution.LoadedIdentity);
        Assert.Equal("13.0.0.0", resolution.LoadedIdentity!.Version);
    }

    /// <summary>
    /// Without a net48 context the existing probe chain runs unchanged.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NonNetFxRoot_NoBindingContextBuilt_ProbeChainUnchanged()
    {
        // HelloWorld is a .NET 10 root — no NetFxBindingContext.
        using var helloAnalyzer = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Null(NetFxBindingContext.TryBuild(helloAnalyzer));

        // Resolve System.Runtime; with no context the call falls through to the .NET shared
        // framework path and returns a non-null result with the original (non-net48) provenance set.
        var requested = new AssemblyRefInfo("System.Runtime", "10.0.0.0", "neutral", "b03f5f7f11d50a3a");
        var resolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
            samples.HelloWorldDll, requested,
            helloAnalyzer.TargetFramework, helloAnalyzer.PreferredRuntimePack, helloAnalyzer.SourceBundlePath,
            netFxBindingContext: null);
        Assert.NotNull(resolution.Resolved);
        Assert.Null(resolution.AppliedPolicy);
        Assert.Null(resolution.LoadedIdentity);
        Assert.NotEqual(AssemblyProvenance.Gac, resolution.Provenance);
        Assert.NotEqual(AssemblyProvenance.FrameworkRuntimeDirectory, resolution.Provenance);
    }

    /// <summary>
    /// ImplementationAssemblyResolver routes through NetFxBinder when both the context and the
    /// referencing analyzer are supplied.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ImplementationAssemblyResolver_NetFxRoot_RoutesThroughBinder()
    {
        SkipIfNotWindows();
        Assert.NotNull(samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.NotNull(ctx);

        var resolved = ImplementationAssemblyResolver.Resolve(
            samples.NetFxBindingRedirectsExe!, "Newtonsoft.Json",
            declaringType: null,
            analyzer.TargetFramework, analyzer.PreferredRuntimePack, analyzer.SourceBundlePath,
            ctx, analyzer);
        Assert.NotNull(resolved);
        var fromFile = Assert.IsType<ResolvedAssembly.FromFile>(resolved);
        Assert.Equal("Newtonsoft.Json.dll", Path.GetFileName(fromFile.Path));
    }

    /// <summary>
    /// DependencyGraphBuilder.Build for a net48 root produces no Unresolved/IdentityMismatch
    /// leaves for assemblies whose oracle says the CLR loaded them, and collapses two requested
    /// versions of Newtonsoft.Json onto a single graph node keyed on the bound identity.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void DependencyGraph_NetFxBindingRedirects_NoUnresolvedLeaves_AndOldNewDepCollapse()
    {
        SkipIfNotWindows();
        Assert.NotNull(samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(samples.NetFxBindingRedirectsExe!);
        var graph = DependencyGraphBuilder.Build(analyzer);

        // The Newtonsoft.Json node should appear exactly once even though OldDep references
        // 12.0.0.0 and NewDep references 13.0.0.0 — both redirect to 13.0.0.0.
        var newtonsoftNodes = graph.Nodes.Where(n => n.Name == "Newtonsoft.Json").ToList();
        Assert.Single(newtonsoftNodes);
        Assert.Equal("13.0.0.0", newtonsoftNodes[0].Version);

        // No Newtonsoft.Json node should be Unresolved or carry IdentityMismatch.
        Assert.False(newtonsoftNodes[0].Unresolved);
        Assert.NotEqual(AssemblyProvenance.IdentityMismatch,
            graph.NavigationById[newtonsoftNodes[0].Id].Provenance);
        Assert.NotEqual(AssemblyProvenance.Unresolved,
            graph.NavigationById[newtonsoftNodes[0].Id].Provenance);

        // mscorlib should resolve to the framework runtime directory and be classified framework.
        var mscorlib = graph.Nodes.FirstOrDefault(n => n.Name == "mscorlib");
        Assert.NotNull(mscorlib);
        Assert.False(mscorlib!.Unresolved);
        var mscorlibNav = graph.NavigationById[mscorlib.Id];
        Assert.True(mscorlibNav.IsFrameworkAssembly);
        Assert.Equal(AssemblyProvenance.FrameworkRuntimeDirectory, mscorlibNav.Provenance);
    }

    /// <summary>
    /// At least one edge into the redirected Newtonsoft.Json node carries a per-edge
    /// RequestedIdentity recording the pre-redirect version (12.0.0.0 from OldDep).
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void DependencyGraph_RedirectedRefs_PreservePreRedirectIdentityOnEdge()
    {
        SkipIfNotWindows();
        Assert.NotNull(samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(samples.NetFxBindingRedirectsExe!);
        var graph = DependencyGraphBuilder.Build(analyzer);

        var newtonsoftNode = graph.Nodes.Single(n => n.Name == "Newtonsoft.Json");
        var edges = graph.Edges.Where(e => e.TargetId == newtonsoftNode.Id).ToList();
        Assert.NotEmpty(edges);
        Assert.Contains(edges, e => e.RequestedIdentity is { Version: "12.0.0.0" });
    }

    private static void SkipIfNotWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Skip("Test requires Windows (.NET Framework binder).");
    }
}
