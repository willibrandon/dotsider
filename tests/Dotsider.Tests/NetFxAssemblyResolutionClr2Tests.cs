using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end resolution tests for CLR 2 roots — proves the dep-graph builder + the
/// <see cref="AssemblyAnalyzer.ResolveAssemblyByIdentity"/> bridge route through
/// <see cref="NetFxBinder"/> for Clr2 contexts and produce the same outcome the live runtime did.
/// Mirror of <see cref="NetFxAssemblyResolutionTests"/> for the Clr2 path.
/// </summary>
[TestClass]
public sealed class NetFxAssemblyResolutionClr2Tests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// The dep-graph for the CLR 2 root has no <c>Unresolved</c> or <c>IdentityMismatch</c>
    /// leaves for any reference the live runtime loaded, and the SharedDep node appears
    /// exactly once at v2.0.0.0 even though UsesSharedV1 references v1.0.0.0 and UsesSharedV2
    /// references v2.0.0.0 — both collapse via the bindingRedirect.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DependencyGraph_Clr2_NoUnresolvedLeaves_AndSharedDepCollapses()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);

        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var graph = DependencyGraphBuilder.Build(analyzer);

        // SharedDep collapses: exactly one node, bound version 2.0.0.0.
        var sharedDepNodes = graph.Nodes
            .Where(n => n.Name == "NetFxBindingRedirects.Clr2.SharedDep")
            .ToList();
        Assert.ContainsSingle(sharedDepNodes);
        Assert.AreEqual("2.0.0.0", sharedDepNodes[0].Version);
        Assert.IsFalse(sharedDepNodes[0].Unresolved);
        Assert.AreNotEqual(AssemblyProvenance.IdentityMismatch,
            graph.NavigationById[sharedDepNodes[0].Id].Provenance);
        Assert.AreNotEqual(AssemblyProvenance.Unresolved,
            graph.NavigationById[sharedDepNodes[0].Id].Provenance);

        // mscorlib resolves to the v2.0.50727 framework runtime directory and is framework.
        var mscorlib = graph.Nodes.FirstOrDefault(n => n.Name == "mscorlib");
        Assert.IsNotNull(mscorlib);
        Assert.IsFalse(mscorlib!.Unresolved);
        var mscorlibNav = graph.NavigationById[mscorlib.Id];
        Assert.IsTrue(mscorlibNav.IsFrameworkAssembly);
        Assert.AreEqual(AssemblyProvenance.FrameworkRuntimeDirectory, mscorlibNav.Provenance);
        var mscorlibPath = mscorlibNav.Resolved switch
        {
            ResolvedAssembly.FromFile f => f.Path,
            _ => string.Empty,
        };
        Assert.Contains("v2.0.50727", mscorlibPath, StringComparison.OrdinalIgnoreCase);

        // No Unresolved or IdentityMismatch leaves anywhere in the graph (the sample is
        // self-contained — every reference is reachable on this Windows host).
        foreach (var node in graph.Nodes)
        {
            var nav = graph.NavigationById[node.Id];
            Assert.AreNotEqual(AssemblyProvenance.Unresolved, nav.Provenance);
            Assert.AreNotEqual(AssemblyProvenance.IdentityMismatch, nav.Provenance);
        }
    }

    /// <summary>At least one edge into the redirected SharedDep node carries a per-edge
    /// RequestedIdentity recording the pre-redirect v1.0.0.0 — proves the dep-graph keeps
    /// the request-side identity for diagnostics even when the node is keyed on v2.0.0.0.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DependencyGraph_RedirectedRefs_PreservePreRedirectIdentityOnEdge()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);

        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var graph = DependencyGraphBuilder.Build(analyzer);

        var sharedDepNode = graph.Nodes.Single(n => n.Name == "NetFxBindingRedirects.Clr2.SharedDep");
        var edges = graph.Edges.Where(e => e.TargetId == sharedDepNode.Id).ToList();
        Assert.IsNotEmpty(edges);
        Assert.Contains(e => e.RequestedIdentity is { Version: "1.0.0.0" }, edges);
    }

    /// <summary>
    /// WindowsBase appears as a node, resolves to its v3.0.0.0 GAC location, and is classified
    /// framework. Covers the v3.0 reference-assemblies allowlist end-to-end through the
    /// dep-graph builder's <c>IsFrameworkAssembly</c> classification.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DependencyGraph_Clr2_WindowsBase_v3_0_ResolvesViaGacAndIsClassifiedFramework()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);

        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var graph = DependencyGraphBuilder.Build(analyzer);

        var windowsBase = graph.Nodes.FirstOrDefault(n => n.Name == "WindowsBase");
        Assert.IsNotNull(windowsBase);
        Assert.AreEqual("3.0.0.0", windowsBase!.Version);
        Assert.IsFalse(windowsBase.Unresolved);
        var nav = graph.NavigationById[windowsBase.Id];
        Assert.AreEqual(AssemblyProvenance.Gac, nav.Provenance);
        Assert.IsTrue(nav.IsFrameworkAssembly);
    }

    /// <summary>
    /// Bridge coverage: <see cref="AssemblyAnalyzer.ResolveAssemblyByIdentity"/> with a Clr2
    /// binding context routes through <see cref="NetFxBinder"/> and returns an
    /// <see cref="AssemblyResolution"/> whose <see cref="AssemblyResolution.AppliedPolicy"/>
    /// records the redirect for SharedDep v1 → v2.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ResolveAssemblyByIdentity_Clr2_SharedDepV1_AppliedPolicyPopulated()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);

        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);

        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.Clr2.SharedDep", "1.0.0.0", "neutral", "e89d2d22dd26920d");
        var resolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
            referencingAssemblyPath: Samples.NetFxBindingRedirectsClr2Exe!,
            identity: requested,
            netFxBindingContext: ctx);

        Assert.IsNotNull(resolution.Resolved);
        Assert.IsNotNull(resolution.AppliedPolicy);
        Assert.AreEqual(new Version(1, 0, 0, 0), resolution.AppliedPolicy!.RequestedVersion);
        Assert.AreEqual(new Version(2, 0, 0, 0), resolution.AppliedPolicy.BoundVersion);
        Assert.IsNotNull(resolution.LoadedIdentity);
        Assert.AreEqual("2.0.0.0", resolution.LoadedIdentity.Version);
    }

    private static void SkipIfNotWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Inconclusive("Test requires Windows (.NET Framework binder).");
    }

    private static void SkipIfClr2Absent()
    {
        if (!Samples.Clr2RuntimePresent)
            Assert.Inconclusive("CLR 2 runtime not installed on this host (no v2.0.50727 mscorlib).");
    }
}
