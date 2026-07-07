namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One WebAssembly import entry.
/// </summary>
/// <param name="ModuleName">The imported module name.</param>
/// <param name="Name">The imported item name.</param>
/// <param name="Kind">The imported external kind.</param>
/// <param name="Index">The import's index within its index space when applicable.</param>
/// <param name="TypeIndex">The function type index for function imports, or null.</param>
public sealed record WasmImportInfo(
    string ModuleName,
    string Name,
    WasmExternalKind Kind,
    int Index,
    int? TypeIndex);
