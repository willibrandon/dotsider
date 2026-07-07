namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A WebAssembly function, imported or defined in the code section.
/// </summary>
/// <param name="Index">The module-wide function index, including imported functions.</param>
/// <param name="TypeIndex">The function type index, when known.</param>
/// <param name="Name">The best display name for the function.</param>
/// <param name="NameSource">Where the display name came from.</param>
/// <param name="IsImported">Whether the function is imported and has no body in this module.</param>
/// <param name="ImportModule">The import module for imported functions.</param>
/// <param name="ImportName">The import name for imported functions.</param>
/// <param name="IsExported">Whether the function is exported.</param>
/// <param name="ExportNames">All export names that point at this function.</param>
/// <param name="BodyOffset">The file offset of the function body payload, including local declarations.</param>
/// <param name="BodySize">The body payload size in bytes.</param>
/// <param name="CodeOffset">The file offset of the first instruction byte after local declarations.</param>
/// <param name="CodeSize">The instruction byte count after local declarations.</param>
/// <param name="Locals">The function's run-length encoded local declarations.</param>
/// <param name="ParamTypes">The raw Wasm parameter type bytes.</param>
/// <param name="ResultTypes">The raw Wasm result type bytes.</param>
public sealed record WasmFunctionInfo(
    int Index,
    int? TypeIndex,
    string Name,
    string NameSource,
    bool IsImported,
    string? ImportModule,
    string? ImportName,
    bool IsExported,
    IReadOnlyList<string> ExportNames,
    long? BodyOffset,
    int BodySize,
    long? CodeOffset,
    int CodeSize,
    IReadOnlyList<WasmLocalInfo> Locals,
    IReadOnlyList<byte> ParamTypes,
    IReadOnlyList<byte> ResultTypes);
