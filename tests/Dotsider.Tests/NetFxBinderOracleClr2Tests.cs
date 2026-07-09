using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Oracle-parity tests for the CLR 2.0 binder. For every assembly the live CLR 2.0 runtime
/// loaded for the NetFxBindingRedirects.Clr2 sample, dotsider's <see cref="NetFxBinder.Bind"/>
/// must produce a matching <see cref="NetFxBindResult.Loaded"/> identity and
/// <see cref="NetFxBindResult.LoadedPath"/>. Any divergence is a binder bug. Mirror of
/// <see cref="NetFxBinderOracleTests"/> for the Clr2 path.
/// </summary>
[TestClass]
public sealed class NetFxBinderOracleClr2Tests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>For every successful oracle entry, dotsider's binder produces a matching loaded
    /// identity at the same on-disk location.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Oracle_AllNonRootRefs_DotsiderBindResultMatches()
    {
        SkipIfNotWindows();
        SkipIfOracleAbsent();

        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);
        Assert.AreEqual(NetFxRuntimeVersion.Clr2, ctx!.RuntimeVersion);

        foreach (var (key, entry) in Samples.NetFxBindingRedirectsClr2Oracle!)
        {
            // Skip the deliberately-broken codeBase entry — covered by the negative test below.
            if (key == "NetFxBindingRedirects.Clr2.MissingCodeBase") continue;
            if (!entry.Loaded) continue;

            var asmName = new AssemblyName(entry.FullName);
            var requested = new AssemblyRefInfo(
                Name: asmName.Name ?? key,
                Version: asmName.Version?.ToString() ?? string.Empty,
                Culture: string.IsNullOrEmpty(asmName.CultureName) ? "neutral" : asmName.CultureName,
                PublicKeyToken: HexFormatToken(asmName.GetPublicKeyToken()));

            var bind = NetFxBinder.Bind(requested, ctx);

            Assert.IsNotNull(bind.Loaded);
            Assert.AreEqual(asmName.Name, bind.Loaded!.Name);
            Assert.IsNotNull(asmName.Version);
            Assert.IsNotNull(bind.Loaded.Version);
            Assert.AreEqual(asmName.Version.ToString(), bind.Loaded.Version);
            Assert.IsNotNull(bind.LoadedPath);
            Assert.AreEqual(entry.Location, bind.LoadedPath, ignoreCase: true);
        }
    }

    /// <summary>The deliberately-broken codeBase entry stays a hard miss for both oracle and binder.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Oracle_MissingCodeBase_BothOracleAndBinderReportMiss()
    {
        SkipIfNotWindows();
        SkipIfOracleAbsent();

        var entry = Samples.NetFxBindingRedirectsClr2Oracle!["NetFxBindingRedirects.Clr2.MissingCodeBase"];
        Assert.IsFalse(entry.Loaded);

        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.Clr2.MissingCodeBase", "9.9.9.9", "neutral", "0123456789abcdef");
        var bind = NetFxBinder.Bind(requested, ctx!);
        Assert.AreEqual(AssemblyProvenance.CodeBaseMissing, bind.Provenance);
    }

    private static string? HexFormatToken(byte[]? token)
    {
        if (token is null || token.Length == 0) return null;
        return Convert.ToHexStringLower(token);
    }

    private static void SkipIfNotWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Inconclusive("Test requires Windows (.NET Framework binder).");
    }

    private static void SkipIfOracleAbsent()
    {
        if (Samples.NetFxBindingRedirectsClr2Oracle is null)
            Assert.Inconclusive("CLR 2 oracle was not captured (CLR 2 runtime not installed on this host).");
    }
}
