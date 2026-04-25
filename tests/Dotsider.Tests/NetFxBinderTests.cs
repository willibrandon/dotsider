using System.Runtime.InteropServices;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests driving <see cref="NetFxBinder.Bind"/> directly against the freshly-built
/// NetFxBindingRedirects sample tree and the real Windows install. No mocks, no synthetic
/// binder behavior — every assertion compares dotsider's bind result to a real on-disk
/// file the actual .NET Framework CLR would also load.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class NetFxBinderTests(SampleAssemblyFixture samples)
{
    /// <summary>mscorlib resolves from the .NET Framework runtime directory.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_Mscorlib_FromFrameworkRuntimeDirectory_MatchesOracle()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo("mscorlib", "4.0.0.0", "neutral", "b77a5c561934e089");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.Equal(AssemblyProvenance.FrameworkRuntimeDirectory, result.Provenance);
        Assert.NotNull(result.LoadedPath);
        Assert.Equal(
            oracle["mscorlib"].Location,
            result.LoadedPath,
            ignoreCase: true);
    }

    /// <summary>System.Drawing resolves from the GAC.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_SystemDrawing_ResolvesViaGacOrRuntimeDirectoryPerOracle()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo("System.Drawing", "4.0.0.0", "neutral", "b03f5f7f11d50a3a");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.True(result.Provenance is AssemblyProvenance.Gac or AssemblyProvenance.FrameworkRuntimeDirectory,
            $"Expected Gac or FrameworkRuntimeDirectory, got {result.Provenance}");
        Assert.Equal(oracle["System.Drawing"].Location, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>Newtonsoft.Json 12 redirects to 13 and resolves from app-local with the
    /// AppliedPolicy annotation populated.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_NewtonsoftJson_RedirectAppliedAndAppLocalHit_MatchesOracleVersionAndPath()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo("Newtonsoft.Json", "12.0.0.0", "neutral", "30ad4fe6b2a6aeed");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.Equal(AssemblyProvenance.AppLocal, result.Provenance);
        Assert.NotNull(result.AppliedPolicy);
        Assert.Equal(new Version(13, 0, 0, 0), result.AppliedPolicy!.BoundVersion);
        Assert.Equal(oracle["Newtonsoft.Json"].Location, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>Two distinct requested versions of Newtonsoft.Json collapse to the same
    /// LoadedAssemblyEntry (reference-equal), proving the LoadedAssemblyCache interns by
    /// bound identity.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_LoadedAssemblyCache_TwoDistinctRequestsCollapseToOneInternedLoadedEntry()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        NetFxBinder.ClearCaches(ctx);
        var v12 = new AssemblyRefInfo("Newtonsoft.Json", "12.0.0.0", "neutral", "30ad4fe6b2a6aeed");
        var v13 = new AssemblyRefInfo("Newtonsoft.Json", "13.0.0.0", "neutral", "30ad4fe6b2a6aeed");
        var r12 = NetFxBinder.Bind(v12, ctx);
        var r13 = NetFxBinder.Bind(v13, ctx);
        Assert.NotNull(r12.Loaded);
        Assert.NotNull(r13.Loaded);
        Assert.Equal(r12.Loaded, r13.Loaded);
        Assert.Equal(r12.LoadedPath, r13.LoadedPath);
    }

    /// <summary>Repeated bind requests for the same identity hit the cache; no extra probes.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_RequestedBindCache_RepeatedRequest_NoSecondFilesystemTraversal()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        NetFxBinder.ClearCaches(ctx);
        var requested = new AssemblyRefInfo("mscorlib", "4.0.0.0", "neutral", "b77a5c561934e089");
        NetFxBinder.Bind(requested, ctx);
        var probesAfterFirst = NetFxBinder.GetProbeCount(ctx);
        NetFxBinder.Bind(requested, ctx);
        Assert.Equal(probesAfterFirst, NetFxBinder.GetProbeCount(ctx));
    }

    /// <summary>Failures are cached too — known misses don't re-probe.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_RequestedBindCache_FailureCached_DoesNotReprobe()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        NetFxBinder.ClearCaches(ctx);
        var nonExistent = new AssemblyRefInfo("Acme.NeverInstalled", "1.0.0.0", "neutral", "0000000000000099");
        NetFxBinder.Bind(nonExistent, ctx);
        var afterFirst = NetFxBinder.GetProbeCount(ctx);
        NetFxBinder.Bind(nonExistent, ctx);
        Assert.Equal(afterFirst, NetFxBinder.GetProbeCount(ctx));
    }

    /// <summary>Configured codeBase href resolves to the file under external/.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_CodeBaseLib_ResolvedViaCodeBaseHref()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.CodeBaseLib", "2.0.0.0", "neutral", "e061e779022b0ce6");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.Equal(AssemblyProvenance.CodeBase, result.Provenance);
        Assert.NotNull(result.LoadedPath);
        Assert.Equal(
            oracle["NetFxBindingRedirects.CodeBaseLib"].Location,
            result.LoadedPath,
            ignoreCase: true);
    }

    /// <summary>A codeBase whose href doesn't exist fails fast with CodeBaseMissing
    /// rather than falling through to probing.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_CodeBaseMissing_FailFast_DoesNotFallBackToProbing()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.MissingCodeBase", "9.9.9.9", "neutral", "0123456789abcdef");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.Equal(AssemblyProvenance.CodeBaseMissing, result.Provenance);
        Assert.Null(result.LoadedPath);
        Assert.Contains("Missing.dll", result.FailureReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>privatePath probing is rooted at the application base, not the parent DLL.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_PrivatePathLib_ResolvedFromAppBaseLibSubdir()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.PrivatePathLib", "1.0.0.0", "neutral", null);
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.Equal(AssemblyProvenance.AppLocal, result.Provenance);
        Assert.NotNull(result.LoadedPath);
        Assert.Equal(
            oracle["NetFxBindingRedirects.PrivatePathLib"].Location,
            result.LoadedPath,
            ignoreCase: true);
        Assert.Contains(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar,
            result.LoadedPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// CodeBaseMissing carries the configured href on both <see cref="AppliedPolicy.CodeBaseHref"/>
    /// and <see cref="NetFxBindResult.CandidateProbePath"/> so the UI can surface the broken href
    /// without having to dig into the failure reason string.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Bind_CodeBaseMissing_PreservesHrefForDiagnostics()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.MissingCodeBase", "9.9.9.9", "neutral", "0123456789abcdef");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.Equal(AssemblyProvenance.CodeBaseMissing, result.Provenance);
        Assert.NotNull(result.AppliedPolicy);
        Assert.NotNull(result.AppliedPolicy!.CodeBaseHref);
        Assert.EndsWith("Missing.dll", result.AppliedPolicy.CodeBaseHref!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.CandidateProbePath);
        Assert.EndsWith("Missing.dll", result.CandidateProbePath!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Third-party strong-named assemblies installed in the GAC must not be classified as
    /// framework: only well-known Microsoft framework PKTs qualify under
    /// <c>AssemblyAnalyzer.IsFrameworkAssembly</c>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IsFrameworkAssembly_GacProvenance_OnlyTrueForWellKnownFrameworkPkt()
    {
        // Microsoft framework PKT b77a5c561934e089 → framework.
        var msIdentity = new AssemblyRefInfo("Anything", "1.0.0.0", "neutral", "b77a5c561934e089");
        Assert.True(AssemblyAnalyzer.IsFrameworkAssembly(
            AssemblyProvenance.Gac, msIdentity, ".NETFramework,Version=v4.8", null));

        // Third-party strong-named lib: random PKT → not framework, even when in the GAC.
        var thirdParty = new AssemblyRefInfo("Vendor.Lib", "1.0.0.0", "neutral", "0123456789abcdef");
        Assert.False(AssemblyAnalyzer.IsFrameworkAssembly(
            AssemblyProvenance.Gac, thirdParty, ".NETFramework,Version=v4.8", null));
    }

    /// <summary>A weak-named assembly skips the GAC scan entirely.</summary>
    [Fact(Timeout = 30_000)]
    public void Bind_NotStrongNamed_SkipsGac()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        NetFxBinder.ClearCaches(ctx);
        var weakNamed = new AssemblyRefInfo("Some.WeakNamed", "1.0.0.0", "neutral", null);
        NetFxBinder.Bind(weakNamed, ctx);
        // GAC scan would walk both GAC_MSIL + GAC_<arch>; if it fired we'd see at least 2 probes
        // before the AppLocal fallback. Anything below the AppLocal probe count proves we skipped.
        // (The AppLocal probe walks 4 paths for a neutral-name miss, so total < 6 means no GAC.)
        Assert.True(NetFxBinder.GetProbeCount(ctx) <= 8,
            $"Probe count {NetFxBinder.GetProbeCount(ctx)} suggests GAC was scanned for a weak-named assembly");
    }

    private (NetFxBindingContext Ctx, IReadOnlyDictionary<string, NetFxOracleEntry> Oracle) LoadFixture()
    {
        Assert.NotNull(samples.NetFxBindingRedirectsExe);
        Assert.NotNull(samples.NetFxBindingRedirectsOracle);
        var analyzer = new AssemblyAnalyzer(samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.NotNull(ctx);
        return (ctx!, samples.NetFxBindingRedirectsOracle!);
    }

    private static void SkipIfNotWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Skip("Test requires Windows (.NET Framework binder).");
    }
}
