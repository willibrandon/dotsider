namespace Dotsider.Core.Analysis.Disasm.x64;

/// <summary>
/// The x86-64 opcode tables and the builder that populates them. One unified table set —
/// <c>Tables[map][pp][opcode]</c> plus group tables indexed by ModRM.reg — serves the legacy, VEX,
/// and EVEX encodings, because VEX/EVEX.pp <em>is</em> the mandatory prefix and VEX/EVEX mmmmm
/// <em>is</em> the opcode map. Family files register their rows through <see cref="Row"/> and
/// <see cref="Group"/>; the static constructor invokes each family's registrar so the source reads
/// like the Intel opcode map rather than a switch.
/// </summary>
internal static partial class XarchTables
{
    /// <summary>Opcode map index: 0 = one-byte, 1 = 0F, 2 = 0F 38, 3 = 0F 3A.</summary>
    public const int MapOneByte = 0;
    /// <summary>The 0F opcode map.</summary>
    public const int Map0F = 1;
    /// <summary>The 0F 38 opcode map.</summary>
    public const int Map0F38 = 2;
    /// <summary>The 0F 3A opcode map.</summary>
    public const int Map0F3A = 3;
    /// <summary>The number of opcode maps.</summary>
    public const int MapCount = 4;

    /// <summary>Mandatory-prefix index: 0 = none, 1 = 66, 2 = F3, 3 = F2 (matches VEX/EVEX.pp).</summary>
    public const int PpNone = 0;
    /// <summary>The 66 mandatory prefix.</summary>
    public const int Pp66 = 1;
    /// <summary>The F3 mandatory prefix.</summary>
    public const int PpF3 = 2;
    /// <summary>The F2 mandatory prefix.</summary>
    public const int PpF2 = 3;

    // Group ids.
    internal const byte Grp1 = 1;    // 80/81 add/or/adc/sbb/and/sub/xor/cmp (Ev,Iz base)
    internal const byte Grp1b = 2;   // 80 (Eb,Ib base)
    internal const byte Grp1s = 3;   // 83 (Ev,Ib base)
    internal const byte Grp1A = 4;   // 8F pop
    internal const byte Grp2 = 5;    // shift/rotate
    internal const byte Grp3b = 6;   // F6 test/not/neg/mul/imul/div/idiv (Eb)
    internal const byte Grp3v = 7;   // F7 (Ev)
    internal const byte Grp4 = 8;    // FE inc/dec
    internal const byte Grp5 = 9;    // FF inc/dec/call/jmp/push
    internal const byte Grp11b = 10; // C6 mov Eb,Ib
    internal const byte Grp11v = 11; // C7 mov Ev,Iz
    internal const byte Grp7 = 12;   // 0F 01 (system; xgetbv etc. handled by mod==11 in the decoder)
    internal const byte Grp8 = 13;   // 0F BA bt/bts/btr/btc
    internal const byte Grp15 = 14;  // 0F AE fences/fxsave
    internal const byte GrpShiftW = 15; // 0F 71 psrlw/psraw/psllw
    internal const byte GrpShiftD = 16; // 0F 72 psrld/psrad/pslld
    internal const byte GrpShiftQ = 17; // 0F 73 psrlq/psrldq/psllq/pslldq
    internal const byte GroupCount = 32;

    private static readonly OpEntry[][][] Tables;
    private static readonly OpEntry[][] Groups;

    static XarchTables()
    {
        Tables = new OpEntry[MapCount][][];
        for (var map = 0; map < MapCount; map++)
        {
            Tables[map] = new OpEntry[4][];
            for (var pp = 0; pp < 4; pp++)
                Tables[map][pp] = new OpEntry[256];
        }

        Groups = new OpEntry[GroupCount][];
        for (var g = 0; g < GroupCount; g++)
            Groups[g] = new OpEntry[8];

        RegisterLegacy();
        RegisterSse();
        RegisterAvx();
    }

    /// <summary>
    /// Looks up an opcode. A missing prefixed entry falls back to the no-prefix entry, since many
    /// 0F opcodes ignore the mandatory prefix while SSE variants populate the prefixed slot.
    /// </summary>
    /// <param name="map">The opcode map.</param>
    /// <param name="pp">The mandatory-prefix index.</param>
    /// <param name="opcode">The final opcode byte.</param>
    public static OpEntry Lookup(int map, int pp, int opcode)
    {
        var entry = Tables[map][pp][opcode];
        if (entry.IsEmpty && pp != PpNone)
            entry = Tables[map][PpNone][opcode];
        return entry;
    }

    /// <summary>Returns a group's entry for a ModRM.reg value.</summary>
    /// <param name="groupId">The group id.</param>
    /// <param name="reg">The ModRM.reg field (0-7).</param>
    public static OpEntry GroupEntry(int groupId, int reg) => Groups[groupId][reg & 7];

    /// <summary>Whether a slot holds a defined (non-empty) row — used by the completeness test.</summary>
    /// <param name="map">The opcode map.</param>
    /// <param name="pp">The mandatory-prefix index.</param>
    /// <param name="opcode">The final opcode byte.</param>
    internal static bool HasEntry(int map, int pp, int opcode) => !Tables[map][pp][opcode].IsEmpty;

    /// <summary>Registers one opcode row.</summary>
    internal static void Row(
        int map, int pp, int opcode, string? mnemonic,
        OperandKind o1 = OperandKind.None, OperandKind o2 = OperandKind.None,
        OperandKind o3 = OperandKind.None, OperandKind o4 = OperandKind.None,
        OpFlags flags = OpFlags.None, byte groupOrTuple = 0)
    {
        Tables[map][pp][opcode] = new OpEntry(mnemonic, o1, o2, o3, o4, flags, groupOrTuple);
    }

    /// <summary>Registers one group entry (by ModRM.reg). None operands inherit the primary row's.</summary>
    internal static void Group(
        int groupId, int reg, string? mnemonic,
        OperandKind o1 = OperandKind.None, OperandKind o2 = OperandKind.None,
        OperandKind o3 = OperandKind.None, OperandKind o4 = OperandKind.None,
        OpFlags flags = OpFlags.None)
    {
        Groups[groupId][reg] = new OpEntry(mnemonic, o1, o2, o3, o4, flags, 0);
    }
}
