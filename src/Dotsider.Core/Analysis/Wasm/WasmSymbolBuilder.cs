using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Wasm;

/// <summary>
/// Projects a parsed WebAssembly module into native-symbol records for shared disassembly surfaces.
/// </summary>
internal static class WasmSymbolBuilder
{
    /// <summary>
    /// Builds native symbols for every defined, file-backed Wasm function.
    /// </summary>
    /// <param name="module">The parsed WebAssembly module.</param>
    /// <returns>A native-symbol info object using WebAssembly provenance and Wasm32 architecture.</returns>
    public static NativeSymbolInfo Build(WasmModuleInfo module)
    {
        var symbols = module.Functions
            .Where(static f => !f.IsImported && f.CodeOffset is not null && f.CodeSize > 0)
            .Select(static f =>
            {
                var aliases = f.ExportNames
                    .Where(name => !string.Equals(name, f.Name, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                aliases.Add($"func:{f.Index}");
                aliases.Add($"func_{f.Index}");

                return new NativeSymbol(
                    Name: f.Name,
                    ManagedName: null,
                    VirtualAddress: (ulong)f.CodeOffset!.Value,
                    Rva: null,
                    FileOffset: f.CodeOffset,
                    Section: "code",
                    Size: f.CodeSize,
                    Kind: NativeSymbolKind.Function,
                    SourceFile: null,
                    Line: null,
                    IsExactMatch: true,
                    Aliases: aliases);
            })
            .OrderBy(static s => s.VirtualAddress)
            .ToList();

        var diagnostic = module.SymbolMapStatus switch
        {
            WasmSymbolMapStatus.Loaded => null,
            WasmSymbolMapStatus.Corrupt => $"symbol map '{module.SymbolMapPath}' could not be parsed",
            _ => "dotnet.native.js.symbols not found; using Wasm name/export/synthetic names",
        };

        return new NativeSymbolInfo(
            symbols,
            NativeSymbolSource.WebAssembly,
            symbols.Count > 0 ? NativeSymbolStatus.Loaded : NativeSymbolStatus.NoSymbolFile,
            module.SymbolMapPath,
            diagnostic,
            NativeArchitecture.Wasm32,
            SourceMap: null);
    }
}
