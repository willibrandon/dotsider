namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One WebAssembly export entry.
/// </summary>
/// <param name="Name">The exported name.</param>
/// <param name="Kind">The exported external kind.</param>
/// <param name="Index">The exported index in the kind's index space.</param>
public sealed record WasmExportInfo(string Name, WasmExternalKind Kind, int Index);
