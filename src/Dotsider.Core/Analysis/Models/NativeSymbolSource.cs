namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Where a binary's native symbols came from. The three primary sources carry names and (mostly)
/// sizes; the three fallback sources recover only function boundaries from unwind data and are
/// lower fidelity — they can miss leaf and thunk functions.
/// </summary>
public enum NativeSymbolSource
{
    /// <summary>A matched Windows native PDB (MSF container).</summary>
    NativePdb,

    /// <summary>DWARF debug info in an unstripped ELF binary or a <c>.dbg</c> sidecar.</summary>
    Dwarf,

    /// <summary>A macOS dSYM bundle (DWARF plus nlist stabs).</summary>
    Dsym,

    /// <summary>The Mach-O symbol table (nlist) of the binary itself.</summary>
    MachONlist,

    /// <summary>PE <c>.pdata</c> exception directory — function boundaries only.</summary>
    PdataFallback,

    /// <summary>ELF <c>.eh_frame</c> unwind info — function boundaries only.</summary>
    EhFrameFallback,

    /// <summary>Mach-O <c>LC_FUNCTION_STARTS</c> — function boundaries only.</summary>
    FunctionStartsFallback,

    /// <summary>
    /// A crossgen2 ReadyToRun image's method entry-point tables: named, sized function ranges
    /// (one per hot/funclet/cold runtime function) recovered directly from the R2R sections.
    /// </summary>
    ReadyToRun
}
