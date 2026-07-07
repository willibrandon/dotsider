namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One WebAssembly memory declaration.
/// </summary>
/// <param name="Index">The zero-based memory index.</param>
/// <param name="MinimumPages">The minimum memory page count.</param>
/// <param name="MaximumPages">The maximum memory page count when one is declared.</param>
/// <param name="IsShared">Whether the memory is declared shared.</param>
/// <param name="IsMemory64">Whether the memory uses 64-bit indices.</param>
public sealed record WasmMemoryInfo(
    int Index,
    ulong MinimumPages,
    ulong? MaximumPages,
    bool IsShared,
    bool IsMemory64);
