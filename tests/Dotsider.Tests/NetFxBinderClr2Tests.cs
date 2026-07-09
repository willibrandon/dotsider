using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests driving <see cref="NetFxBinder.Bind"/> directly against the CLR 2.0 sample tree and the
/// real Windows install. Mirror of <see cref="NetFxBinderTests"/> for the Clr2 path. No mocks for
/// production-runtime tests — every live-runtime assertion compares dotsider's bind result to a
/// real on-disk file the CLR 2.0 fusion would also load. The synthetic temp-GAC tests are
/// deliberately host-independent: they run on every Windows host even when CLR 2 isn't installed.
/// </summary>
[TestClass]
public sealed class NetFxBinderClr2Tests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    // ---- Live-runtime tests (gated on Clr2RuntimePresent) ----

    /// <summary>mscorlib resolves from the v2.0.50727 framework runtime directory.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_Mscorlib_FromV2FrameworkRuntimeDirectory_MatchesOracle()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo("mscorlib", "2.0.0.0", "neutral", "b77a5c561934e089");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.FrameworkRuntimeDirectory, result.Provenance);
        Assert.IsNotNull(result.LoadedPath);
        Assert.Contains("v2.0.50727", result.LoadedPath, StringComparison.OrdinalIgnoreCase);
        Assert.AreEqual(oracle["mscorlib"].Location, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>System.Drawing v2.0.0.0 resolves from %WINDIR%\assembly\GAC_MSIL with the
    /// no-prefix Clr2 token format <c>2.0.0.0__b03f5f7f11d50a3a</c>.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_SystemDrawing_FromClr2Gac_MatchesOracle()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo("System.Drawing", "2.0.0.0", "neutral", "b03f5f7f11d50a3a");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.IsNotNull(result.LoadedPath);
        Assert.AreEqual(oracle["System.Drawing"].Location, result.LoadedPath, ignoreCase: true);
        // No v4.0_ prefix on the Clr2 token.
        Assert.DoesNotContain("v4.0_", result.LoadedPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2.0.0.0__", result.LoadedPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>WindowsBase 3.0.0.0 — the v3.0 reference-assemblies allowlist coverage. Without
    /// the v3.0 branch in <c>LoadFrameworkAssemblyNames</c>, this would silently miss the
    /// unification table and resolve incorrectly.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_WindowsBase_v3_0_FromClr2Gac_ProvesV3ReferenceAssembliesAllowlist()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo("WindowsBase", "3.0.0.0", "neutral", "31bf3856ad364e35");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.AreEqual(oracle["WindowsBase"].Location, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>SharedDep v1.0.0.0 redirects to v2.0.0.0 via the appliesTo="v2.0.50727"
    /// bindingRedirect and resolves from app-local; AppliedPolicy is populated.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_SharedDep_RedirectAppliedAndAppLocal_MatchesOraclePath()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.Clr2.SharedDep", "1.0.0.0", "neutral", "e89d2d22dd26920d");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.AppLocal, result.Provenance);
        Assert.IsNotNull(result.AppliedPolicy);
        Assert.AreEqual(new Version(2, 0, 0, 0), result.AppliedPolicy!.BoundVersion);
        Assert.AreEqual(oracle["SharedDep_via_UsesV1"].Location, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>Two distinct requested versions of SharedDep collapse to the same loaded
    /// identity, proving the LoadedAssemblyCache interns by bound identity.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_LoadedAssemblyCache_SharedDepV1AndV2_CollapseToOneInternedEntry()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        var (ctx, _) = LoadFixture();
        NetFxBinder.ClearCaches(ctx);
        var v1 = new AssemblyRefInfo("NetFxBindingRedirects.Clr2.SharedDep", "1.0.0.0", "neutral", "e89d2d22dd26920d");
        var v2 = new AssemblyRefInfo("NetFxBindingRedirects.Clr2.SharedDep", "2.0.0.0", "neutral", "e89d2d22dd26920d");
        var r1 = NetFxBinder.Bind(v1, ctx);
        var r2 = NetFxBinder.Bind(v2, ctx);
        Assert.IsNotNull(r1.Loaded);
        Assert.IsNotNull(r2.Loaded);
        Assert.AreEqual(r1.Loaded, r2.Loaded);
        Assert.AreEqual(r1.LoadedPath, r2.LoadedPath);
    }

    /// <summary>CodeBase href is honored: resolves to <c>external/Clr2.CodeBaseLib.dll</c>.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_CodeBaseLib_ViaConfiguredCodeBase_ProvenanceIsCodeBase()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.Clr2.CodeBaseLib", "2.0.0.0", "neutral", "d4a9fecb5ef90905");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.CodeBase, result.Provenance);
        Assert.AreEqual(oracle["NetFxBindingRedirects.Clr2.CodeBaseLib"].Location, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>Deliberately-broken codeBase fail-fasts as <c>CodeBaseMissing</c> with the
    /// configured href reported on the result, distinct from generic Unresolved.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_MissingCodeBase_FailsFastWithCodeBaseMissingProvenance()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.Clr2.MissingCodeBase", "9.9.9.9", "neutral", "0123456789abcdef");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.CodeBaseMissing, result.Provenance);
        Assert.IsNotNull(result.AppliedPolicy);
        Assert.AreEqual("external/Missing.dll", result.AppliedPolicy!.CodeBaseHref);
    }

    /// <summary>PrivatePathLib resolves from the <c>lib/</c> subdir via probing privatePath.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_PrivatePathLib_FromProbingPrivatePath_LandsInLibSubdir()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.Clr2.PrivatePathLib", "1.0.0.0", "neutral", null);
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.AppLocal, result.Provenance);
        Assert.AreEqual(oracle["NetFxBindingRedirects.Clr2.PrivatePathLib"].Location, result.LoadedPath, ignoreCase: true);
        Assert.Contains(Path.Combine("lib", "NetFxBindingRedirects.Clr2.PrivatePathLib.dll"),
            result.LoadedPath!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Satellite culture probing: the <c>fr</c> satellite resolves to
    /// <c>fr/NetFxBindingRedirects.Clr2.CulturedLib.resources.dll</c> with the identity the
    /// CLR 2 runtime actually loaded (per oracle).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_CulturedLibFrSatellite_FromCultureSubdir_MatchesOracleIdentity()
    {
        SkipIfNotWindows();
        SkipIfClr2Absent();
        var (ctx, oracle) = LoadFixture();
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.Clr2.CulturedLib.resources", "1.0.0.0", "fr", null);
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.AppLocal, result.Provenance);
        var oracleEntry = oracle["NetFxBindingRedirects.Clr2.CulturedLib.resources(fr)"];
        Assert.AreEqual(oracleEntry.Location, result.LoadedPath, ignoreCase: true);
    }

    // ---- Synthetic temp-tree tests (NOT gated on Clr2RuntimePresent) ----

    /// <summary>
    /// Deterministic bare-<c>GAC</c>-bucket coverage. Constructs a temp CLR2-shaped GAC
    /// (<c>&lt;temp&gt;\assembly\GAC\&lt;name&gt;\&lt;version&gt;__&lt;pkt&gt;\&lt;name&gt;.dll</c>)
    /// using a real signed sample assembly, builds a Clr2 binding context with that as
    /// <c>GacRoots</c>, and asserts <c>NetFxBinder.Bind</c> finds the file via the bare
    /// <c>GAC</c> bucket. Independent of any host-installed file.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_TempGac_BareGacBucket_Clr2_FindsFileWithGacProvenance()
    {
        SkipIfNotWindows();
        var sourceDll = SignedSampleDll();
        var asmName = System.Reflection.AssemblyName.GetAssemblyName(sourceDll);
        var name = asmName.Name!;
        var pkt = HexFormatToken(asmName.GetPublicKeyToken())!;
        var token = $"{asmName.Version}__{pkt}";

        using var temp = new TempDir();
        var gacBucket = Path.Combine(temp.Path, "assembly", "GAC", name, token);
        Directory.CreateDirectory(gacBucket);
        var stagedDll = Path.Combine(gacBucket, $"{name}.dll");
        File.Copy(sourceDll, stagedDll);

        var ctx = MakeSyntheticClr2Context(gacRoot: Path.Combine(temp.Path, "assembly"));
        var requested = new AssemblyRefInfo(name, asmName.Version!.ToString(), "neutral", pkt);
        var result = NetFxBinder.Bind(requested, ctx);

        Assert.AreEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.AreEqual(stagedDll, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>Same coverage as bare-GAC, but staged in <c>GAC_MSIL</c>.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_TempGac_GacMsilBucket_Clr2_FindsFile()
    {
        SkipIfNotWindows();
        AssertClr2GacBucketHit("GAC_MSIL");
    }

    /// <summary>Same coverage as bare-GAC, but staged in <c>GAC_64</c> (architecture slot).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_TempGac_Gac64Bucket_Clr2_FindsFile()
    {
        SkipIfNotWindows();
        AssertClr2GacBucketHit("GAC_64");
    }

    /// <summary>
    /// Clr2 GAC token parser: <c>2.0.0.0__b77a5c561934e089</c> (no prefix) parses; <c>v4.0_*</c>
    /// tokens are rejected so a mixed cache doesn't bleed Clr4-shaped tokens into the Clr2
    /// unification table. Validated indirectly: a v4.0_-prefixed dir under a Clr2 GAC does not
    /// produce a hit, while the no-prefix dir does.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_TempGac_V4PrefixedToken_NotHonoredInClr2Context()
    {
        SkipIfNotWindows();
        var sourceDll = SignedSampleDll();
        var asmName = System.Reflection.AssemblyName.GetAssemblyName(sourceDll);
        var name = asmName.Name!;
        var pkt = HexFormatToken(asmName.GetPublicKeyToken())!;
        var v4Token = $"v4.0_{asmName.Version}__{pkt}";

        using var temp = new TempDir();
        var gacBucket = Path.Combine(temp.Path, "assembly", "GAC_MSIL", name, v4Token);
        Directory.CreateDirectory(gacBucket);
        File.Copy(sourceDll, Path.Combine(gacBucket, $"{name}.dll"));

        var ctx = MakeSyntheticClr2Context(gacRoot: Path.Combine(temp.Path, "assembly"));
        var requested = new AssemblyRefInfo(name, asmName.Version!.ToString(), "neutral", pkt);
        var result = NetFxBinder.Bind(requested, ctx);

        // Clr2 binder builds the no-prefix token; the v4-prefixed dir doesn't match. The bind
        // falls through every path and ends Unresolved (no app-local fallback in temp setup).
        Assert.AreNotEqual(AssemblyProvenance.Gac, result.Provenance);
    }

    /// <summary>Optional opportunistic coverage: if the host has <c>stdole 7.0.3300.0</c> at
    /// <c>%WINDIR%\assembly\GAC\stdole\7.0.3300.0__b03f5f7f11d50a3a\stdole.dll</c>, assert the
    /// production GAC bare-<c>GAC</c> bucket is reachable. Skips cleanly otherwise.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Bind_StdOle_FromBareGac_WhenPresent_FindsViaGac()
    {
        SkipIfNotWindows();
        var windir = Environment.GetEnvironmentVariable("WINDIR")!;
        var stdolePath = Path.Combine(windir, "assembly", "GAC", "stdole", "7.0.3300.0__b03f5f7f11d50a3a", "stdole.dll");
        if (!File.Exists(stdolePath))
            Assert.Inconclusive("stdole not present at the canonical bare-GAC path on this host.");

        SkipIfClr2Absent();
        var (ctx, _) = LoadFixture();
        var requested = new AssemblyRefInfo("stdole", "7.0.3300.0", "neutral", "b03f5f7f11d50a3a");
        var result = NetFxBinder.Bind(requested, ctx);
        Assert.AreEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.AreEqual(stdolePath, result.LoadedPath, ignoreCase: true);
    }

    /// <summary>
    /// Publisher-policy coverage on a synthetic CLR2-shaped GAC tree. The Clr2 path's
    /// <c>appliesTo="v2.0.50727"</c> publisher policy fires, while a parallel block scoped
    /// <c>appliesTo="v4.0.30319"</c> is correctly excluded.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PublisherPolicy_TempClr2Gac_AppliesToV2_FiresAndAppliesToV4_DoesNot()
    {
        SkipIfNotWindows();
        // Build a publisher-policy assembly + .config under a temp Clr2 GAC root. The
        // BindingPolicy.LoadFrom path enumerates policy.<major>.<minor>.<simpleName>* under
        // the supplied gacRoots and reads the .config siblings.
        using var temp = new TempDir();
        var gacRoot = Path.Combine(temp.Path, "assembly");
        var policyName = "policy.1.0.Acme.Synthetic";
        var policyDir = Path.Combine(gacRoot, "GAC_MSIL", policyName, "1.0.0.0__1111111111111111");
        Directory.CreateDirectory(policyDir);

        // The .dll is a placeholder — the binder reads the .config sibling, not the assembly.
        File.WriteAllBytes(Path.Combine(policyDir, $"{policyName}.dll"), [0x4D, 0x5A]);
        File.WriteAllText(Path.Combine(policyDir, $"{policyName}.config"), """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <runtime>
                <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1" appliesTo="v2.0.50727">
                  <dependentAssembly>
                    <assemblyIdentity name="Acme.Synthetic" publicKeyToken="1111111111111111" culture="neutral" />
                    <bindingRedirect oldVersion="1.0.0.0" newVersion="3.0.0.0" />
                  </dependentAssembly>
                </assemblyBinding>
                <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1" appliesTo="v4.0.30319">
                  <dependentAssembly>
                    <assemblyIdentity name="Acme.Synthetic" publicKeyToken="1111111111111111" culture="neutral" />
                    <bindingRedirect oldVersion="1.0.0.0" newVersion="9.9.9.9" />
                  </dependentAssembly>
                </assemblyBinding>
              </runtime>
            </configuration>
            """);

        var ctx = MakeSyntheticClr2Context(gacRoot: gacRoot);
        var v2Redirects = ctx.Policy.PublisherPolicyRedirects;
        Assert.Contains(r =>
            r.Name == "Acme.Synthetic" && r.NewVersion == new Version(3, 0, 0, 0), v2Redirects);
        Assert.DoesNotContain(r =>
            r.Name == "Acme.Synthetic" && r.NewVersion == new Version(9, 9, 9, 9), v2Redirects);
    }

    // ---- Helpers ----

    private static void AssertClr2GacBucketHit(string bucket)
    {
        var sourceDll = SignedSampleDll();
        var asmName = System.Reflection.AssemblyName.GetAssemblyName(sourceDll);
        var name = asmName.Name!;
        var pkt = HexFormatToken(asmName.GetPublicKeyToken())!;
        var token = $"{asmName.Version}__{pkt}";

        using var temp = new TempDir();
        var gacBucket = Path.Combine(temp.Path, "assembly", bucket, name, token);
        Directory.CreateDirectory(gacBucket);
        var stagedDll = Path.Combine(gacBucket, $"{name}.dll");
        File.Copy(sourceDll, stagedDll);

        var ctx = MakeSyntheticClr2Context(gacRoot: Path.Combine(temp.Path, "assembly"));
        var requested = new AssemblyRefInfo(name, asmName.Version!.ToString(), "neutral", pkt);
        var result = NetFxBinder.Bind(requested, ctx);

        Assert.AreEqual(AssemblyProvenance.Gac, result.Provenance);
        Assert.AreEqual(stagedDll, result.LoadedPath, ignoreCase: true);
    }

    private static string SignedSampleDll()
    {
        // Use the CodeBaseLib.dll the fixture builds — it's strong-named with a known PKT
        // and lives under samples/NetFxBindingRedirects.Clr2.CodeBaseLib/bin/Debug/net35.
        var repoRoot = TestHelpers.GetRepoRoot();
        var dll = Path.Combine(repoRoot, "samples", "NetFxBindingRedirects.Clr2.CodeBaseLib",
            "bin", "Debug", "net35", "NetFxBindingRedirects.Clr2.CodeBaseLib.dll");
        Assert.IsTrue(File.Exists(dll), $"Signed sample DLL not built at {dll}");
        return dll;
    }

    private static NetFxBindingContext MakeSyntheticClr2Context(string gacRoot)
    {
        var policy = BindingPolicy.LoadFrom(
            appConfigPath: null,
            architecture: NetFxArchitecture.Amd64,
            gacRoots: [gacRoot],
            runtimeVersion: NetFxRuntimeVersion.Clr2);
        return new NetFxBindingContext(
            EntryAssemblyPath: Path.Combine(Path.GetTempPath(), "no-such-root.exe"),
            AppBaseDirectory: Path.GetTempPath(),
            ConfigPath: null,
            TargetFramework: null,
            EffectiveArchitecture: NetFxArchitecture.Amd64,
            Policy: policy,
            PrivatePaths: [],
            GacRoots: [gacRoot],
            RuntimeVersion: NetFxRuntimeVersion.Clr2,
            IsRuntimeVersionInferred: true);
    }

    private static string? HexFormatToken(byte[]? token)
    {
        if (token is null || token.Length == 0) return null;
        return Convert.ToHexStringLower(token);
    }

    private static (NetFxBindingContext Ctx, IReadOnlyDictionary<string, NetFxOracleEntry> Oracle) LoadFixture()
    {
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Exe);
        Assert.IsNotNull(Samples.NetFxBindingRedirectsClr2Oracle);
        var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        return (ctx!, Samples.NetFxBindingRedirectsClr2Oracle!);
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

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"dotsider-clr2-binder-{Guid.NewGuid():N}");

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }
}
