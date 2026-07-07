namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Parsed facts for a WebAssembly module, including its functions and optional .NET symbol map.
/// </summary>
/// <param name="Version">The WebAssembly binary version.</param>
/// <param name="Sections">The section table in file order.</param>
/// <param name="Types">The parsed function type entries.</param>
/// <param name="Imports">The parsed import entries.</param>
/// <param name="Exports">The parsed export entries.</param>
/// <param name="Functions">Imported and defined functions in function-index order.</param>
/// <param name="Tables">The parsed table declarations.</param>
/// <param name="Memories">The parsed memory declarations.</param>
/// <param name="Globals">The parsed global declarations.</param>
/// <param name="Elements">The parsed element segments.</param>
/// <param name="DataSegments">The parsed data segments.</param>
/// <param name="Tags">The parsed exception tag declarations.</param>
/// <param name="StartFunctionIndex">The start function index, when present.</param>
/// <param name="DataCount">The data-count section value, when present.</param>
/// <param name="TargetFeatures">Feature names from the <c>target_features</c> custom section.</param>
/// <param name="ProducerFields">Producer strings from the <c>producers</c> custom section.</param>
/// <param name="SymbolMapPath">The symbol-map sidecar path, when loaded.</param>
/// <param name="SymbolMapStatus">The symbol-map probe outcome.</param>
/// <param name="SymbolMapEntryCount">The number of parsed sidecar entries.</param>
/// <param name="Diagnostic">A diagnostic note when parsing had to degrade, or null.</param>
public sealed record WasmModuleInfo(
    int Version,
    IReadOnlyList<WasmSectionInfo> Sections,
    IReadOnlyList<WasmTypeInfo> Types,
    IReadOnlyList<WasmImportInfo> Imports,
    IReadOnlyList<WasmExportInfo> Exports,
    IReadOnlyList<WasmFunctionInfo> Functions,
    IReadOnlyList<WasmTableInfo> Tables,
    IReadOnlyList<WasmMemoryInfo> Memories,
    IReadOnlyList<WasmGlobalInfo> Globals,
    IReadOnlyList<WasmElementSegmentInfo> Elements,
    IReadOnlyList<WasmDataSegmentInfo> DataSegments,
    IReadOnlyList<WasmTagInfo> Tags,
    int? StartFunctionIndex,
    int? DataCount,
    IReadOnlyList<string> TargetFeatures,
    IReadOnlyList<string> ProducerFields,
    string? SymbolMapPath,
    WasmSymbolMapStatus SymbolMapStatus,
    int SymbolMapEntryCount,
    string? Diagnostic)
{
    /// <summary>The number of imported functions in the module.</summary>
    public int ImportedFunctionCount => Functions.Count(f => f.IsImported);

    /// <summary>The number of defined functions with code bodies in the module.</summary>
    public int DefinedFunctionCount => Functions.Count(f => !f.IsImported);

    /// <summary>The total byte count of all defined function instruction streams.</summary>
    public long CodeSize => Functions.Sum(f => (long)f.CodeSize);

    /// <summary>The total byte count of all data segments.</summary>
    public long DataSize => DataSegments.Sum(s => (long)s.Size);
}
