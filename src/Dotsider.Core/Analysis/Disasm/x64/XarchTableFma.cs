namespace Dotsider.Core.Analysis.Disasm.x64;

using K = OperandKind;

/// <summary>
/// Registers the FMA (fused multiply-add) opcodes over VEX.66.0F38. Each opcode is three-operand
/// (<c>Vx, Hx, W</c>); the packed forms are registered with the <c>ps</c> suffix and the scalar
/// forms with <c>ss</c> for VEX.W = 0, and the decoder swaps to <c>pd</c>/<c>sd</c> when VEX.W = 1
/// (the one place a row's mnemonic depends on W).
/// </summary>
internal static partial class XarchTables
{
    /// <summary>Whether a 0F 38 opcode is in the FMA range whose <c>ps/ss</c> suffix flips on VEX.W.</summary>
    /// <param name="opcode">The final opcode byte.</param>
    internal static bool IsFmaOpcode(int opcode) => opcode is >= 0x96 and <= 0xBF;

    private static void RegisterFma()
    {
        // (low-nibble op, packed) — the odd opcodes are scalar (ss).
        (int lo, string name)[] ops =
        [
            (0x6, "fmaddsub"), (0x7, "fmsubadd"), (0x8, "fmadd"), (0x9, "fmadd"),
            (0xA, "fmsub"), (0xB, "fmsub"), (0xC, "fnmadd"), (0xD, "fnmadd"),
            (0xE, "fnmsub"), (0xF, "fnmsub"),
        ];
        (int hi, string order)[] orders = [(0x90, "132"), (0xA0, "213"), (0xB0, "231")];

        foreach (var (hi, order) in orders)
        {
            foreach (var (lo, name) in ops)
            {
                var opcode = hi | lo;
                var scalar = (lo & 1) != 0;
                Row(Map0F38, Pp66, opcode, $"{name}{order}{(scalar ? "ss" : "ps")}",
                    K.Vx, K.Hx, scalar ? K.Wss : K.Wx);
            }
        }
    }
}
