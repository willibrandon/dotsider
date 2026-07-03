namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One native symbol recovered from a binary: a function, a compiler-generated data blob, or a
/// nameless boundary. The address is carried in every form a consumer might need — virtual
/// address for display and cross-symbol ordering, PE RVA, file offset when the address is
/// file-backed, and the containing section — so the UI, hex views, and disassembly never have
/// to recompute a mapping.
/// </summary>
/// <param name="Name">The raw symbol name (mangled for managed code), or a synthesized <c>sub_…</c> for a boundary.</param>
/// <param name="ManagedName">The managed name joined from the binary's recovered metadata, or null when no join exists. Overloads share a name, so this alone does not pin a member — <see cref="IsExactMatch"/> is the precision flag.</param>
/// <param name="VirtualAddress">The symbol's virtual address (image base + RVA on PE; the symbol VA on ELF/Mach-O).</param>
/// <param name="Rva">The PE relative virtual address, or null for non-PE images.</param>
/// <param name="FileOffset">The file offset the address maps to, or null when the symbol is not file-backed.</param>
/// <param name="Section">The containing section's name, or null when it could not be determined.</param>
/// <param name="Size">The symbol's size in bytes, derived when the format does not record it directly.</param>
/// <param name="Kind">What the symbol represents.</param>
/// <param name="SourceFile">The declaring source file, when debug line info is present.</param>
/// <param name="Line">The declaring source line, when debug line info is present.</param>
/// <param name="IsExactMatch">Whether <see cref="ManagedName"/> identifies exactly one recovered member; false when the join is ambiguous (overloads sharing a name, or an overload-suffix join).</param>
/// <param name="Aliases">Alternate names that resolved to the same address and were merged into this symbol.</param>
public sealed record NativeSymbol(
    string Name,
    string? ManagedName,
    ulong VirtualAddress,
    uint? Rva,
    long? FileOffset,
    string? Section,
    long Size,
    NativeSymbolKind Kind,
    string? SourceFile,
    int? Line,
    bool IsExactMatch,
    IReadOnlyList<string> Aliases);
