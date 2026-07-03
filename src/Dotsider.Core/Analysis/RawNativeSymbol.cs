namespace Dotsider.Core.Analysis;

/// <summary>
/// A symbol as a format reader (PDB, DWARF, nlist, or a fallback) recovers it, before the facade
/// demangles its name, classifies its kind, and merges duplicates. Addresses are already resolved
/// to the forms the reader could compute; the facade fills in the rest and produces the public
/// <see cref="Models.NativeSymbol"/>.
/// </summary>
/// <param name="Name">The raw (mangled) symbol name, or a synthesized boundary name.</param>
/// <param name="VirtualAddress">The symbol's virtual address.</param>
/// <param name="Rva">The PE relative virtual address, or null for non-PE images.</param>
/// <param name="FileOffset">The file offset the address maps to, or null when not file-backed.</param>
/// <param name="Section">The containing section name, or null when unknown.</param>
/// <param name="Size">The symbol size in bytes, or 0 when the reader could not determine it.</param>
/// <param name="IsData">Whether the reader found this in a data (non-executable) context, so it should classify as data even if the name is unrecognized.</param>
/// <param name="IsBoundary">Whether this is a nameless boundary from a fallback source.</param>
/// <param name="SourceFile">The declaring source file, when available.</param>
/// <param name="Line">The declaring source line, when available.</param>
internal readonly record struct RawNativeSymbol(
    string Name,
    ulong VirtualAddress,
    uint? Rva,
    long? FileOffset,
    string? Section,
    long Size,
    bool IsData,
    bool IsBoundary,
    string? SourceFile,
    int? Line);
