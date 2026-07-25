namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes a validated Mach-O indirect-symbol table.
/// </summary>
/// <param name="Offset">The table's file offset.</param>
/// <param name="Count">The number of four-byte symbol indexes.</param>
internal readonly record struct MachOIndirectSymbolTable(int Offset, int Count);
