namespace Dotsider.Core.Protocol;

/// <summary>
/// Compact WebAssembly module facts.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record WasmSummary(
    int Version,
    int SectionCount,
    int TypeCount,
    int ImportCount,
    int ExportCount,
    int FunctionCount,
    int ImportedFunctionCount,
    int DefinedFunctionCount,
    long CodeSize,
    int TableCount,
    int MemoryCount,
    int GlobalCount,
    int ElementSegmentCount,
    int DataSegmentCount,
    long DataSize,
    int TagCount,
    int? StartFunctionIndex,
    int? DataCount,
    string? SymbolMapPath,
    string SymbolMapStatus,
    int SymbolMapEntryCount,
    IReadOnlyList<string> TargetFeatures,
    IReadOnlyList<string> ProducerFields,
    string? Diagnostic);
