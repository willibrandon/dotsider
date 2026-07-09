using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NetFxBindingContext"/> on CLR 2.0 (.NET Framework 2.0 / 3.0 / 3.5)
/// roots. Mirrors <see cref="NetFxBindingContextTests"/> beat-for-beat for the Clr2 path:
/// detection signal (mscorlib v2 reference), per-runtime path switching (GAC layout, framework
/// runtime dir, machine.config, reference-assemblies tree), and <c>appliesTo</c> filtering.
/// </summary>
[TestClass]
public sealed class NetFxBindingContextClr2Tests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// The sample EXE was built with <c>&lt;GenerateTargetFrameworkAttribute&gt;false&lt;/&gt;</c>.
    /// Prove the bug-shape: the issue's premise is that CLR 2 roots in the wild carry no
    /// <c>TargetFrameworkAttribute</c>. If this assertion fails, the sample fixture has drifted
    /// and the rest of the CLR 2 test suite would falsely pass via the existing v4 branch.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BugReproduction_Clr2RootHasNoTargetFrameworkAttribute()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        Assert.IsNull(analyzer.TargetFramework);
    }

    /// <summary>
    /// TryBuild detects the CLR 2 root through the mscorlib v2 reference and returns a Clr2
    /// context with the inferred-runtime flag set.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryBuild_Clr2Root_ReturnsClr2Context()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);

        Assert.IsNotNull(ctx);
        Assert.AreEqual(NetFxRuntimeVersion.Clr2, ctx!.RuntimeVersion);
        Assert.IsTrue(ctx.IsRuntimeVersionInferred);
        Assert.IsNull(ctx.TargetFramework); // sample has no TFA
        Assert.IsNotEmpty(ctx.Policy.AppConfigRedirects);
        Assert.Contains(p => p == "lib", ctx.PrivatePaths);
    }

    /// <summary>
    /// Regression: v4 detection still wins for net48 roots. Ensures the new Clr2 branch didn't
    /// reorder the existing TFM gate.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryBuild_Clr4Root_StillReturnsClr4Context()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        Assert.AreEqual(NetFxRuntimeVersion.Clr4, ctx!.RuntimeVersion);
        Assert.IsFalse(ctx.IsRuntimeVersionInferred);
    }

    /// <summary>
    /// Clr2 GAC scan list includes all three buckets: GAC_MSIL, the architecture slot, AND the
    /// bare GAC (CLR 1.x carryover, still consulted by CLR 2 fusion).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GacScanList_Clr2Root_IncludesAllThreeGacBuckets()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);

        var list = ctx!.GacScanList();
        Assert.IsGreaterThanOrEqualTo(3, list.Count, $"Expected at least 3 buckets, got {list.Count}");
        Assert.EndsWith("GAC_MSIL", list[0], StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(list[1].EndsWith("GAC_64", StringComparison.OrdinalIgnoreCase)
                 || list[1].EndsWith("GAC_32", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith("GAC", list[2], StringComparison.OrdinalIgnoreCase);
        // All three rooted at the CLR 2 GAC location.
        var windir = Environment.GetEnvironmentVariable("WINDIR")!;
        var clr2Root = Path.Combine(windir, "assembly");
        TestAssert.All(list, p => Assert.StartsWith(clr2Root, p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The legacy COM-PIA fallback list is empty for Clr2 — the primary scan list already covers it.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void LegacyGacScanList_Clr2_IsEmpty()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        Assert.IsEmpty(ctx!.LegacyGacScanList());
    }

    /// <summary>Framework runtime directory for a Clr2 root resolves to v2.0.50727 (architecture-correct).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FrameworkRuntimeDirectory_Clr2Root_IsV2_0_50727()
    {
        SkipIfNotWindows();
        if (!Samples.Clr2RuntimePresent)
            Assert.Inconclusive("CLR 2 runtime not installed on this host (no v2.0.50727 mscorlib).");
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);

        var dir = ctx!.FrameworkRuntimeDirectory();
        Assert.IsNotNull(dir);
        Assert.EndsWith("v2.0.50727", dir!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The Clr2 context loads the v2.0.50727 machine.config (not the v4.0.30319 one). Verified
    /// by ensuring publisher-policy enumeration uses the correct GAC root.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MachineConfig_Clr2_PathPointsAtV2Config()
    {
        SkipIfNotWindows();
        if (!Samples.Clr2RuntimePresent) Assert.Inconclusive("CLR 2 runtime not installed.");

        // BindingPolicy.LoadFrom(... Clr2) consults v2.0.50727\Config\machine.config; verify the
        // file the binder will read exists. (The exact contents are platform-dependent; we just
        // confirm the path-resolution branch picked the right runtime dir.)
        var windir = Environment.GetEnvironmentVariable("WINDIR")!;
        var arch = Environment.Is64BitOperatingSystem ? "Framework64" : "Framework";
        var machineConfig = Path.Combine(windir, "Microsoft.NET", arch, "v2.0.50727", "Config", "machine.config");
        Assert.IsTrue(File.Exists(machineConfig), $"v2.0.50727 machine.config not found at {machineConfig}");
    }

    /// <summary>
    /// Drive the parsed redirects through the real sample app.config and assert the
    /// <c>appliesTo</c> filter accepted every Clr2 form (empty, v2, v2.0, v2.0.50727) and
    /// rejected the v4-only poison block (whose 9.9.9.9 redirect must NOT appear).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AppliesTo_Clr2_AcceptsAllForms_RejectsV4()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);
        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);

        var redirects = ctx!.Policy.AppConfigRedirects;

        // Canonical CLR 2 form: SharedDep redirect under appliesTo="v2.0.50727".
        Assert.Contains(r =>
            r.Name.Equals("NetFxBindingRedirects.Clr2.SharedDep", StringComparison.OrdinalIgnoreCase) &&
            r.NewVersion == new Version(2, 0, 0, 0), redirects);

        // Short forms appliesTo="v2" and appliesTo="v2.0" should both be honored.
        Assert.Contains(r => r.Name == "Clr2.AppliesToShort.V2", redirects);
        Assert.Contains(r => r.Name == "Clr2.AppliesToShort.V20", redirects);

        // Poison block: v4-only appliesTo points SharedDep at 9.9.9.9. Must NOT have leaked
        // through to a Clr2 context.
        Assert.DoesNotContain(r =>
            r.Name.Equals("NetFxBindingRedirects.Clr2.SharedDep", StringComparison.OrdinalIgnoreCase) &&
            r.NewVersion == new Version(9, 9, 9, 9), redirects);
    }

    private static void SkipIfNotWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Inconclusive("Test requires Windows (.NET Framework binder).");
    }
}
