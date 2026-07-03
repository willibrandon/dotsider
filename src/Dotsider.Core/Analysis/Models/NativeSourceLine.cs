namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One address→source mapping row recovered from a native sidecar (PDB C13 line table, DWARF/dSYM
/// line program): the virtual address a source line begins at, its byte length, and the file and
/// 1-based line number.
/// </summary>
/// <param name="Address">The virtual address the source line begins at.</param>
/// <param name="Length">The number of bytes the row covers.</param>
/// <param name="File">The source file path.</param>
/// <param name="Line">The 1-based source line number.</param>
public sealed record NativeSourceLine(ulong Address, uint Length, string File, int Line);
