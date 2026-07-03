namespace Dotsider.Core.Analysis.Disasm.x64;

using K = OperandKind;

/// <summary>
/// Registers the VEX-only AVX / AVX2 opcodes — the ones without a legacy SSE form: vector
/// broadcasts, cross-lane permutes, 128-bit insert/extract, variable shifts, masked moves, and the
/// F16C half-precision conversions. The AVX encodings of the SSE opcodes themselves are already
/// served by the shared SSE rows (which carry the <c>Hx</c> vvvv source that VEX decode renders).
/// These rows are reached only through a VEX prefix, so the decoder applies the <c>v</c> mnemonic
/// prefix automatically.
/// </summary>
internal static partial class XarchTables
{
    private static void RegisterAvx()
    {
        // zeroupper/zeroall (0F 77 under VEX) are named by the decoder (the legacy 0F 77 is emms).

        // Broadcasts (0F 38, 66).
        Row(Map0F38, Pp66, 0x18, "broadcastss", K.Vx, K.Wss);
        Row(Map0F38, Pp66, 0x19, "broadcastsd", K.Vx, K.Wsd);
        Row(Map0F38, Pp66, 0x1A, "broadcastf128", K.Vx, K.M);
        Row(Map0F38, Pp66, 0x58, "pbroadcastd", K.Vx, K.Wx);
        Row(Map0F38, Pp66, 0x59, "pbroadcastq", K.Vx, K.Wx);
        Row(Map0F38, Pp66, 0x78, "pbroadcastb", K.Vx, K.Wx);
        Row(Map0F38, Pp66, 0x79, "pbroadcastw", K.Vx, K.Wx);
        Row(Map0F38, Pp66, 0x5A, "broadcasti128", K.Vx, K.M);

        // Permutes.
        Row(Map0F38, Pp66, 0x0C, "permilps", K.Vx, K.Hx, K.Wx);
        Row(Map0F38, Pp66, 0x0D, "permilpd", K.Vx, K.Hx, K.Wx);
        Row(Map0F3A, Pp66, 0x04, "permilps", K.Vx, K.Wx, K.Ib);
        Row(Map0F3A, Pp66, 0x05, "permilpd", K.Vx, K.Wx, K.Ib);
        Row(Map0F38, Pp66, 0x16, "permps", K.Vx, K.Hx, K.Wx);
        Row(Map0F38, Pp66, 0x36, "permd", K.Vx, K.Hx, K.Wx);
        Row(Map0F3A, Pp66, 0x00, "permq", K.Vx, K.Wx, K.Ib);
        Row(Map0F3A, Pp66, 0x01, "permpd", K.Vx, K.Wx, K.Ib);
        Row(Map0F3A, Pp66, 0x06, "perm2f128", K.Vx, K.Hx, K.Wx, K.Ib);
        Row(Map0F3A, Pp66, 0x46, "perm2i128", K.Vx, K.Hx, K.Wx, K.Ib);

        // 128-bit insert/extract.
        Row(Map0F3A, Pp66, 0x18, "insertf128", K.Vx, K.Hx, K.Wxmm, K.Ib);
        Row(Map0F3A, Pp66, 0x19, "extractf128", K.Wxmm, K.Vx, K.Ib);
        Row(Map0F3A, Pp66, 0x38, "inserti128", K.Vx, K.Hx, K.Wxmm, K.Ib);
        Row(Map0F3A, Pp66, 0x39, "extracti128", K.Wxmm, K.Vx, K.Ib);

        // Blends.
        Row(Map0F3A, Pp66, 0x02, "pblendd", K.Vx, K.Hx, K.Wx, K.Ib);
        Row(Map0F3A, Pp66, 0x4C, "pblendvb", K.Vx, K.Hx, K.Wx, K.Lx);
        Row(Map0F3A, Pp66, 0x4A, "blendvps", K.Vx, K.Hx, K.Wx, K.Lx);
        Row(Map0F3A, Pp66, 0x4B, "blendvpd", K.Vx, K.Hx, K.Wx, K.Lx);

        // Variable shifts (AVX2).
        Row(Map0F38, Pp66, 0x45, "psrlvd", K.Vx, K.Hx, K.Wx);
        Row(Map0F38, Pp66, 0x46, "psravd", K.Vx, K.Hx, K.Wx);
        Row(Map0F38, Pp66, 0x47, "psllvd", K.Vx, K.Hx, K.Wx);

        // Masked moves.
        Row(Map0F38, Pp66, 0x2C, "maskmovps", K.Vx, K.Hx, K.M);
        Row(Map0F38, Pp66, 0x2D, "maskmovpd", K.Vx, K.Hx, K.M);
        Row(Map0F38, Pp66, 0x2E, "maskmovps", K.M, K.Hx, K.Vx);
        Row(Map0F38, Pp66, 0x2F, "maskmovpd", K.M, K.Hx, K.Vx);
        Row(Map0F38, Pp66, 0x8C, "pmaskmovd", K.Vx, K.Hx, K.M);
        Row(Map0F38, Pp66, 0x8E, "pmaskmovd", K.M, K.Hx, K.Vx);

        // Tests.
        Row(Map0F38, Pp66, 0x0E, "testps", K.Vx, K.Wx);
        Row(Map0F38, Pp66, 0x0F, "testpd", K.Vx, K.Wx);

        // F16C half-precision conversions (the packed-half side is half the width).
        Row(Map0F38, Pp66, 0x13, "cvtph2ps", K.Vx, K.Wxmm);
        Row(Map0F3A, Pp66, 0x1D, "cvtps2ph", K.Wxmm, K.Vx, K.Ib);
    }
}
