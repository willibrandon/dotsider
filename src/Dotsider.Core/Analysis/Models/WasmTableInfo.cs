namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One WebAssembly table declaration.
/// </summary>
/// <param name="Index">The zero-based table index.</param>
/// <param name="RefType">The table reference type.</param>
/// <param name="Minimum">The minimum element count.</param>
/// <param name="Maximum">The maximum element count when one is declared.</param>
public sealed record WasmTableInfo(
    int Index,
    string RefType,
    ulong Minimum,
    ulong? Maximum);
