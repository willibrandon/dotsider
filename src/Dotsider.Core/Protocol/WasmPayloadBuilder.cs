using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Builds JSON-ready WebAssembly payloads shared by direct MCP tools, the CLI, and the
/// diagnostics session protocol. The payloads describe raw SDK-produced Wasm modules such as
/// <c>dotnet.native.wasm</c>, not ECMA-335 metadata assemblies.
/// </summary>
public static class WasmPayloadBuilder
{
    /// <summary>
    /// Builds a compact WebAssembly module summary for protocol surfaces. Returns null when the
    /// analyzer is not a raw Wasm module so callers can include it unconditionally beside other
    /// binary-kind summaries.
    /// </summary>
    /// <param name="analyzer">The analyzer whose raw Wasm summary should be serialized.</param>
    /// <returns>A JSON-ready summary object, or null when the analyzer is not raw Wasm.</returns>
    public static WasmSummary? BuildSummary(AssemblyAnalyzer analyzer)
    {
        if (analyzer.WasmModuleInfo is not { } wasm)
            return null;

        return new WasmSummary(
            wasm.Version,
            wasm.Sections.Count,
            wasm.Types.Count,
            wasm.Imports.Count,
            wasm.Exports.Count,
            wasm.Functions.Count,
            wasm.ImportedFunctionCount,
            wasm.DefinedFunctionCount,
            wasm.CodeSize,
            wasm.Tables.Count,
            wasm.Memories.Count,
            wasm.Globals.Count,
            wasm.Elements.Count,
            wasm.DataSegments.Count,
            wasm.DataSize,
            wasm.Tags.Count,
            wasm.StartFunctionIndex,
            wasm.DataCount,
            wasm.SymbolMapPath,
            wasm.SymbolMapStatus.ToString(),
            wasm.SymbolMapEntryCount,
            wasm.TargetFeatures,
            wasm.ProducerFields,
            wasm.Diagnostic);
    }

    /// <summary>
    /// Builds a section-table payload for a WebAssembly module. Each section keeps its raw id,
    /// display name, file payload offset, and payload size so callers can jump to the bytes that
    /// the SDK emitted.
    /// </summary>
    /// <param name="analyzer">The analyzer that opened a raw Wasm module.</param>
    /// <returns>A JSON-ready section table payload.</returns>
    public static WasmSectionsPayload BuildSections(AssemblyAnalyzer analyzer)
    {
        var wasm = RequireWasm(analyzer);
        return new WasmSectionsPayload(
            analyzer.FilePath,
            wasm.Sections.Count,
            [.. wasm.Sections.Select(static s => new WasmSectionPayload(
                s.Id,
                s.Name,
                s.FileOffset,
                s.Size))]);
    }

    /// <summary>
    /// Builds a function inventory for a WebAssembly module. Imported functions and file-backed
    /// defined functions share the same Wasm function-index space, matching direct call operands
    /// and symbol-map entries.
    /// </summary>
    /// <param name="analyzer">The analyzer that opened a raw Wasm module.</param>
    /// <returns>A JSON-ready function inventory payload.</returns>
    public static WasmFunctionsPayload BuildFunctions(AssemblyAnalyzer analyzer)
    {
        var wasm = RequireWasm(analyzer);
        return new WasmFunctionsPayload(
            analyzer.FilePath,
            wasm.Functions.Count,
            [.. wasm.Functions.Select(static f => new WasmFunctionPayload(
                f.Index,
                f.Name,
                f.NameSource,
                f.IsImported,
                f.ImportModule,
                f.ImportName,
                f.IsExported,
                f.ExportNames,
                f.TypeIndex,
                f.BodyOffset,
                f.BodySize,
                f.CodeOffset,
                f.CodeSize,
                [.. f.ParamTypes.Select(ValueTypeName)],
                [.. f.ResultTypes.Select(ValueTypeName)]))]);
    }

    private static WasmModuleInfo RequireWasm(AssemblyAnalyzer analyzer)
    {
        if (analyzer.WasmModuleInfo is { } wasm)
            return wasm;

        throw new InvalidOperationException("The analyzer is not a WebAssembly module.");
    }

    private static string ValueTypeName(byte valueType) => valueType switch
    {
        0x7F => "i32",
        0x7E => "i64",
        0x7D => "f32",
        0x7C => "f64",
        0x7B => "v128",
        0x70 => "funcref",
        0x6F => "externref",
        _ => $"0x{valueType:X2}",
    };
}
