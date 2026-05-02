using System.Reflection;
using System.Runtime.InteropServices;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Oracle-parity tests for the CLR 2.0 binder. For every assembly the live CLR 2.0 runtime
/// loaded for the NetFxBindingRedirects.Clr2 sample, dotsider's <see cref="NetFxBinder.Bind"/>
/// must produce a matching <see cref="NetFxBindResult.Loaded"/> identity and
/// <see cref="NetFxBindResult.LoadedPath"/>. Any divergence is a binder bug. Mirror of
/// <see cref="NetFxBinderOracleTests"/> for the Clr2 path.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class NetFxBinderOracleClr2Tests(SampleAssemblyFixture samples)
{
    /// <summary>For every successful oracle entry, dotsider's binder produces a matching loaded
    /// identity at the same on-disk location.</summary>
    [Fact(Timeout = 60_000)]
    public void Oracle_AllNonRootRefs_DotsiderBindResultMatches()
    {
        SkipIfNotWindows();
        SkipIfOracleAbsent();

        using var analyzer = new AssemblyAnalyzer(samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        Assert.NotNull(ctx);
        Assert.Equal(NetFxRuntimeVersion.Clr2, ctx!.RuntimeVersion);

        foreach (var (key, entry) in samples.NetFxBindingRedirectsClr2Oracle!)
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
        SkipIfOracleAbsent();

        var entry = samples.NetFxBindingRedirectsClr2Oracle!["NetFxBindingRedirects.Clr2.MissingCodeBase"];
        Assert.False(entry.Loaded);

        using var analyzer = new AssemblyAnalyzer(samples.NetFxBindingRedirectsClr2Exe!);
        var ctx = NetFxBindingContext.TryBuild(analyzer);
        var requested = new AssemblyRefInfo(
            "NetFxBindingRedirects.Clr2.MissingCodeBase", "9.9.9.9", "neutral", "0123456789abcdef");
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

    private void SkipIfOracleAbsent()
    {
        if (samples.NetFxBindingRedirectsClr2Oracle is null)
            Assert.Skip("CLR 2 oracle was not captured (CLR 2 runtime not installed on this host).");
    }
}
