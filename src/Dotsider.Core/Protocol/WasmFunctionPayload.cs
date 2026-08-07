namespace Dotsider.Core.Protocol;

/// <summary>
/// A WebAssembly function row.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record WasmFunctionPayload(
    int Index,
    string Name,
    string NameSource,
    bool IsImported,
    string? ImportModule,
    string? ImportName,
    bool IsExported,
    IReadOnlyList<string> ExportNames,
    int? TypeIndex,
    long? BodyOffset,
    int BodySize,
    long? CodeOffset,
    int CodeSize,
    IReadOnlyList<string> ParamTypes,
    IReadOnlyList<string> ResultTypes);
