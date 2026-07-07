namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One WebAssembly element segment declaration.
/// </summary>
/// <param name="Index">The zero-based element segment index.</param>
/// <param name="Mode">The decoded element segment mode.</param>
/// <param name="TableIndex">The table index when the mode records one.</param>
/// <param name="ElementType">The reference type or element kind.</param>
/// <param name="ElementCount">The number of recorded element expressions or indices.</param>
public sealed record WasmElementSegmentInfo(
    int Index,
    string Mode,
    int? TableIndex,
    string ElementType,
    int ElementCount);
