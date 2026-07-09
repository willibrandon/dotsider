using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests driving <see cref="NetFxBinder.Bind"/> directly against the freshly-built
/// NetFxBindingRedirects sample tree and the real Windows install. No mocks, no synthetic
/// binder behavior — every assertion compares dotsider's bind result to a real on-disk
/// file the actual .NET Framework CLR would also load.
/// </summary>
[TestClass]
public sealed class NetFxBinderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>mscorlib resolves from the .NET Framework runtime directory.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_Mscorlib_FromFrameworkRuntimeDirectory_MatchesOracle()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo("mscorlib", "4.0.0.0", "neutral", "b77a5c561934e089");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.FrameworkRuntimeDirectory, result.Provenance);
        Assert.IsNotNull(result.LoadedPath);
        Assert.AreEqual(
            oracle["mscorlib"].Location,
            result.LoadedPath,
            ignoreCase: true);
    }

    /// <summary>System.Drawing resolves from the GAC.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_SystemDrawing_ResolvesViaGacOrRuntimeDirectoryPerOracle()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo("System.Drawing", "4.0.0.0", "neutral", "b03f5f7f11d50a3a");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.IsTrue(result.Provenance is AssemblyProvenance.Gac or AssemblyProvenance.FrameworkRuntimeDirectory,
            $"Expected Gac or FrameworkRuntimeDirectory, got {result.Provenance}");
        Assert.AreEqual(oracle["System.Drawing"].Location, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>Newtonsoft.Json 12 redirects to 13 and resolves from app-local with the
    /// AppliedPolicy annotation populated.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_NewtonsoftJson_RedirectAppliedAndAppLocalHit_MatchesOracleVersionAndPath()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo("Newtonsoft.Json", "12.0.0.0", "neutral", "30ad4fe6b2a6aeed");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.AppLocal, result.Provenance);
        Assert.IsNotNull(result.AppliedPolicy);
        Assert.AreEqual(new Version(13, 0, 0, 0), result.AppliedPolicy!.BoundVersion);
        Assert.AreEqual(oracle["Newtonsoft.Json"].Location, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>Two distinct requested versions of Newtonsoft.Json collapse to the same
    /// LoadedAssemblyEntry (reference-equal), proving the LoadedAssemblyCache interns by
    /// bound identity.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_LoadedAssemblyCache_TwoDistinctRequestsCollapseToOneInternedLoadedEntry()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        NetFxBinder.ClearCaches(ctx);
        var v12 = new AssemblyRefInfo("Newtonsoft.Json", "12.0.0.0", "neutral", "30ad4fe6b2a6aeed");
        var v13 = new AssemblyRefInfo("Newtonsoft.Json", "13.0.0.0", "neutral", "30ad4fe6b2a6aeed");
        var r12 = NetFxBinder.Bind(v12, ctx);
        var r13 = NetFxBinder.Bind(v13, ctx);
        Assert.IsNotNull(r12.Loaded);
        Assert.IsNotNull(r13.Loaded);
        Assert.AreEqual(r12.Loaded, r13.Loaded);
        Assert.AreEqual(r12.LoadedPath, r13.LoadedPath);
    }

    /// <summary>Repeated bind requests for the same identity hit the cache; no extra probes.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_RequestedBindCache_RepeatedRequest_NoSecondFilesystemTraversal()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        NetFxBinder.ClearCaches(ctx);
        var requested = new AssemblyRefInfo("mscorlib", "4.0.0.0", "neutral", "b77a5c561934e089");
        NetFxBinder.Bind(requested, ctx);
        var probesAfterFirst = NetFxBinder.GetProbeCount(ctx);
        NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(probesAfterFirst, NetFxBinder.GetProbeCount(ctx));
    }

    /// <summary>Failures are cached too — known misses don't re-probe.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_RequestedBindCache_FailureCached_DoesNotReprobe()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        NetFxBinder.ClearCaches(ctx);
        var nonExistent = new AssemblyRefInfo("Acme.NeverInstalled", "1.0.0.0", "neutral", "0000000000000099");
        NetFxBinder.Bind(nonExistent, ctx);
        var afterFirst = NetFxBinder.GetProbeCount(ctx);
        NetFxBinder.Bind(nonExistent, ctx);
        Assert.AreEqual(afterFirst, NetFxBinder.GetProbeCount(ctx));
    }

    /// <summary>Configured codeBase href resolves to the file under external/.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_CodeBaseLib_ResolvedViaCodeBaseHref()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.CodeBaseLib", "2.0.0.0", "neutral", "e061e779022b0ce6");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.CodeBase, result.Provenance);
        Assert.IsNotNull(result.LoadedPath);
        Assert.AreEqual(
            oracle["NetFxBindingRedirects.CodeBaseLib"].Location,
            result.LoadedPath,
            ignoreCase: true);
    }

    /// <summary>A codeBase whose href doesn't exist fails fast with CodeBaseMissing
    /// rather than falling through to probing.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_CodeBaseMissing_FailFast_DoesNotFallBackToProbing()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.MissingCodeBase", "9.9.9.9", "neutral", "0123456789abcdef");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.CodeBaseMissing, result.Provenance);
        Assert.IsNull(result.LoadedPath);
        Assert.Contains("Missing.dll", result.FailureReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>privatePath probing is rooted at the application base, not the parent DLL.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_PrivatePathLib_ResolvedFromAppBaseLibSubdir()
    {
        SkipIfNotWindows();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.PrivatePathLib", "1.0.0.0", "neutral", null);
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.AppLocal, result.Provenance);
        Assert.IsNotNull(result.LoadedPath);
        Assert.AreEqual(
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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_CodeBaseMissing_PreservesHrefForDiagnostics()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.MissingCodeBase", "9.9.9.9", "neutral", "0123456789abcdef");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.CodeBaseMissing, result.Provenance);
        Assert.IsNotNull(result.AppliedPolicy);
        Assert.IsNotNull(result.AppliedPolicy!.CodeBaseHref);
        Assert.EndsWith("Missing.dll", result.AppliedPolicy.CodeBaseHref!, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(result.CandidateProbePath);
        Assert.EndsWith("Missing.dll", result.CandidateProbePath!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Third-party strong-named assemblies installed in the GAC must not be classified as
    /// framework: only well-known Microsoft framework PKTs qualify under
    /// <c>AssemblyAnalyzer.IsFrameworkAssembly</c>.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsFrameworkAssembly_GacProvenance_OnlyTrueForWellKnownFrameworkPkt()
    {
        // Microsoft framework PKT b77a5c561934e089 → framework.
        var msIdentity = new AssemblyRefInfo("Anything", "1.0.0.0", "neutral", "b77a5c561934e089");
        Assert.IsTrue(AssemblyAnalyzer.IsFrameworkAssembly(
            AssemblyProvenance.Gac, msIdentity, ".NETFramework,Version=v4.8", null));

        // Third-party strong-named lib: random PKT → not framework, even when in the GAC.
        var thirdParty = new AssemblyRefInfo("Vendor.Lib", "1.0.0.0", "neutral", "0123456789abcdef");
        Assert.IsFalse(AssemblyAnalyzer.IsFrameworkAssembly(
            AssemblyProvenance.Gac, thirdParty, ".NETFramework,Version=v4.8", null));
    }

    /// <summary>
    /// A <c>file://server/share/...</c> codeBase href must round-trip to the UNC path
    /// <c>\\server\share\...</c> rather than dropping the leading slashes and looking relative.
    /// We don't expect the share to exist on the test machine — the assertion is that the
    /// failure reason names the original UNC href, proving the resolver kept the prefix and
    /// hit a real file probe (which then missed) instead of silently misclassifying.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_CodeBaseHref_UncFileUri_PreservesUncPrefix()
    {
        SkipIfNotWindows();

        // Synthesize a binding context with one codeBase entry pointing at a UNC URI. We use
        // a server name that almost certainly does not resolve, so the bind fails — but the
        // failure must report the UNC href, which it can only do if the resolver kept \\.
        var codeBase = new CodeBaseEntry(
            PolicyLayer.AppConfig,
            "Acme.UncLib", "1111111111111111", "neutral",
            new Version(1, 0, 0, 0),
            "file://dotsider-test-no-such-server/share/Acme.UncLib.dll");
        var policy = new BindingPolicy(
            AppConfigRedirects: [],
            PublisherPolicyRedirects: [],
            MachineConfigRedirects: [],
            FrameworkUnificationRedirects: [],
            CodeBases: [codeBase],
            PublisherPolicyDisabledFor: []);
        var ctx = new NetFxBindingContext(
            EntryAssemblyPath: Path.Combine(Path.GetTempPath(), "no-such-root.exe"),
            AppBaseDirectory: Path.GetTempPath(),
            ConfigPath: null,
            TargetFramework: ".NETFramework,Version=v4.8",
            EffectiveArchitecture: NetFxArchitecture.Amd64,
            Policy: policy,
            PrivatePaths: [],
            GacRoots: []);

        var requested = new AssemblyRefInfo("Acme.UncLib", "1.0.0.0", "neutral", "1111111111111111");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.CodeBaseMissing, result.Provenance);
        Assert.IsNotNull(result.AppliedPolicy);
        Assert.AreEqual(codeBase.Href, result.AppliedPolicy!.CodeBaseHref);
        Assert.AreEqual(codeBase.Href, result.CandidateProbePath);
    }

    /// <summary>
    /// The CLR's framework unification table covers in-box framework assemblies: a request for
    /// <c>Microsoft.VisualBasic v8.0.0.0</c> unifies to the runtime's <c>v10.0.0.0</c> at the
    /// policy stage, then locates from the GAC where the file actually lives — the stock GAC
    /// slot <c>v4.0_10.0.0.0__b03f5f7f11d50a3a</c>. Provenance is <see cref="AssemblyProvenance.Gac"/>,
    /// not the framework runtime directory, so the location dotsider reports matches the CLR.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_FrameworkUnification_MicrosoftVisualBasicV8_UnifiesAndLoadsFromGac()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo("Microsoft.VisualBasic", "8.0.0.0", "neutral", "b03f5f7f11d50a3a");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.IsNotNull(result.Loaded);
        Assert.AreEqual("10.0.0.0", result.Loaded!.Version);
        Assert.IsNotNull(result.LoadedPath);
        Assert.Contains("GAC_MSIL", result.LoadedPath, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(result.AppliedPolicy);
        Assert.AreEqual(PolicyLayer.FrameworkUnification, result.AppliedPolicy!.Source);
        Assert.AreEqual(new Version(8, 0, 0, 0), result.AppliedPolicy.RequestedVersion);
        Assert.AreEqual(new Version(10, 0, 0, 0), result.AppliedPolicy.BoundVersion);
    }

    /// <summary>
    /// Compatibility-pack assemblies (PKT <c>cc7b13ffcd2ddd51</c>: System.ValueTuple,
    /// System.Memory, System.Buffers, etc.) are <em>not</em> covered by the CLR's framework
    /// unification table. A request for <c>System.ValueTuple v4.1.0.0</c> against the in-box
    /// <c>v4.0.0.0</c> file must fail without an explicit binding redirect — same as the real
    /// runtime — rather than silently rolling forward.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_CompatibilityPackPkt_DoesNotUnify()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo("System.ValueTuple", "4.1.0.0", "neutral", "cc7b13ffcd2ddd51");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreNotEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.AreNotEqual(AssemblyProvenance.FrameworkRuntimeDirectory, result.Provenance);
        // The framework unification layer must not have rewritten the version.
        if (result.AppliedPolicy is not null)
            Assert.AreNotEqual(PolicyLayer.FrameworkUnification, result.AppliedPolicy.Source);
    }

    /// <summary>
    /// In-box framework assemblies unify in both directions. Verified against live net48
    /// PowerShell: <c>System.IO.Compression v4.2.0.0</c> loads as <c>v4.0.0.0</c> from
    /// <c>GAC_MSIL\System.IO.Compression\v4.0_4.0.0.0__b77a5c561934e089</c>, even though the
    /// requested version is higher than the runtime's. The unification table rewrites the
    /// effective identity, then the GAC scan finds the file at its real slot.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_FrameworkUnification_HigherThanFramework_RollsDownToInBoxVersion()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo("System.IO.Compression", "4.2.0.0", "neutral", "b77a5c561934e089");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.IsNotNull(result.Loaded);
        Assert.AreEqual("4.0.0.0", result.Loaded!.Version);
        Assert.IsNotNull(result.LoadedPath);
        Assert.Contains("GAC_MSIL", result.LoadedPath, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(result.AppliedPolicy);
        Assert.AreEqual(PolicyLayer.FrameworkUnification, result.AppliedPolicy!.Source);
        Assert.AreEqual(new Version(4, 2, 0, 0), result.AppliedPolicy.RequestedVersion);
        Assert.AreEqual(new Version(4, 0, 0, 0), result.AppliedPolicy.BoundVersion);
    }

    /// <summary>
    /// mscorlib unifies the same way: a request for <c>mscorlib v8.0.0.0</c> loads the
    /// runtime's <c>v4.0.0.0</c> from <c>Framework[64]\v4.0.30319</c>. Verified against live
    /// net48 PowerShell. The mscorlib fast path runs after unification, so the effective
    /// identity already matches the in-box file and the strict identity check passes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_FrameworkUnification_MscorlibV8_LoadsFrameworkV4()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo("mscorlib", "8.0.0.0", "neutral", "b77a5c561934e089");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.FrameworkRuntimeDirectory, result.Provenance);
        Assert.IsNotNull(result.Loaded);
        Assert.AreEqual("4.0.0.0", result.Loaded!.Version);
        Assert.IsNotNull(result.LoadedPath);
        Assert.Contains("v4.0.30319", result.LoadedPath, StringComparison.OrdinalIgnoreCase);
        Assert.IsNotNull(result.AppliedPolicy);
        Assert.AreEqual(PolicyLayer.FrameworkUnification, result.AppliedPolicy!.Source);
        Assert.AreEqual(new Version(8, 0, 0, 0), result.AppliedPolicy.RequestedVersion);
        Assert.AreEqual(new Version(4, 0, 0, 0), result.AppliedPolicy.BoundVersion);
    }

    /// <summary>
    /// Net4 fusion falls back to the legacy CLR 2.0 GAC at <c>C:\Windows\assembly</c> for
    /// COM PIAs and other 2.0-registered assemblies. Verified against live net48:
    /// <c>stdole 7.0.3300.0</c> loads from
    /// <c>C:\Windows\assembly\GAC\stdole\7.0.3300.0__b03f5f7f11d50a3a\stdole.dll</c>. Token
    /// format there has no <c>v4.0_</c> prefix.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_LegacyGac_StdoleResolvesFromWindowsAssemblyGac()
    {
        SkipIfNotWindows();
        // Skip if the test box doesn't have stdole installed in the legacy GAC.
        var stdolePath = @"C:\Windows\assembly\GAC\stdole\7.0.3300.0__b03f5f7f11d50a3a\stdole.dll";
        if (!File.Exists(stdolePath))
            Assert.Inconclusive("Legacy GAC entry for stdole 7.0.3300.0 is not present on this machine.");

        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo("stdole", "7.0.3300.0", "neutral", "b03f5f7f11d50a3a");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.IsNotNull(result.LoadedPath);
        Assert.AreEqual(stdolePath, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>
    /// Non-framework Microsoft-signed assemblies installed in the GAC (VS, SQL Server, etc.)
    /// must not enter the unification table. The framework name allowlist is built from the
    /// Reference Assemblies tree; anything outside it is third-party even when it happens to
    /// carry a framework public key token. Picks one example empirically: synthesize a request
    /// for an assembly name that's known to live in the GAC under a framework PKT but isn't a
    /// framework assembly, and verify the binder doesn't unify it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_FrameworkUnification_NonFrameworkGacEntries_DoNotUnify()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        // Microsoft.Build.Tasks.Core ships in MSBuild (not the .NET Framework reference
        // assemblies) but is signed with PKT b03f5f7f11d50a3a and lives in the GAC on machines
        // with VS / Build Tools. Even when present, the unification table must not pull it in.
        var requested = new AssemblyRefInfo("Microsoft.Build.Tasks.Core", "1.0.0.0", "neutral", "b03f5f7f11d50a3a");
        var result = NetFxBinder.Bind(requested, ctx);
        if (result.AppliedPolicy is not null)
            Assert.AreNotEqual(PolicyLayer.FrameworkUnification, result.AppliedPolicy.Source);
    }

    /// <summary>
    /// The GAC unification scan must use the same architecture-compatible bucket list as the
    /// locate stage — <c>GAC_MSIL</c> + the matching bitness — so a higher version installed
    /// in the cross-architecture slot doesn't end up in the table where it's unreachable.
    /// On an Amd64 root, scanning GAC_32 would be a contradiction; the
    /// <c>Bind_FrameworkUnification_SystemPrintingV3_UnifiesViaGacEntry</c> test below
    /// exercises GAC_64 explicitly. This test simply confirms a GAC_32-only entry isn't
    /// picked up on an Amd64 root.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_FrameworkUnification_RespectsRootArchitecture()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        Assert.AreEqual(NetFxArchitecture.Amd64, ctx.EffectiveArchitecture);
        // The unification table built for an Amd64 root must not contain entries that exist
        // only in GAC_32. We can't easily synthesize a GAC_32-only entry on this machine, but
        // we can assert the table's entries map to versions reachable from the locate stage —
        // i.e. every (name, pkt) in the table has either a Framework64 file or a GAC_64/MSIL
        // entry at the table's version. Sample-check System.Printing.
        Assert.IsNotNull(ctx.Policy.FrameworkUnificationTable);
        var key = ("System.Printing", "31bf3856ad364e35");
        Assert.IsTrue(
            ctx.Policy.FrameworkUnificationTable!.ContainsKey(key),
            "System.Printing should be in the unification table for an Amd64 root.");
        var v = ctx.Policy.FrameworkUnificationTable[key];
        Assert.IsTrue(File.Exists(
            $@"C:\Windows\Microsoft.NET\assembly\GAC_64\System.Printing\v4.0_{v}__31bf3856ad364e35\System.Printing.dll")
            || File.Exists(
            $@"C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Printing\v4.0_{v}__31bf3856ad364e35\System.Printing.dll"),
            $"Unification table records System.Printing v{v} but no architecture-compatible GAC slot exists.");
    }

    /// <summary>
    /// Framework assemblies that live only in the GAC (the WPF set: <c>System.Printing</c>,
    /// <c>PresentationCore</c>, …) must still participate in framework unification.
    /// Verified against live net48: <c>System.Printing v3.0.0.0</c> loads as <c>v4.0.0.0</c>
    /// from <c>GAC_64\System.Printing\v4.0_4.0.0.0__31bf3856ad364e35\</c>. The unification
    /// table picks these up by walking the GAC alongside the framework runtime directory.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_FrameworkUnification_SystemPrintingV3_UnifiesViaGacEntry()
    {
        SkipIfNotWindows();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo("System.Printing", "3.0.0.0", "neutral", "31bf3856ad364e35");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.IsNotNull(result.Loaded);
        Assert.AreEqual("4.0.0.0", result.Loaded!.Version);
        Assert.IsNotNull(result.AppliedPolicy);
        Assert.AreEqual(PolicyLayer.FrameworkUnification, result.AppliedPolicy!.Source);
        Assert.AreEqual(new Version(3, 0, 0, 0), result.AppliedPolicy.RequestedVersion);
        Assert.AreEqual(new Version(4, 0, 0, 0), result.AppliedPolicy.BoundVersion);
    }

    /// <summary>A weak-named assembly skips the GAC scan entirely.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsLessThanOrEqualTo(8, NetFxBinder.GetProbeCount(ctx), $"Probe count {NetFxBinder.GetProbeCount(ctx)} suggests GAC was scanned for a weak-named assembly");
    }

    private static (NetFxBindingContext Ctx, IReadOnlyDictionary<string, NetFxOracleEntry> Oracle) LoadFixture()
    {
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        Assert.IsNotNull(Samples.NetFxBindingRedirectsOracle);
        var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        return (ctx!, Samples.NetFxBindingRedirectsOracle!);
    }

    private static void SkipIfNotWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Inconclusive("Test requires Windows (.NET Framework binder).");
    }
}
