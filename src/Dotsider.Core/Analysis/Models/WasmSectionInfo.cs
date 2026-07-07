namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One section in a WebAssembly module, including custom sections.
/// </summary>
/// <param name="Id">The numeric section id.</param>
/// <param name="Name">The standard section name, or the custom section name for id 0.</param>
/// <param name="FileOffset">The file offset where section payload bytes begin.</param>
/// <param name="Size">The section payload size in bytes.</param>
public sealed record WasmSectionInfo(
    byte Id,
    string Name,
    long FileOffset,
    int Size);
