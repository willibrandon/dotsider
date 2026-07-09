using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Oracle-parity tests. For every assembly the .NET Framework runtime actually loaded for the
/// NetFxBindingRedirects sample, dotsider's <see cref="NetFxBinder.Bind"/> must produce a
/// matching <see cref="NetFxBindResult.Loaded"/> identity and <see cref="NetFxBindResult.LoadedPath"/>.
/// Any divergence is a binder bug, not a documentation note. This is the literal-accuracy gate.
/// </summary>
[TestClass]
public sealed class NetFxBinderOracleTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// For every successful oracle entry, dotsider's binder produces a matching loaded
    /// identity at the same on-disk location.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Oracle_AllNonRootRefs_DotsiderBindResultMatches()
    {
        SkipIfNotWindows();
        Assert.IsNotNull(Samples.NetFxBindingRedirectsExe);
        Assert.IsNotNull(Samples.NetFxBindingRedirectsOracle);

        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.IsNotNull(ctx);

        foreach (var (key, entry) in Samples.NetFxBindingRedirectsOracle!)
        {
            // Skip the deliberately-broken codeBase entry — that's covered by the negative test.
            if (key == "NetFxBindingRedirects.MissingCodeBase") continue;
            // Skip the sample's own root identity entries that the oracle records.
            if (!entry.Loaded) continue;

            var asmName = new AssemblyName(entry.FullName);
            var requested = new AssemblyRefInfo(
                Name: asmName.Name ?? key,
                Version: asmName.Version?.ToString() ?? string.Empty,
                Culture: string.IsNullOrEmpty(asmName.CultureName) ? "neutral" : asmName.CultureName,
                PublicKeyToken: HexFormatToken(asmName.GetPublicKeyToken()));

            var bind = NetFxBinder.Bind(requested, ctx!);
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
        Assert.IsNotNull(Samples.NetFxBindingRedirectsOracle);
        var entry = Samples.NetFxBindingRedirectsOracle!["NetFxBindingRedirects.MissingCodeBase"];
        Assert.IsFalse(entry.Loaded);

        using var analyzer = new AssemblyAnalyzer(Samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.MissingCodeBase", "9.9.9.9", "neutral", "0123456789abcdef");
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
}
