using Dotsider.Core.Analysis.Models;
using System.Text;

namespace Dotsider.Core.Analysis.Disasm.x64;

/// <summary>
/// Renders x86-64 memory references and immediates to Intel-syntax text and structured
/// <see cref="NativeOperand"/> fields, and resolves the x87 escape opcodes to mnemonics. Memory
/// references carry a size hint (<c>dword ptr [..]</c>) matching disassembler convention.
/// </summary>
internal static class XarchOperandFormatter
{
    /// <summary>Returns the Intel size-hint word for an operand size in bytes.</summary>
    /// <param name="sizeBytes">The operand size (1/2/4/8/16/32/64).</param>
    public static string SizeHint(int sizeBytes) => sizeBytes switch
    {
        1 => "byte ptr",
        2 => "word ptr",
        4 => "dword ptr",
        8 => "qword ptr",
        10 => "tbyte ptr",
        16 => "xmmword ptr",
        32 => "ymmword ptr",
        64 => "zmmword ptr",
        _ => "",
    };

    /// <summary>Formats a signed immediate as Intel hex (<c>0x10</c> / <c>-0x8</c>).</summary>
    /// <param name="value">The immediate value.</param>
    public static string Immediate(long value) =>
        value < 0 ? $"-0x{-value:x}" : $"0x{value:x}";

    /// <summary>
    /// Builds a memory-reference operand from its decoded parts, prefixing the size hint.
    /// </summary>
    /// <param name="sizeBytes">The referenced operand's size in bytes, or 0 for no hint.</param>
    /// <param name="baseReg">The base register name, or null.</param>
    /// <param name="indexReg">The index register name, or null.</param>
    /// <param name="scale">The index scale (1/2/4/8), or 0 when there is no index.</param>
    /// <param name="disp">The displacement.</param>
    /// <param name="ripRelative">Whether the reference is RIP-relative.</param>
    public static NativeOperand Memory(
        int sizeBytes, string? baseReg, string? indexReg, int scale, long disp, bool ripRelative)
    {
        var inner = new StringBuilder();
        if (ripRelative)
        {
            // The RIP-relative displacement is always an encoded disp32, so it is always shown.
            inner.Append("rip").Append(disp < 0 ? $"-0x{-disp:x}" : $"+0x{disp:x}");
        }
        else
        {
            var wrote = false;
            if (baseReg is not null) { inner.Append(baseReg); wrote = true; }
            if (indexReg is not null)
            {
                if (wrote) inner.Append('+');
                inner.Append(indexReg).Append('*').Append(scale);
                wrote = true;
            }

            if (disp != 0 || !wrote)
            {
                if (wrote) inner.Append(disp < 0 ? $"-0x{-disp:x}" : $"+0x{disp:x}");
                else inner.Append($"0x{disp:x}");
            }
        }

        var hint = SizeHint(sizeBytes);
        var text = hint.Length > 0 ? $"{hint} [{inner}]" : $"[{inner}]";
        return new NativeOperand(
            NativeOperandKind.Memory, text,
            MemoryBase: baseReg, MemoryIndex: indexReg, MemoryScale: scale,
            MemoryDisplacement: disp, IsRipRelative: ripRelative);
    }

    private static readonly string[][] X87 = BuildX87();

    /// <summary>
    /// Resolves an x87 escape (opcodes D8–DF) to its mnemonic from the opcode's low 3 bits, the
    /// ModRM reg field, and whether the ModRM is a register (mod==11) or memory form.
    /// </summary>
    /// <param name="opcode">The escape opcode byte (0xD8–0xDF).</param>
    /// <param name="modrm">The ModRM byte.</param>
    public static string X87Mnemonic(int opcode, byte modrm)
    {
        var esc = opcode - 0xD8;
        var isReg = (modrm >> 6) == 3;
        var reg = (modrm >> 3) & 7;
        var name = X87[esc * 2 + (isReg ? 1 : 0)][reg];
        return string.IsNullOrEmpty(name) ? "fpu" : name;
    }

    // Rows: [esc*2 + (isReg?1:0)][reg 0-7]. Memory then register form for each of D8..DF.
    private static string[][] BuildX87() =>
    [
        ["fadd", "fmul", "fcom", "fcomp", "fsub", "fsubr", "fdiv", "fdivr"],       // D8 mem
        ["fadd", "fmul", "fcom", "fcomp", "fsub", "fsubr", "fdiv", "fdivr"],       // D8 reg
        ["fld", "", "fst", "fstp", "fldenv", "fldcw", "fnstenv", "fnstcw"],        // D9 mem
        ["fld", "fxch", "fnop", "fstp", "", "", "", ""],                           // D9 reg
        ["fiadd", "fimul", "ficom", "ficomp", "fisub", "fisubr", "fidiv", "fidivr"], // DA mem
        ["fcmovb", "fcmove", "fcmovbe", "fcmovu", "", "fucompp", "", ""],          // DA reg
        ["fild", "fisttp", "fist", "fistp", "", "fld", "", "fstp"],                // DB mem
        ["fcmovnb", "fcmovne", "fcmovnbe", "fcmovnu", "", "fucomi", "fcomi", ""],  // DB reg
        ["fadd", "fmul", "fcom", "fcomp", "fsub", "fsubr", "fdiv", "fdivr"],       // DC mem
        ["fadd", "fmul", "", "", "fsub", "fsubr", "fdiv", "fdivr"],                // DC reg
        ["fld", "fisttp", "fst", "fstp", "frstor", "", "fnsave", "fnstsw"],        // DD mem
        ["ffree", "", "fst", "fstp", "fucom", "fucomp", "", ""],                   // DD reg
        ["fiadd", "fimul", "ficom", "ficomp", "fisub", "fisubr", "fidiv", "fidivr"], // DE mem
        ["faddp", "fmulp", "", "", "fsubrp", "fsubp", "fdivrp", "fdivp"],          // DE reg
        ["fild", "fisttp", "fist", "fistp", "fbld", "fild", "fbstp", "fistp"],     // DF mem
        ["", "", "", "", "fnstsw", "fucomip", "fcomip", ""],                       // DF reg
    ];
}
