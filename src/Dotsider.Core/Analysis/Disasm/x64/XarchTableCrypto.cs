namespace Dotsider.Core.Analysis.Disasm.x64;

using K = OperandKind;

/// <summary>
/// Registers the AES-NI, PCLMULQDQ, and GFNI opcodes over 66.0F38 / 66.0F3A. The rounds
/// (<c>aesenc</c>/<c>aesdec</c> and the GF ops) are three-operand under VEX/EVEX (VAES,
/// VPCLMULQDQ), so they carry the <c>Hx</c> vvvv source the legacy decode drops; <c>aesimc</c> and
/// <c>aeskeygenassist</c> stay two-operand.
/// </summary>
internal static partial class XarchTables
{
    private static void RegisterCrypto()
    {
        // AES-NI (0F 38).
        Row(Map0F38, Pp66, 0xDB, "aesimc", K.Vx, K.Wx);
        Row(Map0F38, Pp66, 0xDC, "aesenc", K.Vx, K.Hx, K.Wx);
        Row(Map0F38, Pp66, 0xDD, "aesenclast", K.Vx, K.Hx, K.Wx);
        Row(Map0F38, Pp66, 0xDE, "aesdec", K.Vx, K.Hx, K.Wx);
        Row(Map0F38, Pp66, 0xDF, "aesdeclast", K.Vx, K.Hx, K.Wx);
        Row(Map0F3A, Pp66, 0xDF, "aeskeygenassist", K.Vx, K.Wx, K.Ib);

        // PCLMULQDQ (+ VPCLMULQDQ under VEX/EVEX).
        Row(Map0F3A, Pp66, 0x44, "pclmulqdq", K.Vx, K.Hx, K.Wx, K.Ib);

        // GFNI.
        Row(Map0F38, Pp66, 0xCF, "gf2p8mulb", K.Vx, K.Hx, K.Wx);
        Row(Map0F3A, Pp66, 0xCE, "gf2p8affineqb", K.Vx, K.Hx, K.Wx, K.Ib);
        Row(Map0F3A, Pp66, 0xCF, "gf2p8affineinvqb", K.Vx, K.Hx, K.Wx, K.Ib);
    }
}
