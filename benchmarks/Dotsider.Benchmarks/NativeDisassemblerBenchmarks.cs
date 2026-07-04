using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="NativeDisassembler"/>: whole-region throughput over a large buffer of
/// representative code, and the single-function <see cref="NativeDisassembler.DisassembleWithText"/>
/// hot path invoked on every UI selection — for both x86-64 and AArch64.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class NativeDisassemblerBenchmarks
{
    private byte[] _x64Region = null!;
    private byte[] _x64Function = null!;
    private byte[] _arm64Region = null!;

    /// <summary>Builds large representative x64 and A64 code regions plus one hot function.</summary>
    [GlobalSetup]
    public void Setup()
    {
        // A realistic x86-64 instruction mix (prologue, moves, arithmetic, SSE, a call, ret).
        byte[] x64Seq =
        [
            0x55, 0x48, 0x89, 0xE5, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x89, 0x4D, 0xF8,
            0x8B, 0x45, 0xF8, 0x03, 0x45, 0xFC, 0x0F, 0x28, 0xC1, 0x66, 0x0F, 0xEF, 0xC2,
            0xE8, 0x00, 0x00, 0x00, 0x00, 0x48, 0x83, 0xC4, 0x20, 0x5D, 0xC3,
        ];
        _x64Function = x64Seq;
        _x64Region = Tile(x64Seq, 256 * 1024);

        // A realistic A64 word sequence (stp/mov/add/mul/ldr/bl/ret).
        uint[] a64Words =
        [
            0xA9BF7BFD, 0x910003FD, 0xD2800020, 0x8B020020, 0x9B027C20, 0xF9400020,
            0x94000004, 0xA8C17BFD, 0xD65F03C0,
        ];
        var a64 = new byte[a64Words.Length * 4];
        for (var i = 0; i < a64Words.Length; i++)
            BitConverter.GetBytes(a64Words[i]).CopyTo(a64, i * 4);
        _arm64Region = Tile(a64, 256 * 1024);
    }

    private static byte[] Tile(byte[] seq, int targetBytes)
    {
        var buffer = new byte[targetBytes];
        for (var i = 0; i < targetBytes; i++)
            buffer[i] = seq[i % seq.Length];
        return buffer;
    }

    /// <summary>Decodes a 256 KB x86-64 region to instructions.</summary>
    [Benchmark(Description = "x64 Disassemble 256KB")]
    public int X64_Region()
        => NativeDisassembler.Disassemble(_x64Region, 0x140001000, NativeArchitecture.X64).Count;

    /// <summary>Decodes a 256 KB AArch64 region to instructions.</summary>
    [Benchmark(Description = "arm64 Disassemble 256KB")]
    public int Arm64_Region()
        => NativeDisassembler.Disassemble(_arm64Region, 0x100000, NativeArchitecture.Arm64).Count;

    /// <summary>Measures the single-function DisassembleWithText hot path (UI selection).</summary>
    [Benchmark(Description = "x64 DisassembleWithText single function")]
    [BenchmarkCategory("SingleFunction")]
    public int X64_Function()
        => NativeDisassembler.DisassembleWithText(_x64Function, 0x140001000, NativeArchitecture.X64).Text.Length;
}
