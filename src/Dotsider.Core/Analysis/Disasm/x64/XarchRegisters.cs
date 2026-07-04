namespace Dotsider.Core.Analysis.Disasm.x64;

/// <summary>
/// x86-64 register name tables. General-purpose names depend on operand size and whether a REX
/// prefix is present (which turns <c>ah/ch/dh/bh</c> into <c>spl/bpl/sil/dil</c> and unlocks the
/// r8–r15 file); vector names depend on the effective vector length.
/// </summary>
internal static class XarchRegisters
{
    private static readonly string[] Gpr64 =
    [
        "rax", "rcx", "rdx", "rbx", "rsp", "rbp", "rsi", "rdi",
        "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15",
    ];

    private static readonly string[] Gpr32 =
    [
        "eax", "ecx", "edx", "ebx", "esp", "ebp", "esi", "edi",
        "r8d", "r9d", "r10d", "r11d", "r12d", "r13d", "r14d", "r15d",
    ];

    private static readonly string[] Gpr16 =
    [
        "ax", "cx", "dx", "bx", "sp", "bp", "si", "di",
        "r8w", "r9w", "r10w", "r11w", "r12w", "r13w", "r14w", "r15w",
    ];

    private static readonly string[] Gpr8Rex =
    [
        "al", "cl", "dl", "bl", "spl", "bpl", "sil", "dil",
        "r8b", "r9b", "r10b", "r11b", "r12b", "r13b", "r14b", "r15b",
    ];

    private static readonly string[] Gpr8NoRex =
        ["al", "cl", "dl", "bl", "ah", "ch", "dh", "bh"];

    private static readonly string[] Segments =
        ["es", "cs", "ss", "ds", "fs", "gs", "?", "?"];

    /// <summary>Returns a general-purpose register name.</summary>
    /// <param name="index">The register index (0-15).</param>
    /// <param name="sizeBytes">The operand size in bytes (1, 2, 4, or 8).</param>
    /// <param name="hasRex">Whether a REX prefix is present (affects the 8-bit high-byte names).</param>
    public static string Gpr(int index, int sizeBytes, bool hasRex) => sizeBytes switch
    {
        8 => Gpr64[index & 15],
        2 => Gpr16[index & 15],
        1 => hasRex ? Gpr8Rex[index & 15] : Gpr8NoRex[index & 7],
        _ => Gpr32[index & 15],
    };

    /// <summary>Returns a vector register name by effective length.</summary>
    /// <param name="index">The register index (0-31).</param>
    /// <param name="lengthBytes">The vector length in bytes (16 = xmm, 32 = ymm, 64 = zmm).</param>
    public static string Vector(int index, int lengthBytes)
    {
        var prefix = lengthBytes switch { 64 => "zmm", 32 => "ymm", _ => "xmm" };
        return $"{prefix}{index & 31}";
    }

    /// <summary>Returns a mask register name (<c>k0</c>–<c>k7</c>).</summary>
    /// <param name="index">The mask register index (0-7).</param>
    public static string Mask(int index) => $"k{index & 7}";

    /// <summary>Returns an MMX register name (<c>mm0</c>–<c>mm7</c>).</summary>
    /// <param name="index">The MMX register index (0-7).</param>
    public static string Mmx(int index) => $"mm{index & 7}";

    /// <summary>Returns an x87 register name (<c>st(0)</c>–<c>st(7)</c>).</summary>
    /// <param name="index">The x87 register index (0-7).</param>
    public static string St(int index) => $"st({index & 7})";

    /// <summary>Returns a segment register name.</summary>
    /// <param name="index">The segment index (0-5).</param>
    public static string Segment(int index) => Segments[index & 7];
}
