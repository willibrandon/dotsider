namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes validated Mach-O symbol and string tables.
/// </summary>
/// <param name="Offset">The symbol table's file offset.</param>
/// <param name="Count">The number of symbol records.</param>
/// <param name="StringOffset">The string table's file offset.</param>
/// <param name="StringSize">The string table's byte size.</param>
internal readonly record struct MachOSymbolTable(
    int Offset,
    int Count,
    int StringOffset,
    int StringSize);
