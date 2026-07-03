namespace Dotsider.Core.Analysis.Disasm.arm64;

using static Arm64Format;

/// <summary>
/// The A64 decode groups and the top-level dispatch. AArch64 is fixed 32-bit; bits[28:25] select a
/// major class, and within a class the rows are scanned most-specific-first (descending mask
/// population) so an alias or a narrower encoding matches before its generic parent. Group files
/// register their rows; new groups (loads/stores, SIMD/FP, SVE) plug into <see cref="Dispatch"/>.
/// </summary>
internal static partial class Arm64Tables
{
    private static readonly List<Arm64Entry> DpImm = [];
    private static readonly List<Arm64Entry> Branch = [];
    private static readonly List<Arm64Entry> DpReg = [];
    private static readonly List<Arm64Entry> LoadStore = [];
    private static readonly List<Arm64Entry> SimdFp = [];
    private static readonly List<Arm64Entry> Sve = [];
    private static readonly Arm64Entry[] Empty = [];

    static Arm64Tables()
    {
        RegisterDpImm();
        RegisterBranch();
        RegisterDpReg();
        RegisterLoadStore();
        RegisterSimdFp();
        RegisterSve();

        SortBySpecificity(DpImm);
        SortBySpecificity(Branch);
        SortBySpecificity(DpReg);
        SortBySpecificity(LoadStore);
        SortBySpecificity(SimdFp);
        SortBySpecificity(Sve);
    }

    /// <summary>Finds the most-specific row matching <paramref name="word"/>, or null.</summary>
    /// <param name="word">The 32-bit instruction word.</param>
    public static Arm64Entry? Decode(uint word)
    {
        foreach (var entry in Dispatch(word))
        {
            if (entry.Matches(word)) return entry;
        }

        return null;
    }

    /// <summary>Selects the decode group for a word by its major class bits[28:25].</summary>
    private static IReadOnlyList<Arm64Entry> Dispatch(uint word) => ((word >> 25) & 0xF) switch
    {
        0x8 or 0x9 => DpImm,
        0xA or 0xB => Branch,
        0x5 or 0xD => DpReg,
        0x4 or 0x6 or 0xC or 0xE => LoadStore,
        0x7 or 0xF => SimdFp,
        0x2 => Sve,
        _ => Empty,
    };

    private static void SortBySpecificity(List<Arm64Entry> rows) =>
        rows.Sort((a, b) => System.Numerics.BitOperations.PopCount(b.Mask)
            .CompareTo(System.Numerics.BitOperations.PopCount(a.Mask)));

    private static void Add(List<Arm64Entry> group, uint mask, uint match, string mnemonic, Arm64Format format) =>
        group.Add(new Arm64Entry(mask, match, mnemonic, format));

    // Implemented by the later group files (loads/stores, SIMD/FP, SVE); no-ops until then.
    static partial void RegisterLoadStore();
    static partial void RegisterSimdFp();
    static partial void RegisterSve();

    private static void RegisterDpImm()
    {
        // PC-relative addressing.
        Add(DpImm, 0x9F000000, 0x10000000, "adr", Adr);
        Add(DpImm, 0x9F000000, 0x90000000, "adrp", Adrp);

        // Add/subtract (immediate).
        Add(DpImm, 0x7F800000, 0x11000000, "add", AddSubImm);
        Add(DpImm, 0x7F800000, 0x31000000, "adds", AddSubImm);
        Add(DpImm, 0x7F800000, 0x51000000, "sub", AddSubImm);
        Add(DpImm, 0x7F800000, 0x71000000, "subs", AddSubImm);

        // Logical (immediate).
        Add(DpImm, 0x7F800000, 0x12000000, "and", LogicalImm);
        Add(DpImm, 0x7F800000, 0x32000000, "orr", LogicalImm);
        Add(DpImm, 0x7F800000, 0x52000000, "eor", LogicalImm);
        Add(DpImm, 0x7F800000, 0x72000000, "ands", LogicalImm);

        // Move wide (immediate).
        Add(DpImm, 0x7F800000, 0x12800000, "movn", MoveWide);
        Add(DpImm, 0x7F800000, 0x52800000, "movz", MoveWide);
        Add(DpImm, 0x7F800000, 0x72800000, "movk", MoveWide);

        // Bitfield and extract.
        Add(DpImm, 0x7F800000, 0x13000000, "sbfm", Bitfield);
        Add(DpImm, 0x7F800000, 0x33000000, "bfm", Bitfield);
        Add(DpImm, 0x7F800000, 0x53000000, "ubfm", Bitfield);
        Add(DpImm, 0x7FA00000, 0x13800000, "extr", Extract);
    }

    private static void RegisterBranch()
    {
        Add(Branch, 0xFC000000, 0x14000000, "b", BranchImm26);
        Add(Branch, 0xFC000000, 0x94000000, "bl", BranchImm26);
        Add(Branch, 0xFF000010, 0x54000000, "b", BranchCond);

        Add(Branch, 0x7F000000, 0x34000000, "cbz", CompareBranch);
        Add(Branch, 0x7F000000, 0x35000000, "cbnz", CompareBranch);
        Add(Branch, 0x7F000000, 0x36000000, "tbz", TestBranch);
        Add(Branch, 0x7F000000, 0x37000000, "tbnz", TestBranch);

        Add(Branch, 0xFFFFFC1F, 0xD61F0000, "br", BranchReg);
        Add(Branch, 0xFFFFFC1F, 0xD63F0000, "blr", BranchReg);
        Add(Branch, 0xFFFFFC1F, 0xD65F0000, "ret", BranchReg);

        Add(Branch, 0xFFE0001F, 0xD4000001, "svc", Exception);
        Add(Branch, 0xFFE0001F, 0xD4200000, "brk", Exception);

        Add(Branch, 0xFFFFFFFF, 0xD503201F, "nop", Hint);
        Add(Branch, 0xFFFFFFFF, 0xD503203F, "yield", Hint);
        Add(Branch, 0xFFFFF01F, 0xD503309F, "dsb", Barrier);
        Add(Branch, 0xFFFFF0FF, 0xD50330BF, "dmb", Barrier);
        Add(Branch, 0xFFFFF0FF, 0xD50330DF, "isb", Barrier);
        Add(Branch, 0xFFF00000, 0xD5300000, "mrs", SystemReg);
        Add(Branch, 0xFFF00000, 0xD5100000, "msr", SystemReg);
    }

    private static void RegisterDpReg()
    {
        // Add/subtract (shifted register).
        Add(DpReg, 0x7F200000, 0x0B000000, "add", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x2B000000, "adds", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x4B000000, "sub", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x6B000000, "subs", ShiftedReg);

        // Add/subtract (extended register).
        Add(DpReg, 0x7FE00000, 0x0B200000, "add", ExtendedReg);
        Add(DpReg, 0x7FE00000, 0x2B200000, "adds", ExtendedReg);
        Add(DpReg, 0x7FE00000, 0x4B200000, "sub", ExtendedReg);
        Add(DpReg, 0x7FE00000, 0x6B200000, "subs", ExtendedReg);

        // Logical (shifted register).
        Add(DpReg, 0x7F200000, 0x0A000000, "and", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x0A200000, "bic", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x2A000000, "orr", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x2A200000, "orn", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x4A000000, "eor", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x4A200000, "eon", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x6A000000, "ands", ShiftedReg);
        Add(DpReg, 0x7F200000, 0x6A200000, "bics", ShiftedReg);

        // Add/subtract with carry.
        Add(DpReg, 0x7FE0FC00, 0x1A000000, "adc", DataProc2);
        Add(DpReg, 0x7FE0FC00, 0x5A000000, "sbc", DataProc2);

        // Conditional select.
        Add(DpReg, 0x7FE00C00, 0x1A800000, "csel", CondSelect);
        Add(DpReg, 0x7FE00C00, 0x1A800400, "csinc", CondSelect);
        Add(DpReg, 0x7FE00C00, 0x5A800000, "csinv", CondSelect);
        Add(DpReg, 0x7FE00C00, 0x5A800400, "csneg", CondSelect);

        // Conditional compare.
        Add(DpReg, 0x7FE00C10, 0x3A400000, "ccmn", CondCompareReg);
        Add(DpReg, 0x7FE00C10, 0x7A400000, "ccmp", CondCompareReg);
        Add(DpReg, 0x7FE00C10, 0x3A400800, "ccmn", CondCompareImm);
        Add(DpReg, 0x7FE00C10, 0x7A400800, "ccmp", CondCompareImm);

        // Data-processing (2 source).
        Add(DpReg, 0x7FE0FC00, 0x1AC00800, "udiv", DataProc2);
        Add(DpReg, 0x7FE0FC00, 0x1AC00C00, "sdiv", DataProc2);
        Add(DpReg, 0x7FE0FC00, 0x1AC02000, "lslv", DataProc2);
        Add(DpReg, 0x7FE0FC00, 0x1AC02400, "lsrv", DataProc2);
        Add(DpReg, 0x7FE0FC00, 0x1AC02800, "asrv", DataProc2);
        Add(DpReg, 0x7FE0FC00, 0x1AC02C00, "rorv", DataProc2);

        // Data-processing (1 source).
        Add(DpReg, 0x7FFFFC00, 0x5AC00000, "rbit", DataProc1);
        Add(DpReg, 0x7FFFFC00, 0x5AC00400, "rev16", DataProc1);
        Add(DpReg, 0x7FFFFC00, 0x5AC00800, "rev", DataProc1);
        Add(DpReg, 0x7FFFFC00, 0x5AC01000, "clz", DataProc1);
        Add(DpReg, 0x7FFFFC00, 0x5AC01400, "cls", DataProc1);

        // Data-processing (3 source).
        Add(DpReg, 0x7FE08000, 0x1B000000, "madd", DataProc3);
        Add(DpReg, 0x7FE08000, 0x1B008000, "msub", DataProc3);
    }
}
