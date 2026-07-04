namespace Dotsider.Core.Analysis.Disasm.x64;

using K = OperandKind;
using F = OpFlags;

/// <summary>
/// Registers BMI1, BMI2, and the ADX opcodes. The BMI ops are VEX-encoded but keep their plain
/// mnemonics (no automatic <c>v</c> prefix), and take a general-purpose vvvv source (<c>By</c>);
/// ADX (<c>adcx</c>/<c>adox</c>) is a legacy 66/F3 0F 38 encoding. <c>popcnt</c>/<c>lzcnt</c>/
/// <c>tzcnt</c> are registered with the legacy 0F map.
/// </summary>
internal static partial class XarchTables
{
    internal const byte GrpBls = 18; // VEX 0F38 F3: blsr/blsmsk/blsi by reg

    private static void RegisterBmi()
    {
        Row(Map0F, PpF3, 0xB8, "popcnt", K.Gv, K.Ev, flags: F.NoVexPrefix);

        // BMI1.
        Row(Map0F38, PpNone, 0xF2, "andn", K.Gy, K.By, K.Ey, flags: F.NoVexPrefix);
        Row(Map0F38, PpNone, 0xF3, null, K.By, K.Ey, flags: F.Group | F.NoVexPrefix, groupOrTuple: GrpBls);
        Group(GrpBls, 1, "blsr"); Group(GrpBls, 2, "blsmsk"); Group(GrpBls, 3, "blsi");
        Row(Map0F38, PpNone, 0xF7, "bextr", K.Gy, K.Ey, K.By, flags: F.NoVexPrefix);

        // BMI2.
        Row(Map0F38, PpNone, 0xF5, "bzhi", K.Gy, K.Ey, K.By, flags: F.NoVexPrefix);
        Row(Map0F38, PpF2, 0xF5, "pdep", K.Gy, K.By, K.Ey, flags: F.NoVexPrefix);
        Row(Map0F38, PpF3, 0xF5, "pext", K.Gy, K.By, K.Ey, flags: F.NoVexPrefix);
        Row(Map0F38, PpF2, 0xF6, "mulx", K.Gy, K.By, K.Ey, flags: F.NoVexPrefix);
        Row(Map0F38, Pp66, 0xF7, "shlx", K.Gy, K.Ey, K.By, flags: F.NoVexPrefix);
        Row(Map0F38, PpF3, 0xF7, "sarx", K.Gy, K.Ey, K.By, flags: F.NoVexPrefix);
        Row(Map0F38, PpF2, 0xF7, "shrx", K.Gy, K.Ey, K.By, flags: F.NoVexPrefix);
        Row(Map0F3A, PpF2, 0xF0, "rorx", K.Gy, K.Ey, K.Ib, flags: F.NoVexPrefix);

        // ADX (legacy 0F 38).
        Row(Map0F38, Pp66, 0xF6, "adcx", K.Gy, K.Ey);
        Row(Map0F38, PpF3, 0xF6, "adox", K.Gy, K.Ey);
    }
}
