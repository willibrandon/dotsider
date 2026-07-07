using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Verifies raw WebAssembly support against a real SDK-produced browser-wasm module.
/// The fixture publish emits <c>dotnet.native.wasm</c> and <c>dotnet.native.js.symbols</c>.
/// These tests cover the public analyzer path users exercise when they open the module directly.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class WasmSdkModuleDecoderTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Opening <c>dotnet.native.wasm</c> classifies the file as WebAssembly, reports Wasm32,
    /// and preserves the module's section, import, export, function, and symbol-map facts.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BrowserWasmNativeModule_OpensAsWasmModule()
    {
        using var analyzer = OpenWasmFixture();

        Assert.False(analyzer.HasMetadata);
        Assert.Equal(BinaryKind.Wasm, analyzer.BinaryKind);
        Assert.Equal("Wasm32", analyzer.Architecture);

        var wasm = analyzer.WasmModuleInfo;
        Assert.NotNull(wasm);
        Assert.Equal(1, wasm.Version);
        Assert.NotEmpty(wasm.Sections);
        Assert.True(wasm.ImportedFunctionCount > 0);
        Assert.True(wasm.DefinedFunctionCount > 0);
        Assert.True(wasm.CodeSize > 0);
        Assert.True(wasm.DataSize > 0);
        Assert.Equal(WasmSymbolMapStatus.Loaded, wasm.SymbolMapStatus);
        Assert.True(wasm.SymbolMapEntryCount > wasm.DefinedFunctionCount / 2);
    }

    /// <summary>
    /// The raw Wasm reader parses standard SDK-emitted sections into model facts instead of only
    /// preserving their raw section headers.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BrowserWasmNativeModule_ParsesStandardSections()
    {
        using var analyzer = OpenWasmFixture();

        var wasm = analyzer.WasmModuleInfo;
        Assert.NotNull(wasm);
        Assert.NotEmpty(wasm.Types);
        Assert.All(wasm.Functions.Where(static f => f.TypeIndex is not null), f =>
            Assert.InRange(f.TypeIndex!.Value, 0, wasm.Types.Count - 1));

        AssertParsedWhenSectionExists(wasm, 4, wasm.Tables.Count, "table");
        AssertParsedWhenSectionExists(wasm, 5, wasm.Memories.Count, "memory");
        AssertParsedWhenSectionExists(wasm, 6, wasm.Globals.Count, "global");
        AssertParsedWhenSectionExists(wasm, 9, wasm.Elements.Count, "element");
        AssertParsedWhenSectionExists(wasm, 13, wasm.Tags.Count, "tag");
        AssertDefinedIndexesStartAfterImports(wasm);

        if (wasm.Sections.Any(static s => s.Id == 8))
            Assert.NotNull(wasm.StartFunctionIndex);
        if (wasm.Sections.Any(static s => s.Id == 12))
            Assert.Equal(wasm.DataSegments.Count, wasm.DataCount);
    }

    /// <summary>
    /// A browser-wasm publish with <c>RunAOTCompilation=true</c> still opens through the raw
    /// WebAssembly path and exposes real file-backed Wasm functions.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BrowserWasmAotNativeModule_OpensAsWasmModule()
    {
        Assert.SkipWhen(samples.WasmConsoleAotNativeWasm is null,
            "browser-wasm AOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(samples.WasmConsoleAotNativeWasm!);

        Assert.False(analyzer.HasMetadata);
        Assert.Equal(BinaryKind.Wasm, analyzer.BinaryKind);
        var wasm = analyzer.WasmModuleInfo;
        Assert.NotNull(wasm);
        Assert.True(wasm.DefinedFunctionCount > 0);
        Assert.True(wasm.CodeSize > 0);
        Assert.NotEmpty(analyzer.NativeSymbols?.Symbols ?? []);
    }

    /// <summary>
    /// Opening a Webcil-wrapped <c>.wasm</c> app assembly unwraps to managed metadata and IL
    /// instead of presenting the wrapper as native runtime WebAssembly.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BrowserWasmWebcilAssembly_OpensAsManagedMetadata()
    {
        using var analyzer = OpenWebcilFixture();

        Assert.True(analyzer.HasMetadata);
        Assert.Equal(BinaryKind.Managed, analyzer.BinaryKind);
        Assert.NotNull(analyzer.WebcilInfo);
        Assert.Null(analyzer.WasmModuleInfo);
        Assert.Contains(analyzer.TypeDefs, static t => t.FullName == "WasmCalculator");

        var method = analyzer.MethodDefs.First(static m =>
            m.DeclaringType == "WasmCalculator" && m.Name == "Add");
        var il = new IlDisassembler(analyzer).DisassembleWithText(method);
        Assert.NotNull(il);
        Assert.Contains("IL_", il.Value.Text);
        Assert.Contains("ldarg", il.Value.Text);
    }

    /// <summary>
    /// WebAssembly functions become native symbols with WebAssembly provenance and file offsets.
    /// The symbol names come from the SDK sidecar when <c>dotnet.native.js.symbols</c> is present.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BrowserWasmNativeModule_NativeSymbolsUseSymbolMap()
    {
        using var analyzer = OpenWasmFixture();

        var info = analyzer.NativeSymbols;
        Assert.NotNull(info);
        Assert.Equal(NativeSymbolSource.WebAssembly, info.Source);
        Assert.Equal(NativeSymbolStatus.Loaded, info.Status);
        Assert.Equal(NativeArchitecture.Wasm32, info.Architecture);
        Assert.NotNull(info.Path);
        Assert.NotEmpty(info.Symbols);
        Assert.All(info.Symbols.Take(100), symbol =>
        {
            Assert.Equal(NativeSymbolKind.Function, symbol.Kind);
            Assert.NotNull(symbol.FileOffset);
            Assert.True(symbol.Size > 0);
        });
    }

    /// <summary>
    /// Disassembling a real SDK-produced Wasm function produces full instruction coverage and
    /// resolves direct call operands to imported or defined function names.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BrowserWasmNativeModule_DisassemblesRealFunctionAndNamesDirectCalls()
    {
        using var analyzer = OpenWasmFixture();
        var symbol = FindFunctionWithNamedCall(analyzer);

        var result = NativeDisassembler.DisassembleSymbol(analyzer, symbol);
        Assert.NotNull(result);

        var instructions = result.Value.Instructions;
        Assert.NotEmpty(instructions);
        Assert.DoesNotContain(instructions, static instruction => instruction.IsFallback);
        Assert.Equal(symbol.Size, instructions.Sum(static instruction => instruction.Length));
        Assert.Contains(instructions, static instruction =>
            instruction.Mnemonic is "call" or "return_call"
            && instruction.TargetName is not null);
    }

    /// <summary>
    /// Wasm disassembly annotates function-index, local, and table/type operands without treating
    /// function indexes as native virtual addresses.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BrowserWasmNativeModule_DisassemblyAnnotatesWasmOperands()
    {
        using var analyzer = OpenWasmFixture();
        var info = analyzer.NativeSymbols;
        Assert.NotNull(info);

        var annotated = info.Symbols
            .Take(512)
            .Select(symbol => NativeDisassembler.DisassembleSymbol(analyzer, symbol))
            .Where(static result => result is not null)
            .SelectMany(static result => result!.Value.Instructions)
            .Where(static instruction => instruction.OperandText.Contains('<', StringComparison.Ordinal))
            .ToList();

        Assert.Contains(annotated, static instruction =>
            instruction.Mnemonic is "call" or "return_call"
            && instruction.TargetName is not null);
        Assert.Contains(annotated, static instruction =>
            instruction.Mnemonic is "local.get" or "local.set" or "local.tee");
    }

    /// <summary>
    /// The Size Map treats a raw Wasm module as native code: function bodies, data segments, and
    /// remaining sections are sized from file-backed Wasm payloads rather than managed IL.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BrowserWasmNativeModule_SizeTreeUsesWasmPayloads()
    {
        using var analyzer = OpenWasmFixture();

        var tree = SizeAnalyzer.BuildSizeTree(analyzer);
        Assert.True(tree.Name.EndsWith("(Wasm)", StringComparison.Ordinal), tree.Name);
        Assert.True(tree.Size > 0);
        Assert.Contains(tree.Children, static child => child.Name == "Functions" && child.Size > 0);
        Assert.Contains(tree.Children, static child => child.Name == "Data" && child.Size > 0);
        Assert.Contains(tree.Children, static child => child.Name == "Sections" && child.Size > 0);
    }

    private AssemblyAnalyzer OpenWasmFixture()
    {
        Assert.SkipWhen(
            samples.WasmConsoleNativeWasm is null && samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return new AssemblyAnalyzer(samples.WasmConsoleNativeWasm ?? samples.ReadyToRunConsoleWasmNativeWasm!);
    }

    private AssemblyAnalyzer OpenWebcilFixture()
    {
        Assert.SkipWhen(
            samples.WasmConsoleWebcilWasm is null,
            "browser-wasm publish did not produce the Webcil app assembly on this leg.");

        return new AssemblyAnalyzer(samples.WasmConsoleWebcilWasm!);
    }

    private static NativeSymbol FindFunctionWithNamedCall(AssemblyAnalyzer analyzer)
    {
        var info = analyzer.NativeSymbols;
        Assert.NotNull(info);
        foreach (var symbol in info.Symbols.Take(512))
        {
            var result = NativeDisassembler.DisassembleSymbol(analyzer, symbol);
            if (result is null)
                continue;

            if (result.Value.Instructions.Any(static instruction =>
                    instruction.Mnemonic is "call" or "return_call"
                    && instruction.TargetName is not null))
            {
                return symbol;
            }
        }

        throw new InvalidOperationException("No Wasm function with a named direct call was found.");
    }

    private static void AssertParsedWhenSectionExists(WasmModuleInfo wasm, byte sectionId, int parsedCount, string name)
    {
        if (wasm.Sections.Any(s => s.Id == sectionId))
            Assert.True(parsedCount > 0, $"Expected parsed {name} entries for section {sectionId}.");
    }

    private static void AssertDefinedIndexesStartAfterImports(WasmModuleInfo wasm)
    {
        AssertIndexesStartAfterImports(
            wasm.Imports.Count(static i => i.Kind == WasmExternalKind.Table),
            wasm.Tables.Select(static t => t.Index),
            "table");
        AssertIndexesStartAfterImports(
            wasm.Imports.Count(static i => i.Kind == WasmExternalKind.Memory),
            wasm.Memories.Select(static m => m.Index),
            "memory");
        AssertIndexesStartAfterImports(
            wasm.Imports.Count(static i => i.Kind == WasmExternalKind.Global),
            wasm.Globals.Select(static g => g.Index),
            "global");
        AssertIndexesStartAfterImports(
            wasm.Imports.Count(static i => i.Kind == WasmExternalKind.Tag),
            wasm.Tags.Select(static t => t.Index),
            "tag");
    }

    private static void AssertIndexesStartAfterImports(int importCount, IEnumerable<int> indexes, string label)
    {
        var values = indexes.ToList();
        if (values.Count == 0)
            return;

        Assert.All(values, index =>
            Assert.True(index >= importCount, $"{label} index {index} should account for {importCount} imports."));
    }
}
