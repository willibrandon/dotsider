namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One WebAssembly data segment.
/// </summary>
/// <param name="Index">The data segment index.</param>
/// <param name="Mode">The decoded segment mode: active, passive, or active-explicit-memory.</param>
/// <param name="FileOffset">The file offset where the segment's bytes begin.</param>
/// <param name="Size">The segment byte size.</param>
public sealed record WasmDataSegmentInfo(int Index, string Mode, long FileOffset, int Size);
