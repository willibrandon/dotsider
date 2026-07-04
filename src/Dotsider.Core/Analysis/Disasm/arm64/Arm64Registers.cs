namespace Dotsider.Core.Analysis.Disasm.arm64;

/// <summary>
/// Renders A64 register names from their 5-bit encodings across the register files: the general
/// registers (<c>w0-w30</c>/<c>x0-x30</c> with <c>wzr</c>/<c>xzr</c> or <c>wsp</c>/<c>sp</c> for
/// encoding 31), the SIMD/FP files (<c>b/h/s/d/q</c> and vector <c>v</c>), the SVE Z and predicate
/// files, and the condition names.
/// </summary>
internal static class Arm64Registers
{
    /// <summary>A general-purpose register; encoding 31 is the zero register.</summary>
    /// <param name="index">The 5-bit register number.</param>
    /// <param name="is64">Whether the 64-bit (x) view is selected.</param>
    public static string Gpr(int index, bool is64) => index == 31
        ? is64 ? "xzr" : "wzr"
        : (is64 ? "x" : "w") + index;

    /// <summary>A general-purpose register where encoding 31 is the stack pointer (add/sub, addresses).</summary>
    /// <param name="index">The 5-bit register number.</param>
    /// <param name="is64">Whether the 64-bit (x) view is selected.</param>
    public static string GprSp(int index, bool is64) => index == 31
        ? is64 ? "sp" : "wsp"
        : (is64 ? "x" : "w") + index;

    /// <summary>A scalar SIMD/FP register of the given size code.</summary>
    /// <param name="index">The 5-bit register number.</param>
    /// <param name="sizeCode">0=b, 1=h, 2=s, 3=d, 4=q.</param>
    public static string Fp(int index, int sizeCode) =>
        (sizeCode switch { 0 => "b", 1 => "h", 2 => "s", 3 => "d", _ => "q" }) + index;

    /// <summary>An SVE Z (scalable vector) register.</summary>
    /// <param name="index">The 5-bit register number.</param>
    public static string Z(int index) => "z" + index;

    /// <summary>An SVE predicate register.</summary>
    /// <param name="index">The 4-bit predicate number.</param>
    public static string P(int index) => "p" + index;

    private static readonly string[] Conditions =
        ["eq", "ne", "cs", "cc", "mi", "pl", "vs", "vc", "hi", "ls", "ge", "lt", "gt", "le", "al", "nv"];

    /// <summary>The condition name for a 4-bit condition code.</summary>
    /// <param name="cond">The 4-bit condition field.</param>
    public static string Condition(int cond) => Conditions[cond & 0xF];

    private static readonly string[] Shifts = ["lsl", "lsr", "asr", "ror"];

    /// <summary>The shift-type name for a 2-bit shift field.</summary>
    /// <param name="shift">The 2-bit shift type.</param>
    public static string Shift(int shift) => Shifts[shift & 3];

    private static readonly string[] Extends =
        ["uxtb", "uxth", "uxtw", "uxtx", "sxtb", "sxth", "sxtw", "sxtx"];

    /// <summary>The extend-type name for a 3-bit option field.</summary>
    /// <param name="option">The 3-bit extend option.</param>
    public static string Extend(int option) => Extends[option & 7];
}
