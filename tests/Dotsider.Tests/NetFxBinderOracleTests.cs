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
[Collection("SampleAssemblies")]
public sealed class NetFxBinderOracleTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// For every successful oracle entry, dotsider's binder produces a matching loaded
    /// identity at the same on-disk location.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void Oracle_AllNonRootRefs_DotsiderBindResultMatches()
    {
        SkipIfNotWindows();
        Assert.NotNull(samples.NetFxBindingRedirectsExe);
        Assert.NotNull(samples.NetFxBindingRedirectsOracle);

        using var analyzer = new AssemblyAnalyzer(samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.NotNull(ctx);

        foreach (var (key, entry) in samples.NetFxBindingRedirectsOracle!)
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
            Assert.NotNull(bind.Loaded);
            Assert.Equal(asmName.Name, bind.Loaded!.Name);
            Assert.Equal(asmName.Version?.ToString(), bind.Loaded.Version);
            Assert.NotNull(bind.LoadedPath);
            Assert.Equal(entry.Location, bind.LoadedPath, ignoreCase: true);
        }
    }

    /// <summary>The deliberately-broken codeBase entry stays a hard miss for both oracle and binder.</summary>
    [Fact(Timeout = 30_000)]
    public void Oracle_MissingCodeBase_BothOracleAndBinderReportMiss()
    {
        SkipIfNotWindows();
        Assert.NotNull(samples.NetFxBindingRedirectsOracle);
        var entry = samples.NetFxBindingRedirectsOracle!["NetFxBindingRedirects.MissingCodeBase"];
        Assert.False(entry.Loaded);

        using var analyzer = new AssemblyAnalyzer(samples.NetFxBindingRedirectsExe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.MissingCodeBase", "9.9.9.9", "neutral", "0123456789abcdef");
        var bind = NetFxBinder.Bind(requested, ctx!);
        Assert.Equal(AssemblyProvenance.CodeBaseMissing, bind.Provenance);
    }

    private static string? HexFormatToken(byte[]? token)
    {
        if (token is null || token.Length == 0) return null;
        return Convert.ToHexStringLower(token);
    }

    private static void SkipIfNotWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Skip("Test requires Windows (.NET Framework binder).");
    }
}
