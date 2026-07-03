namespace Dotsider.Core.Analysis.Disasm.x64;

using K = OperandKind;

/// <summary>
/// Registers the SSE, SSE2, SSE3, SSSE3, SSE4.1, and SSE4.2 opcodes over the 0F, 0F 38, and
/// 0F 3A maps, keyed by their mandatory prefix (none / 66 / F3 / F2). The same rows are reused for
/// the VEX (AVX) and EVEX (AVX-512) encodings by their family files, since the prefix and map
/// collapse onto this key.
/// </summary>
internal static partial class XarchTables
{
    private static void RegisterSse()
    {
        RegisterSseMoves();
        RegisterSseArithmetic();
        RegisterSseConvert();
        RegisterSseLogicalCompare();
        RegisterSsePackedInteger();
        RegisterSse38();
        RegisterSse3A();
    }

    private static void RegisterSseMoves()
    {
        // 10/11 mov[u]ps/pd/ss/sd.
        Row(Map0F, PpNone, 0x10, "movups", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x10, "movupd", K.Vx, K.Wx);
        Row(Map0F, PpF3, 0x10, "movss", K.Vx, K.Wss);
        Row(Map0F, PpF2, 0x10, "movsd", K.Vx, K.Wsd);
        Row(Map0F, PpNone, 0x11, "movups", K.Wx, K.Vx);
        Row(Map0F, Pp66, 0x11, "movupd", K.Wx, K.Vx);
        Row(Map0F, PpF3, 0x11, "movss", K.Wss, K.Vx);
        Row(Map0F, PpF2, 0x11, "movsd", K.Wsd, K.Vx);

        Row(Map0F, PpNone, 0x12, "movlps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x12, "movlpd", K.Vx, K.Wsd);
        Row(Map0F, PpF3, 0x12, "movsldup", K.Vx, K.Wx);
        Row(Map0F, PpF2, 0x12, "movddup", K.Vx, K.Wsd);
        Row(Map0F, PpNone, 0x13, "movlps", K.Wx, K.Vx);
        Row(Map0F, Pp66, 0x13, "movlpd", K.Wsd, K.Vx);
        Row(Map0F, PpNone, 0x14, "unpcklps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x14, "unpcklpd", K.Vx, K.Wx);
        Row(Map0F, PpNone, 0x15, "unpckhps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x15, "unpckhpd", K.Vx, K.Wx);
        Row(Map0F, PpNone, 0x16, "movhps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x16, "movhpd", K.Vx, K.Wsd);
        Row(Map0F, PpF3, 0x16, "movshdup", K.Vx, K.Wx);
        Row(Map0F, PpNone, 0x17, "movhps", K.Wx, K.Vx);
        Row(Map0F, Pp66, 0x17, "movhpd", K.Wsd, K.Vx);

        Row(Map0F, PpNone, 0x28, "movaps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x28, "movapd", K.Vx, K.Wx);
        Row(Map0F, PpNone, 0x29, "movaps", K.Wx, K.Vx);
        Row(Map0F, Pp66, 0x29, "movapd", K.Wx, K.Vx);

        // movd/movq to/from GPR.
        Row(Map0F, Pp66, 0x6E, "movd", K.Vx, K.Ev);
        Row(Map0F, Pp66, 0x7E, "movd", K.Ev, K.Vx);
        Row(Map0F, PpF3, 0x7E, "movq", K.Vx, K.Wsd);
        Row(Map0F, Pp66, 0xD6, "movq", K.Wsd, K.Vx);

        // movdqa/movdqu.
        Row(Map0F, Pp66, 0x6F, "movdqa", K.Vx, K.Wx);
        Row(Map0F, PpF3, 0x6F, "movdqu", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x7F, "movdqa", K.Wx, K.Vx);
        Row(Map0F, PpF3, 0x7F, "movdqu", K.Wx, K.Vx);

        // movmskps/pd, pmovmskb.
        Row(Map0F, PpNone, 0x50, "movmskps", K.Gd, K.Wx);
        Row(Map0F, Pp66, 0x50, "movmskpd", K.Gd, K.Wx);
        Row(Map0F, Pp66, 0xD7, "pmovmskb", K.Gd, K.Wx);

        // non-temporal stores.
        Row(Map0F, PpNone, 0x2B, "movntps", K.Wx, K.Vx);
        Row(Map0F, Pp66, 0x2B, "movntpd", K.Wx, K.Vx);
        Row(Map0F, Pp66, 0xE7, "movntdq", K.Wx, K.Vx);
        Row(Map0F, PpF2, 0xF0, "lddqu", K.Vx, K.Wx);
    }

    private static void RegisterSseArithmetic()
    {
        Packed("add", 0x58);
        Packed("mul", 0x59);
        Packed("sub", 0x5C);
        Packed("min", 0x5D);
        Packed("div", 0x5E);
        Packed("max", 0x5F);
        Row(Map0F, PpNone, 0x51, "sqrtps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x51, "sqrtpd", K.Vx, K.Wx);
        Row(Map0F, PpF3, 0x51, "sqrtss", K.Vx, K.Wss);
        Row(Map0F, PpF2, 0x51, "sqrtsd", K.Vx, K.Wsd);
        Row(Map0F, PpNone, 0x52, "rsqrtps", K.Vx, K.Wx);
        Row(Map0F, PpF3, 0x52, "rsqrtss", K.Vx, K.Wss);
        Row(Map0F, PpNone, 0x53, "rcpps", K.Vx, K.Wx);
        Row(Map0F, PpF3, 0x53, "rcpss", K.Vx, K.Wss);
        Row(Map0F, PpF2, 0x7C, "haddps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x7C, "haddpd", K.Vx, K.Wx);
        Row(Map0F, PpF2, 0x7D, "hsubps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x7D, "hsubpd", K.Vx, K.Wx);
        Row(Map0F, PpF2, 0xD0, "addsubps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0xD0, "addsubpd", K.Vx, K.Wx);
    }

    private static void Packed(string op, int opcode)
    {
        Row(Map0F, PpNone, opcode, op + "ps", K.Vx, K.Wx);
        Row(Map0F, Pp66, opcode, op + "pd", K.Vx, K.Wx);
        Row(Map0F, PpF3, opcode, op + "ss", K.Vx, K.Wss);
        Row(Map0F, PpF2, opcode, op + "sd", K.Vx, K.Wsd);
    }

    private static void RegisterSseConvert()
    {
        Row(Map0F, PpF3, 0x2A, "cvtsi2ss", K.Vx, K.Ev);
        Row(Map0F, PpF2, 0x2A, "cvtsi2sd", K.Vx, K.Ev);
        Row(Map0F, PpF3, 0x2C, "cvttss2si", K.Gy, K.Wss);
        Row(Map0F, PpF2, 0x2C, "cvttsd2si", K.Gy, K.Wsd);
        Row(Map0F, PpF3, 0x2D, "cvtss2si", K.Gy, K.Wss);
        Row(Map0F, PpF2, 0x2D, "cvtsd2si", K.Gy, K.Wsd);
        Row(Map0F, PpNone, 0x5A, "cvtps2pd", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x5A, "cvtpd2ps", K.Vx, K.Wx);
        Row(Map0F, PpF3, 0x5A, "cvtss2sd", K.Vx, K.Wss);
        Row(Map0F, PpF2, 0x5A, "cvtsd2ss", K.Vx, K.Wsd);
        Row(Map0F, PpNone, 0x5B, "cvtdq2ps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x5B, "cvtps2dq", K.Vx, K.Wx);
        Row(Map0F, PpF3, 0x5B, "cvttps2dq", K.Vx, K.Wx);
        Row(Map0F, PpNone, 0xE6, "cvtdq2pd", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0xE6, "cvttpd2dq", K.Vx, K.Wx);
        Row(Map0F, PpF3, 0xE6, "cvtdq2pd", K.Vx, K.Wx);
        Row(Map0F, PpNone, 0x2E, "ucomiss", K.Vx, K.Wss);
        Row(Map0F, Pp66, 0x2E, "ucomisd", K.Vx, K.Wsd);
        Row(Map0F, PpNone, 0x2F, "comiss", K.Vx, K.Wss);
        Row(Map0F, Pp66, 0x2F, "comisd", K.Vx, K.Wsd);
    }

    private static void RegisterSseLogicalCompare()
    {
        Row(Map0F, PpNone, 0x54, "andps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x54, "andpd", K.Vx, K.Wx);
        Row(Map0F, PpNone, 0x55, "andnps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x55, "andnpd", K.Vx, K.Wx);
        Row(Map0F, PpNone, 0x56, "orps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x56, "orpd", K.Vx, K.Wx);
        Row(Map0F, PpNone, 0x57, "xorps", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x57, "xorpd", K.Vx, K.Wx);

        Row(Map0F, PpNone, 0xC2, "cmpps", K.Vx, K.Wx, K.Ib);
        Row(Map0F, Pp66, 0xC2, "cmppd", K.Vx, K.Wx, K.Ib);
        Row(Map0F, PpF3, 0xC2, "cmpss", K.Vx, K.Wss, K.Ib);
        Row(Map0F, PpF2, 0xC2, "cmpsd", K.Vx, K.Wsd, K.Ib);
        Row(Map0F, PpNone, 0xC6, "shufps", K.Vx, K.Wx, K.Ib);
        Row(Map0F, Pp66, 0xC6, "shufpd", K.Vx, K.Wx, K.Ib);
    }

    private static void RegisterSsePackedInteger()
    {
        // Shuffles.
        Row(Map0F, Pp66, 0x70, "pshufd", K.Vx, K.Wx, K.Ib);
        Row(Map0F, PpF3, 0x70, "pshufhw", K.Vx, K.Wx, K.Ib);
        Row(Map0F, PpF2, 0x70, "pshuflw", K.Vx, K.Wx, K.Ib);

        // Unpacks.
        (int op, string name)[] unpack =
        [
            (0x60, "punpcklbw"), (0x61, "punpcklwd"), (0x62, "punpckldq"), (0x63, "packsswb"),
            (0x64, "pcmpgtb"), (0x65, "pcmpgtw"), (0x66, "pcmpgtd"), (0x67, "packuswb"),
            (0x68, "punpckhbw"), (0x69, "punpckhwd"), (0x6A, "punpckhdq"), (0x6B, "packssdw"),
            (0x6C, "punpcklqdq"), (0x6D, "punpckhqdq"),
        ];
        foreach (var (op, name) in unpack) Row(Map0F, Pp66, op, name, K.Vx, K.Wx);

        Row(Map0F, Pp66, 0x74, "pcmpeqb", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x75, "pcmpeqw", K.Vx, K.Wx);
        Row(Map0F, Pp66, 0x76, "pcmpeqd", K.Vx, K.Wx);

        // Arithmetic/logical packed integer (D0-FF).
        (int op, string name)[] packedInt =
        [
            (0xD1, "psrlw"), (0xD2, "psrld"), (0xD3, "psrlq"), (0xD4, "paddq"), (0xD5, "pmullw"),
            (0xD8, "psubusb"), (0xD9, "psubusw"), (0xDA, "pminub"), (0xDB, "pand"), (0xDC, "paddusb"),
            (0xDD, "paddusw"), (0xDE, "pmaxub"), (0xDF, "pandn"), (0xE0, "pavgb"), (0xE1, "psraw"),
            (0xE2, "psrad"), (0xE3, "pavgw"), (0xE4, "pmulhuw"), (0xE5, "pmulhw"), (0xE8, "psubsb"),
            (0xE9, "psubsw"), (0xEA, "pminsw"), (0xEB, "por"), (0xEC, "paddsb"), (0xED, "paddsw"),
            (0xEE, "pmaxsw"), (0xEF, "pxor"), (0xF1, "psllw"), (0xF2, "pslld"), (0xF3, "psllq"),
            (0xF4, "pmuludq"), (0xF5, "pmaddwd"), (0xF6, "psadbw"), (0xF8, "psubb"), (0xF9, "psubw"),
            (0xFA, "psubd"), (0xFB, "psubq"), (0xFC, "paddb"), (0xFD, "paddw"), (0xFE, "paddd"),
        ];
        foreach (var (op, name) in packedInt) Row(Map0F, Pp66, op, name, K.Vx, K.Wx);

        // Shift-by-immediate groups (71/72/73) — reg selects psrlw/psraw/psllw etc.
        Row(Map0F, Pp66, 0x71, null, K.Wx, K.Ib, flags: OpFlags.Group, groupOrTuple: GrpShiftW);
        Row(Map0F, Pp66, 0x72, null, K.Wx, K.Ib, flags: OpFlags.Group, groupOrTuple: GrpShiftD);
        Row(Map0F, Pp66, 0x73, null, K.Wx, K.Ib, flags: OpFlags.Group, groupOrTuple: GrpShiftQ);
        Group(GrpShiftW, 2, "psrlw"); Group(GrpShiftW, 4, "psraw"); Group(GrpShiftW, 6, "psllw");
        Group(GrpShiftD, 2, "psrld"); Group(GrpShiftD, 4, "psrad"); Group(GrpShiftD, 6, "pslld");
        Group(GrpShiftQ, 2, "psrlq"); Group(GrpShiftQ, 3, "psrldq");
        Group(GrpShiftQ, 6, "psllq"); Group(GrpShiftQ, 7, "pslldq");
    }

    private static void RegisterSse38()
    {
        // SSSE3 / SSE4.1 / SSE4.2 over 0F 38 (all 66-prefixed here).
        (int op, string name)[] ops =
        [
            (0x00, "pshufb"), (0x01, "phaddw"), (0x02, "phaddd"), (0x03, "phaddsw"), (0x04, "pmaddubsw"),
            (0x05, "phsubw"), (0x06, "phsubd"), (0x07, "phsubsw"), (0x08, "psignb"), (0x09, "psignw"),
            (0x0A, "psignd"), (0x0B, "pmulhrsw"), (0x1C, "pabsb"), (0x1D, "pabsw"), (0x1E, "pabsd"),
            (0x20, "pmovsxbw"), (0x21, "pmovsxbd"), (0x22, "pmovsxbq"), (0x23, "pmovsxwd"), (0x24, "pmovsxwq"),
            (0x25, "pmovsxdq"), (0x28, "pmuldq"), (0x29, "pcmpeqq"), (0x2B, "packusdw"),
            (0x30, "pmovzxbw"), (0x31, "pmovzxbd"), (0x32, "pmovzxbq"), (0x33, "pmovzxwd"), (0x34, "pmovzxwq"),
            (0x35, "pmovzxdq"), (0x37, "pcmpgtq"), (0x38, "pminsb"), (0x39, "pminsd"), (0x3A, "pminuw"),
            (0x3B, "pminud"), (0x3C, "pmaxsb"), (0x3D, "pmaxsd"), (0x3E, "pmaxuw"), (0x3F, "pmaxud"),
            (0x40, "pmulld"), (0x41, "phminposuw"),
        ];
        foreach (var (op, name) in ops) Row(Map0F38, Pp66, op, name, K.Vx, K.Wx);

        Row(Map0F38, Pp66, 0x17, "ptest", K.Vx, K.Wx);
        Row(Map0F38, Pp66, 0x2A, "movntdqa", K.Vx, K.Wx);
        // crc32 (SSE4.2, F2 prefix).
        Row(Map0F38, PpF2, 0xF0, "crc32", K.Gy, K.Eb);
        Row(Map0F38, PpF2, 0xF1, "crc32", K.Gy, K.Ev);
    }

    private static void RegisterSse3A()
    {
        (int op, string name)[] ops =
        [
            (0x08, "roundps"), (0x09, "roundpd"), (0x0A, "roundss"), (0x0B, "roundsd"),
            (0x0C, "blendps"), (0x0D, "blendpd"), (0x0E, "pblendw"), (0x0F, "palignr"),
            (0x40, "dpps"), (0x41, "dppd"), (0x42, "mpsadbw"),
            (0x60, "pcmpestrm"), (0x61, "pcmpestri"), (0x62, "pcmpistrm"), (0x63, "pcmpistri"),
        ];
        foreach (var (op, name) in ops) Row(Map0F3A, Pp66, op, name, K.Vx, K.Wx, K.Ib);

        // Insert/extract carry a GPR or memory operand.
        Row(Map0F3A, Pp66, 0x14, "pextrb", K.Ev, K.Vx, K.Ib);
        Row(Map0F3A, Pp66, 0x15, "pextrw", K.Ev, K.Vx, K.Ib);
        Row(Map0F3A, Pp66, 0x16, "pextrd", K.Ev, K.Vx, K.Ib);
        Row(Map0F3A, Pp66, 0x17, "extractps", K.Ev, K.Vx, K.Ib);
        Row(Map0F3A, Pp66, 0x20, "pinsrb", K.Vx, K.Ev, K.Ib);
        Row(Map0F3A, Pp66, 0x21, "insertps", K.Vx, K.Wss, K.Ib);
        Row(Map0F3A, Pp66, 0x22, "pinsrd", K.Vx, K.Ev, K.Ib);
    }
}
