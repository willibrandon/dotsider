namespace Dotsider.Core.Analysis.Disasm.x64;

using F = OpFlags;
using K = OperandKind;

/// <summary>
/// Registers the EVEX-only AVX-512 / AVX10 / VNNI opcodes — the ones without a legacy or VEX form:
/// ternary logic, VNNI dot-products, conflict/leading-zero counts, cross-lane permutes, compress/
/// expand, scale/exponent/mantissa, integer compares that write an opmask, and the VEX-encoded
/// opmask moves and logic. The AVX-512 encodings of the shared SSE/AVX opcodes are served by the
/// shared rows (with EVEX adding the <c>d/q</c> width suffix, <c>{k}{z}</c> masking, and broadcast
/// in the decoder). Ops flagged <see cref="OpFlags.EvexDQ"/> take a W-selected <c>d</c>/<c>q</c>.
/// </summary>
internal static partial class XarchTables
{
    private static void RegisterAvx512()
    {
        // VNNI dot-products.
        Row(Map0F38, Pp66, 0x50, "vpdpbusd", K.Vx, K.Hx, K.Wx, flags: F.NoVexPrefix);
        Row(Map0F38, Pp66, 0x51, "vpdpbusds", K.Vx, K.Hx, K.Wx, flags: F.NoVexPrefix);
        Row(Map0F38, Pp66, 0x52, "vpdpwssd", K.Vx, K.Hx, K.Wx, flags: F.NoVexPrefix);
        Row(Map0F38, Pp66, 0x53, "vpdpwssds", K.Vx, K.Hx, K.Wx, flags: F.NoVexPrefix);

        // Conflict detection / leading zeros (AVX512CD).
        Row(Map0F38, Pp66, 0x44, "plzcnt", K.Vx, K.Wx, flags: F.EvexDQ);
        Row(Map0F38, Pp66, 0xC4, "pconflict", K.Vx, K.Wx, flags: F.EvexDQ);

        // Scale / exponent (ps/pd by W).
        Row(Map0F38, Pp66, 0x2C, "scalefps", K.Vx, K.Hx, K.Wx);
        Row(Map0F38, Pp66, 0x42, "getexpps", K.Vx, K.Wx);

        // Two-source permutes (AVX512F/VBMI).
        Row(Map0F38, Pp66, 0x75, "permi2b", K.Vx, K.Hx, K.Wx, flags: F.NoVexPrefix);
        Row(Map0F38, Pp66, 0x76, "permi2", K.Vx, K.Hx, K.Wx, flags: F.EvexDQ);
        Row(Map0F38, Pp66, 0x7D, "permt2b", K.Vx, K.Hx, K.Wx, flags: F.NoVexPrefix);
        Row(Map0F38, Pp66, 0x7E, "permt2", K.Vx, K.Hx, K.Wx, flags: F.EvexDQ);
        Row(Map0F38, Pp66, 0x8D, "permb", K.Vx, K.Hx, K.Wx, flags: F.NoVexPrefix);

        // Compress / expand.
        Row(Map0F38, Pp66, 0x89, "pexpand", K.Vx, K.Wx, flags: F.EvexDQ);
        Row(Map0F38, Pp66, 0x8B, "pcompress", K.Wx, K.Vx, flags: F.EvexDQ);

        // Ternary logic and align.
        Row(Map0F3A, Pp66, 0x25, "pternlog", K.Vx, K.Hx, K.Wx, K.Ib, flags: F.EvexDQ);
        Row(Map0F3A, Pp66, 0x03, "palign", K.Vx, K.Hx, K.Wx, K.Ib, flags: F.EvexDQ);
        Row(Map0F3A, Pp66, 0x54, "fixupimm", K.Vx, K.Hx, K.Wx, K.Ib, flags: F.EvexDQ);

        // Integer compares that write an opmask (dest is k).
        Row(Map0F3A, Pp66, 0x1F, "pcmp", K.Kr, K.Hx, K.Wx, K.Ib, flags: F.EvexDQ);
        Row(Map0F3A, Pp66, 0x1E, "pcmpu", K.Kr, K.Hx, K.Wx, K.Ib, flags: F.EvexDQ);

        // Round-scale (0F3A 08-0B) shares the SSE round* slots; the decoder renames round→rndscale
        // under EVEX. Get-mantissa opcodes are EVEX-only.
        Row(Map0F3A, Pp66, 0x26, "getmantps", K.Vx, K.Wx, K.Ib);
        Row(Map0F3A, Pp66, 0x27, "getmantpd", K.Vx, K.Wx, K.Ib);

        RegisterOpmask();
    }

    private static void RegisterOpmask()
    {
        // VEX-encoded opmask moves and logic on k registers. These 0F opcodes are shared with the
        // legacy setcc/cmovcc slots, so they live in a VEX-only side table the decoder consults when
        // a VEX prefix is present (see TryKmask); the width suffix is by pp (W/B/D/Q).
        Kmask(PpNone, 0x90, "kmovw", K.Kr, K.Km);
        Kmask(Pp66, 0x90, "kmovb", K.Kr, K.Km);
        Kmask(PpNone, 0x91, "kmovw", K.Km, K.Kr);
        Kmask(Pp66, 0x91, "kmovb", K.Km, K.Kr);
        Kmask(PpF2, 0x92, "kmovd", K.Kr, K.Ed);
        Kmask(PpNone, 0x92, "kmovw", K.Kr, K.Ed);
        Kmask(PpF2, 0x93, "kmovd", K.Gd, K.Km);
        Kmask(PpNone, 0x41, "kandw", K.Kr, K.Kv, K.Km);
        Kmask(PpNone, 0x42, "kandnw", K.Kr, K.Kv, K.Km);
        Kmask(PpNone, 0x45, "korw", K.Kr, K.Kv, K.Km);
        Kmask(PpNone, 0x46, "kxnorw", K.Kr, K.Kv, K.Km);
        Kmask(PpNone, 0x47, "kxorw", K.Kr, K.Kv, K.Km);
        Kmask(PpNone, 0x4A, "kaddw", K.Kr, K.Kv, K.Km);
        Kmask(PpNone, 0x4B, "kunpckwd", K.Kr, K.Kv, K.Km);
        Kmask(PpNone, 0x44, "knotw", K.Kr, K.Km);
        Kmask(PpNone, 0x98, "kortestw", K.Kr, K.Km);
        Kmask(PpNone, 0x99, "ktestw", K.Kr, K.Km);
    }

    private static void Kmask(int pp, int opcode, string mnemonic,
        K o1 = K.None, K o2 = K.None, K o3 = K.None) =>
        KmaskOps[(pp << 8) | opcode] = new OpEntry(mnemonic, o1, o2, o3, K.None, F.NoVexPrefix, 0);
}
