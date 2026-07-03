using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the merge, dedup, sizing, and classification core of <see cref="NativeSymbolReader"/>
/// (its <c>Build</c> step), driven with synthetic raw symbols so the rules are pinned independently
/// of any format reader.
/// </summary>
public class NativeSymbolMergeTests
{
    private static readonly IlcNameDemangler EmptyDemangler = new([]);

    private static RawNativeSymbol Raw(
        string name, ulong va, long size, bool isData = false, bool isBoundary = false,
        string? file = null, int? line = null) =>
        new(name, va, (uint)va, (long)va, ".text", size, isData, isBoundary, file, line);

    private static NativeSymbolInfo Build(params RawNativeSymbol[] raw) =>
        NativeSymbolReader.Build(raw, EmptyDemangler, NativeSymbolSource.NativePdb,
            NativeSymbolStatus.Loaded, "x.pdb", null);

    /// <summary>
    /// Verifies two records at the same address collapse to one symbol, the richer record's size
    /// wins, and the other name becomes an alias — so the address is never counted twice.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_SameAddress_MergesToPrimaryWithAlias()
    {
        var info = Build(
            Raw("proc_with_size", 0x1000, 0x40),
            Raw("public_no_size", 0x1000, 0));

        var symbol = Assert.Single(info.Symbols);
        Assert.Equal("proc_with_size", symbol.Name);
        Assert.Equal(0x40, symbol.Size);
        Assert.Contains("public_no_size", symbol.Aliases);
    }

    /// <summary>
    /// Verifies an unsized symbol is sized by the distance to the next symbol's address.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_UnsizedSymbol_SizedByNextAddress()
    {
        var info = Build(
            Raw("a", 0x1000, 0),
            Raw("b", 0x1050, 0));

        Assert.Equal(0x50, info.Symbols[0].Size);
    }

    /// <summary>
    /// Verifies the merged set has no two symbols at the same address — the Size Map sums this set,
    /// so a duplicate would double-count.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_Result_HasUniqueAddresses()
    {
        var info = Build(
            Raw("a", 0x1000, 0x10),
            Raw("a_alias", 0x1000, 0),
            Raw("b", 0x1020, 0x10));

        Assert.Equal(info.Symbols.Count, info.Symbols.Select(s => s.VirtualAddress).Distinct().Count());
    }

    /// <summary>
    /// Verifies a boundary record classifies as a boundary with no managed name.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_Boundary_ClassifiesAsBoundary()
    {
        var info = Build(Raw("sub_1000", 0x1000, 0x10, isBoundary: true));

        var symbol = Assert.Single(info.Symbols);
        Assert.Equal(NativeSymbolKind.Boundary, symbol.Kind);
        Assert.Null(symbol.ManagedName);
    }

    /// <summary>
    /// Verifies an unrecognized data-section record is dropped — unrelated globals and import
    /// thunks would otherwise inflate the Size Map's data categories — while a recognized ILC
    /// node at the same footing survives.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_UnrecognizedDataSymbol_Dropped()
    {
        var info = Build(
            Raw("__imp_GetLastError", 0x2000, 0x08, isData: true),
            Raw("_ZTV6Widget", 0x2010, 0x18, isData: true));

        var symbol = Assert.Single(info.Symbols);
        Assert.Equal("_ZTV6Widget", symbol.Name);
        Assert.Equal(NativeSymbolKind.MethodTable, symbol.Kind);
    }

    /// <summary>
    /// Verifies a data record joined to a managed name is kept and classified as data rather
    /// than dropped with the unrecognized ones.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_ManagedJoinedDataSymbol_KeptAsData()
    {
        var demangler = new IlcNameDemangler([new RecoveredType("System.Foo", ["Bar"], "System.Private.CoreLib")]);
        var info = NativeSymbolReader.Build(
            [Raw("S_P_CoreLib_System_Foo__Bar", 0x2000, 0x10, isData: true)],
            demangler, NativeSymbolSource.NativePdb, NativeSymbolStatus.Loaded, "x.pdb", null);

        var symbol = Assert.Single(info.Symbols);
        Assert.Equal(NativeSymbolKind.Data, symbol.Kind);
        Assert.Equal("System.Foo.Bar", symbol.ManagedName);
    }

    /// <summary>
    /// Verifies a merged data group whose primary is unrecognized re-fronts a recognized alias
    /// instead of dropping the address: the node name leads and the old primary becomes an alias.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_UnrecognizedDataPrimary_PromotesRecognizedAlias()
    {
        var info = Build(
            Raw("crt_state", 0x2000, 0x18, isData: true),
            Raw("_ZTV6Widget", 0x2000, 0, isData: true));

        var symbol = Assert.Single(info.Symbols);
        Assert.Equal("_ZTV6Widget", symbol.Name);
        Assert.Equal(NativeSymbolKind.MethodTable, symbol.Kind);
        Assert.Equal(0x18, symbol.Size);
        Assert.Contains("crt_state", symbol.Aliases);
    }

    /// <summary>
    /// Verifies overlapping extents clip to the next symbol's start so the Size Map never
    /// counts a byte twice.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_OverlappingRanges_ClipToNextStart()
    {
        var info = Build(
            Raw("a", 0x1000, 0x100),
            Raw("b", 0x1050, 0x20));

        Assert.Equal(0x50, info.Symbols[0].Size);
        Assert.Equal(0x20, info.Symbols[1].Size);
    }

    /// <summary>
    /// Verifies an unsized symbol is not sized across a section boundary: the gap to a symbol
    /// in a different section says nothing about its extent.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_UnsizedSymbol_NotSizedAcrossSections()
    {
        var raw = new RawNativeSymbol[]
        {
            new("last_in_text", 0x1000, 0x1000, 0x1000, ".text", 0, false, false, null, null),
            new("_ZTV6Widget", 0x9000, 0x9000, 0x9000, ".data", 0x10, true, false, null, null),
        };
        var info = NativeSymbolReader.Build(raw, EmptyDemangler, NativeSymbolSource.NativePdb,
            NativeSymbolStatus.Loaded, "x.pdb", null);

        Assert.Equal(0, info.Symbols[0].Size);
    }

    /// <summary>
    /// Verifies the demangler is applied to primaries: a symbol matching a recovered type/method
    /// gets its managed name, and the interval lookup resolves an address inside it.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_AppliesDemanglerAndSupportsIntervalLookup()
    {
        var demangler = new IlcNameDemangler([new RecoveredType("System.Foo", ["Bar"], "System.Private.CoreLib")]);
        var info = NativeSymbolReader.Build(
            [Raw("S_P_CoreLib_System_Foo__Bar", 0x3000, 0x30)],
            demangler, NativeSymbolSource.NativePdb, NativeSymbolStatus.Loaded, "x.pdb", null);

        Assert.Equal("System.Foo.Bar", info.Symbols[0].ManagedName);
        Assert.True(info.Symbols[0].IsExactMatch);
        Assert.True(info.TryFindByAddress(0x3010, out var hit));
        Assert.Equal("S_P_CoreLib_System_Foo__Bar", hit.Name);
        Assert.False(info.TryFindByAddress(0x3040, out _));
    }

    /// <summary>
    /// Verifies an empty input carries the status and diagnostic so an empty result is explainable.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_Empty_CarriesStatus()
    {
        var info = NativeSymbolReader.Build([], EmptyDemangler, NativeSymbolSource.PdataFallback,
            NativeSymbolStatus.NoSymbolFile, null, "no symbol file beside the binary");

        Assert.Empty(info.Symbols);
        Assert.Equal(NativeSymbolStatus.NoSymbolFile, info.Status);
        Assert.Equal("no symbol file beside the binary", info.Diagnostic);
    }
}
