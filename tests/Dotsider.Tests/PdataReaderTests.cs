using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="PdataReader"/>, the PE <c>.pdata</c> function-boundary fallback, using
/// synthetic PE images so both the x64 and ARM64 layouts are exercised on every platform.
/// </summary>
public class PdataReaderTests
{
    /// <summary>
    /// Verifies x64 RUNTIME_FUNCTION entries yield sized boundaries, and a chained fragment is
    /// folded away rather than counted as its own function.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadBoundaries_Amd64_EmitsFunctionsAndFoldsChained()
    {
        var section = new byte[0x200];
        // Two RUNTIME_FUNCTIONs at RVA 0x1000; unwind info at RVA 0x1100/0x1101.
        SyntheticImageBuilders.Amd64RuntimeFunction(0x2000, 0x2040, 0x1100).CopyTo(section, 0);
        SyntheticImageBuilders.Amd64RuntimeFunction(0x2040, 0x2080, 0x1101).CopyTo(section, 12);
        section[0x100] = 0x01;                       // version 1, flags 0 (standalone)
        section[0x101] = 0x01 | (0x4 << 3);          // version 1, flags = CHAININFO

        var pe = SyntheticImageBuilders.BuildPe(0x8664, section, exceptionRva: 0x1000, exceptionSize: 24);

        var boundaries = PdataReader.ReadBoundaries(pe);

        var only = Assert.Single(boundaries);
        Assert.Equal("sub_2000", only.Name);
        Assert.Equal(0x40, only.Size);
        Assert.Equal(NativeSymbolKind.Boundary, Build(only).Symbols[0].Kind);
    }

    /// <summary>
    /// Verifies an ARM64 packed unwind entry decodes its function length from the packed word.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadBoundaries_Arm64Packed_DecodesFunctionLength()
    {
        var section = new byte[0x200];
        // begin RVA 0x2000; packed unwind: flag bits set, FunctionLength = 0x10 words = 0x40 bytes.
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(section, 0x2000);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(section.AsSpan(4), (0x10u << 2) | 0x1u);

        var pe = SyntheticImageBuilders.BuildPe(0xAA64, section, exceptionRva: 0x1000, exceptionSize: 8);

        var boundaries = PdataReader.ReadBoundaries(pe);

        var only = Assert.Single(boundaries);
        Assert.Equal(0x40, only.Size);
    }

    /// <summary>
    /// Verifies a PE without an exception directory yields no boundaries.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadBoundaries_NoExceptionDirectory_ReturnsEmpty()
    {
        var pe = SyntheticImageBuilders.BuildPe(0x8664, new byte[0x200], exceptionRva: 0, exceptionSize: 0);

        Assert.Empty(PdataReader.ReadBoundaries(pe));
    }

    private static NativeSymbolInfo Build(RawNativeSymbol s) =>
        NativeSymbolReader.Build([s], new IlcNameDemangler([]),
            NativeSymbolSource.PdataFallback, NativeSymbolStatus.FallbackOnly, null, null);
}
