namespace Dotsider.Core.Analysis.Disasm.x64;

using K = OperandKind;
using F = OpFlags;

/// <summary>
/// Registers the legacy x86-64 surface: the full one-byte opcode map, the 0F integer / control /
/// bit / system opcodes, the enumerated runtime/system instructions (NOP forms, INT3, UD2, PAUSE,
/// fences, CPUID, XGETBV, CET ENDBR, RDTSC), and the opcode groups. The SSE and vector maps are
/// registered by their own family files. Each row reads like a line of the Intel opcode map.
/// </summary>
internal static partial class XarchTables
{
    private static void RegisterLegacy()
    {
        RegisterArithmetic();
        RegisterOneByteMisc();
        RegisterGroups();
        Register0FLegacy();
    }

    // 00-3D: the eight ALU ops share the six-form pattern (Eb,Gb / Ev,Gv / Gb,Eb / Gv,Ev / AL,Ib / rAX,Iz).
    private static void RegisterArithmetic()
    {
        AluBlock(0x00, "add");
        AluBlock(0x08, "or");
        AluBlock(0x10, "adc");
        AluBlock(0x18, "sbb");
        AluBlock(0x20, "and");
        AluBlock(0x28, "sub");
        AluBlock(0x30, "xor");
        AluBlock(0x38, "cmp");
    }

    private static void AluBlock(int b, string m)
    {
        Row(MapOneByte, PpNone, b + 0, m, K.Eb, K.Gb);
        Row(MapOneByte, PpNone, b + 1, m, K.Ev, K.Gv);
        Row(MapOneByte, PpNone, b + 2, m, K.Gb, K.Eb);
        Row(MapOneByte, PpNone, b + 3, m, K.Gv, K.Ev);
        Row(MapOneByte, PpNone, b + 4, m, K.AL, K.Ib);
        Row(MapOneByte, PpNone, b + 5, m, K.RAX, K.Iz);
    }

    private static void RegisterOneByteMisc()
    {
        // 50-5F push/pop, default 64-bit operand size.
        for (var i = 0; i < 8; i++)
        {
            Row(MapOneByte, PpNone, 0x50 + i, "push", K.Zv, flags: F.Default64);
            Row(MapOneByte, PpNone, 0x58 + i, "pop", K.Zv, flags: F.Default64);
        }

        Row(MapOneByte, PpNone, 0x63, "movsxd", K.Gv, K.Ev);
        Row(MapOneByte, PpNone, 0x68, "push", K.Iz, flags: F.Default64);
        Row(MapOneByte, PpNone, 0x69, "imul", K.Gv, K.Ev, K.Iz);
        Row(MapOneByte, PpNone, 0x6A, "push", K.Ib, flags: F.Default64);
        Row(MapOneByte, PpNone, 0x6B, "imul", K.Gv, K.Ev, K.Ib);
        Row(MapOneByte, PpNone, 0x6C, "insb", K.None);
        Row(MapOneByte, PpNone, 0x6D, "insd", K.None);
        Row(MapOneByte, PpNone, 0x6E, "outsb", K.None);
        Row(MapOneByte, PpNone, 0x6F, "outsd", K.None);

        // 70-7F jcc rel8.
        string[] cc =
        [
            "jo", "jno", "jb", "jae", "je", "jne", "jbe", "ja",
            "js", "jns", "jp", "jnp", "jl", "jge", "jle", "jg",
        ];
        for (var i = 0; i < 16; i++)
            Row(MapOneByte, PpNone, 0x70 + i, cc[i], K.Jb);

        // 80-83 Grp1, 84-8F.
        Row(MapOneByte, PpNone, 0x80, null, K.Eb, K.Ib, flags: F.Group, groupOrTuple: Grp1b);
        Row(MapOneByte, PpNone, 0x81, null, K.Ev, K.Iz, flags: F.Group, groupOrTuple: Grp1);
        Row(MapOneByte, PpNone, 0x83, null, K.Ev, K.Ib, flags: F.Group, groupOrTuple: Grp1s);
        Row(MapOneByte, PpNone, 0x84, "test", K.Eb, K.Gb);
        Row(MapOneByte, PpNone, 0x85, "test", K.Ev, K.Gv);
        Row(MapOneByte, PpNone, 0x86, "xchg", K.Eb, K.Gb);
        Row(MapOneByte, PpNone, 0x87, "xchg", K.Ev, K.Gv);
        Row(MapOneByte, PpNone, 0x88, "mov", K.Eb, K.Gb);
        Row(MapOneByte, PpNone, 0x89, "mov", K.Ev, K.Gv);
        Row(MapOneByte, PpNone, 0x8A, "mov", K.Gb, K.Eb);
        Row(MapOneByte, PpNone, 0x8B, "mov", K.Gv, K.Ev);
        Row(MapOneByte, PpNone, 0x8C, "mov", K.Ev, K.Sw);
        Row(MapOneByte, PpNone, 0x8D, "lea", K.Gv, K.M);
        Row(MapOneByte, PpNone, 0x8E, "mov", K.Sw, K.Ew);
        Row(MapOneByte, PpNone, 0x8F, null, K.Ev, flags: F.Group | F.Default64, groupOrTuple: Grp1A);

        // 90-97 nop/xchg.
        Row(MapOneByte, PpNone, 0x90, "nop");
        Row(MapOneByte, PpF3, 0x90, "pause");
        for (var i = 1; i < 8; i++)
            Row(MapOneByte, PpNone, 0x90 + i, "xchg", K.RAX, K.Zv);

        Row(MapOneByte, PpNone, 0x98, "cwde");
        Row(MapOneByte, PpNone, 0x99, "cdq");
        Row(MapOneByte, PpNone, 0x9B, "fwait");
        Row(MapOneByte, PpNone, 0x9C, "pushf", flags: F.Default64);
        Row(MapOneByte, PpNone, 0x9D, "popf", flags: F.Default64);
        Row(MapOneByte, PpNone, 0x9E, "sahf");
        Row(MapOneByte, PpNone, 0x9F, "lahf");

        Row(MapOneByte, PpNone, 0xA0, "mov", K.AL, K.Ob);
        Row(MapOneByte, PpNone, 0xA1, "mov", K.RAX, K.Ov);
        Row(MapOneByte, PpNone, 0xA2, "mov", K.Ob, K.AL);
        Row(MapOneByte, PpNone, 0xA3, "mov", K.Ov, K.RAX);
        Row(MapOneByte, PpNone, 0xA4, "movsb");
        Row(MapOneByte, PpNone, 0xA5, "movsd");
        Row(MapOneByte, PpNone, 0xA6, "cmpsb");
        Row(MapOneByte, PpNone, 0xA7, "cmpsd");
        Row(MapOneByte, PpNone, 0xA8, "test", K.AL, K.Ib);
        Row(MapOneByte, PpNone, 0xA9, "test", K.RAX, K.Iz);
        Row(MapOneByte, PpNone, 0xAA, "stosb");
        Row(MapOneByte, PpNone, 0xAB, "stosd");
        Row(MapOneByte, PpNone, 0xAC, "lodsb");
        Row(MapOneByte, PpNone, 0xAD, "lodsd");
        Row(MapOneByte, PpNone, 0xAE, "scasb");
        Row(MapOneByte, PpNone, 0xAF, "scasd");

        // B0-BF mov reg,imm.
        for (var i = 0; i < 8; i++)
        {
            Row(MapOneByte, PpNone, 0xB0 + i, "mov", K.Zb, K.Ib);
            Row(MapOneByte, PpNone, 0xB8 + i, "mov", K.Zv, K.Iv);
        }

        Row(MapOneByte, PpNone, 0xC0, null, K.Eb, K.Ib, flags: F.Group, groupOrTuple: Grp2);
        Row(MapOneByte, PpNone, 0xC1, null, K.Ev, K.Ib, flags: F.Group, groupOrTuple: Grp2);
        Row(MapOneByte, PpNone, 0xC2, "ret", K.Iw);
        Row(MapOneByte, PpNone, 0xC3, "ret");
        Row(MapOneByte, PpNone, 0xC6, null, K.Eb, K.Ib, flags: F.Group, groupOrTuple: Grp11b);
        Row(MapOneByte, PpNone, 0xC7, null, K.Ev, K.Iz, flags: F.Group, groupOrTuple: Grp11v);
        Row(MapOneByte, PpNone, 0xC8, "enter", K.Iw, K.Ib);
        Row(MapOneByte, PpNone, 0xC9, "leave", flags: F.Default64);
        Row(MapOneByte, PpNone, 0xCA, "retf", K.Iw);
        Row(MapOneByte, PpNone, 0xCB, "retf");
        Row(MapOneByte, PpNone, 0xCC, "int3");
        Row(MapOneByte, PpNone, 0xCD, "int", K.Ib);
        Row(MapOneByte, PpNone, 0xCF, "iret");

        Row(MapOneByte, PpNone, 0xD0, null, K.Eb, K.One, flags: F.Group, groupOrTuple: Grp2);
        Row(MapOneByte, PpNone, 0xD1, null, K.Ev, K.One, flags: F.Group, groupOrTuple: Grp2);
        Row(MapOneByte, PpNone, 0xD2, null, K.Eb, K.CL, flags: F.Group, groupOrTuple: Grp2);
        Row(MapOneByte, PpNone, 0xD3, null, K.Ev, K.CL, flags: F.Group, groupOrTuple: Grp2);
        Row(MapOneByte, PpNone, 0xD7, "xlat");

        // D8-DF x87 escapes — all carry a ModRM; the mnemonic is resolved from the FPU sub-tables.
        for (var i = 0; i < 8; i++)
            Row(MapOneByte, PpNone, 0xD8 + i, "fpu", flags: F.HasModRm);

        Row(MapOneByte, PpNone, 0xE0, "loopne", K.Jb);
        Row(MapOneByte, PpNone, 0xE1, "loope", K.Jb);
        Row(MapOneByte, PpNone, 0xE2, "loop", K.Jb);
        Row(MapOneByte, PpNone, 0xE3, "jrcxz", K.Jb);
        Row(MapOneByte, PpNone, 0xE4, "in", K.AL, K.Ib);
        Row(MapOneByte, PpNone, 0xE5, "in", K.RAX, K.Ib);
        Row(MapOneByte, PpNone, 0xE6, "out", K.Ib, K.AL);
        Row(MapOneByte, PpNone, 0xE7, "out", K.Ib, K.RAX);
        Row(MapOneByte, PpNone, 0xE8, "call", K.Jz, flags: F.Default64);
        Row(MapOneByte, PpNone, 0xE9, "jmp", K.Jz, flags: F.Default64);
        Row(MapOneByte, PpNone, 0xEB, "jmp", K.Jb);
        Row(MapOneByte, PpNone, 0xEC, "in", K.AL, K.DX);
        Row(MapOneByte, PpNone, 0xED, "in", K.RAX, K.DX);
        Row(MapOneByte, PpNone, 0xEE, "out", K.DX, K.AL);
        Row(MapOneByte, PpNone, 0xEF, "out", K.DX, K.RAX);

        Row(MapOneByte, PpNone, 0xF1, "int1");
        Row(MapOneByte, PpNone, 0xF4, "hlt");
        Row(MapOneByte, PpNone, 0xF5, "cmc");
        Row(MapOneByte, PpNone, 0xF6, null, K.Eb, flags: F.Group, groupOrTuple: Grp3b);
        Row(MapOneByte, PpNone, 0xF7, null, K.Ev, flags: F.Group, groupOrTuple: Grp3v);
        Row(MapOneByte, PpNone, 0xF8, "clc");
        Row(MapOneByte, PpNone, 0xF9, "stc");
        Row(MapOneByte, PpNone, 0xFA, "cli");
        Row(MapOneByte, PpNone, 0xFB, "sti");
        Row(MapOneByte, PpNone, 0xFC, "cld");
        Row(MapOneByte, PpNone, 0xFD, "std");
        Row(MapOneByte, PpNone, 0xFE, null, K.Eb, flags: F.Group, groupOrTuple: Grp4);
        Row(MapOneByte, PpNone, 0xFF, null, flags: F.Group, groupOrTuple: Grp5);
    }

    private static void RegisterGroups()
    {
        string[] alu = ["add", "or", "adc", "sbb", "and", "sub", "xor", "cmp"];
        for (var r = 0; r < 8; r++)
        {
            Group(Grp1, r, alu[r]);
            Group(Grp1b, r, alu[r]);
            Group(Grp1s, r, alu[r]);
        }

        Group(Grp1A, 0, "pop");

        string[] shift = ["rol", "ror", "rcl", "rcr", "shl", "shr", "sal", "sar"];
        for (var r = 0; r < 8; r++)
            Group(Grp2, r, shift[r]);

        // Grp3: test (0/1 carry an immediate), not, neg, mul, imul, div, idiv.
        Group(Grp3b, 0, "test", K.Eb, K.Ib);
        Group(Grp3b, 1, "test", K.Eb, K.Ib);
        Group(Grp3b, 2, "not", K.Eb);
        Group(Grp3b, 3, "neg", K.Eb);
        Group(Grp3b, 4, "mul", K.Eb);
        Group(Grp3b, 5, "imul", K.Eb);
        Group(Grp3b, 6, "div", K.Eb);
        Group(Grp3b, 7, "idiv", K.Eb);
        Group(Grp3v, 0, "test", K.Ev, K.Iz);
        Group(Grp3v, 1, "test", K.Ev, K.Iz);
        Group(Grp3v, 2, "not", K.Ev);
        Group(Grp3v, 3, "neg", K.Ev);
        Group(Grp3v, 4, "mul", K.Ev);
        Group(Grp3v, 5, "imul", K.Ev);
        Group(Grp3v, 6, "div", K.Ev);
        Group(Grp3v, 7, "idiv", K.Ev);

        Group(Grp4, 0, "inc");
        Group(Grp4, 1, "dec");

        Group(Grp5, 0, "inc", K.Ev);
        Group(Grp5, 1, "dec", K.Ev);
        Group(Grp5, 2, "call", K.Ev, flags: F.Force64);
        Group(Grp5, 3, "callf", K.M);
        Group(Grp5, 4, "jmp", K.Ev, flags: F.Force64);
        Group(Grp5, 5, "jmpf", K.M);
        Group(Grp5, 6, "push", K.Ev, flags: F.Default64);

        Group(Grp11b, 0, "mov");
        Group(Grp11v, 0, "mov");

        string[] bt = ["", "", "", "", "bt", "bts", "btr", "btc"];
        for (var r = 4; r < 8; r++)
            Group(Grp8, r, bt[r], K.Ev, K.Ib);
    }

    private static void Register0FLegacy()
    {
        Row(Map0F, PpNone, 0x05, "syscall");
        Row(Map0F, PpNone, 0x0B, "ud2");
        Row(Map0F, PpNone, 0x08, "invd");
        Row(Map0F, PpNone, 0x09, "wbinvd");
        Row(Map0F, PpNone, 0x1F, "nop", K.Ev, flags: F.HasModRm);
        Row(Map0F, PpNone, 0x18, "prefetch", K.M, flags: F.HasModRm);
        Row(Map0F, PpNone, 0x0D, "prefetchw", K.M, flags: F.HasModRm);
        for (var op = 0x19; op <= 0x1E; op++)
            Row(Map0F, PpNone, op, "nop", K.Ev, flags: F.HasModRm);
        Row(Map0F, PpF3, 0x1E, "endbr", flags: F.HasModRm); // FA=endbr64, FB=endbr32 (decoder)

        Row(Map0F, PpNone, 0x01, null, flags: F.Group | F.HasModRm, groupOrTuple: Grp7);
        Row(Map0F, PpNone, 0x31, "rdtsc");
        Row(Map0F, PpNone, 0xA2, "cpuid");
        Row(Map0F, PpNone, 0x77, "emms");
        Row(Map0F, PpNone, 0x34, "sysenter");
        Row(Map0F, PpNone, 0x35, "sysexit");
        Row(Map0F, PpNone, 0xAE, null, flags: F.Group | F.HasModRm, groupOrTuple: Grp15);

        // 40-4F cmovcc.
        string[] cc =
        [
            "cmovo", "cmovno", "cmovb", "cmovae", "cmove", "cmovne", "cmovbe", "cmova",
            "cmovs", "cmovns", "cmovp", "cmovnp", "cmovl", "cmovge", "cmovle", "cmovg",
        ];
        for (var i = 0; i < 16; i++)
            Row(Map0F, PpNone, 0x40 + i, cc[i], K.Gv, K.Ev);

        // 80-8F jcc near, default 64-bit.
        string[] jcc =
        [
            "jo", "jno", "jb", "jae", "je", "jne", "jbe", "ja",
            "js", "jns", "jp", "jnp", "jl", "jge", "jle", "jg",
        ];
        for (var i = 0; i < 16; i++)
            Row(Map0F, PpNone, 0x80 + i, jcc[i], K.Jz, flags: F.Default64);

        // 90-9F setcc.
        string[] setcc =
        [
            "seto", "setno", "setb", "setae", "sete", "setne", "setbe", "seta",
            "sets", "setns", "setp", "setnp", "setl", "setge", "setle", "setg",
        ];
        for (var i = 0; i < 16; i++)
            Row(Map0F, PpNone, 0x90 + i, setcc[i], K.Eb);

        Row(Map0F, PpNone, 0xA0, "push", K.None, flags: F.Default64); // push fs
        Row(Map0F, PpNone, 0xA1, "pop", K.None, flags: F.Default64);  // pop fs
        Row(Map0F, PpNone, 0xA3, "bt", K.Ev, K.Gv);
        Row(Map0F, PpNone, 0xA8, "push", K.None, flags: F.Default64); // push gs
        Row(Map0F, PpNone, 0xA9, "pop", K.None, flags: F.Default64);  // pop gs
        Row(Map0F, PpNone, 0xAB, "bts", K.Ev, K.Gv);
        Row(Map0F, PpNone, 0xAF, "imul", K.Gv, K.Ev);
        Row(Map0F, PpNone, 0xB0, "cmpxchg", K.Eb, K.Gb);
        Row(Map0F, PpNone, 0xB1, "cmpxchg", K.Ev, K.Gv);
        Row(Map0F, PpNone, 0xB3, "btr", K.Ev, K.Gv);
        Row(Map0F, PpNone, 0xB6, "movzx", K.Gv, K.Eb);
        Row(Map0F, PpNone, 0xB7, "movzx", K.Gv, K.Ew);
        Row(Map0F, PpNone, 0xBA, null, K.Ev, K.Ib, flags: F.Group, groupOrTuple: Grp8);
        Row(Map0F, PpNone, 0xBB, "btc", K.Ev, K.Gv);
        Row(Map0F, PpNone, 0xBC, "bsf", K.Gv, K.Ev);
        Row(Map0F, PpF3, 0xBC, "tzcnt", K.Gv, K.Ev);
        Row(Map0F, PpNone, 0xBD, "bsr", K.Gv, K.Ev);
        Row(Map0F, PpF3, 0xBD, "lzcnt", K.Gv, K.Ev);
        Row(Map0F, PpNone, 0xBE, "movsx", K.Gv, K.Eb);
        Row(Map0F, PpNone, 0xBF, "movsx", K.Gv, K.Ew);
        Row(Map0F, PpNone, 0xC0, "xadd", K.Eb, K.Gb);
        Row(Map0F, PpNone, 0xC1, "xadd", K.Ev, K.Gv);

        // C8-CF bswap.
        for (var i = 0; i < 8; i++)
            Row(Map0F, PpNone, 0xC8 + i, "bswap", K.Zv);
    }
}
