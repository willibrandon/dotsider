using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Verifies raw WebAssembly support against a real SDK-produced browser-wasm module.
/// The fixture publish emits <c>dotnet.native.wasm</c> and <c>dotnet.native.js.symbols</c>.
/// These tests cover the public analyzer path users exercise when they open the module directly.
/// </summary>
[TestClass]
public sealed class WasmSdkModuleDecoderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Opening <c>dotnet.native.wasm</c> classifies the file as WebAssembly, reports Wasm32,
    /// and preserves the module's section, import, export, function, and symbol-map facts.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BrowserWasmNativeModule_OpensAsWasmModule()
    {
        using var analyzer = OpenWasmFixture();

        Assert.IsFalse(analyzer.HasMetadata);
        Assert.AreEqual(BinaryKind.Wasm, analyzer.BinaryKind);
        Assert.AreEqual("Wasm32", analyzer.Architecture);

        var wasm = analyzer.WasmModuleInfo;
        Assert.IsNotNull(wasm);
        Assert.AreEqual(1, wasm.Version);
        Assert.IsNotEmpty(wasm.Sections);
        Assert.IsGreaterThan(0, wasm.ImportedFunctionCount);
        Assert.IsGreaterThan(0, wasm.DefinedFunctionCount);
        Assert.IsGreaterThan(0, wasm.CodeSize);
        Assert.IsGreaterThan(0, wasm.DataSize);
        Assert.AreEqual(WasmSymbolMapStatus.Loaded, wasm.SymbolMapStatus);
        Assert.IsGreaterThan(wasm.DefinedFunctionCount / 2, wasm.SymbolMapEntryCount);
    }

    /// <summary>
    /// The raw Wasm reader parses standard SDK-emitted sections into model facts instead of only
    /// preserving their raw section headers.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BrowserWasmNativeModule_ParsesStandardSections()
    {
        using var analyzer = OpenWasmFixture();

        var wasm = analyzer.WasmModuleInfo;
        Assert.IsNotNull(wasm);
        Assert.IsNotEmpty(wasm.Types);
        TestAssert.All(wasm.Functions.Where(static f => f.TypeIndex is not null), f =>
            Assert.IsInRange(0, wasm.Types.Count - 1, f.TypeIndex!.Value));

        AssertParsedWhenSectionExists(wasm, 4, wasm.Tables.Count, "table");
        AssertParsedWhenSectionExists(wasm, 5, wasm.Memories.Count, "memory");
        AssertParsedWhenSectionExists(wasm, 6, wasm.Globals.Count, "global");
        AssertParsedWhenSectionExists(wasm, 9, wasm.Elements.Count, "element");
        AssertParsedWhenSectionExists(wasm, 13, wasm.Tags.Count, "tag");
        AssertDefinedIndexesStartAfterImports(wasm);

        if (wasm.Sections.Any(static s => s.Id == 8))
            Assert.IsNotNull(wasm.StartFunctionIndex);
        if (wasm.Sections.Any(static s => s.Id == 12))
            Assert.AreEqual(wasm.DataSegments.Count, wasm.DataCount);
    }

    /// <summary>
    /// A browser-wasm publish with <c>RunAOTCompilation=true</c> still opens through the raw
    /// WebAssembly path and exposes real file-backed Wasm functions.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BrowserWasmAotNativeModule_OpensAsWasmModule()
    {
        TestSkip.When(Samples.WasmConsoleAotNativeWasm is null,
            "browser-wasm AOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(Samples.WasmConsoleAotNativeWasm!);

        Assert.IsFalse(analyzer.HasMetadata);
        Assert.AreEqual(BinaryKind.Wasm, analyzer.BinaryKind);
        var wasm = analyzer.WasmModuleInfo;
        Assert.IsNotNull(wasm);
        Assert.IsGreaterThan(0, wasm.DefinedFunctionCount);
        Assert.IsGreaterThan(0, wasm.CodeSize);
        Assert.IsNotEmpty(analyzer.NativeSymbols?.Symbols ?? []);
    }

    /// <summary>
    /// Opening a Webcil-wrapped <c>.wasm</c> app assembly unwraps to managed metadata and IL
    /// instead of presenting the wrapper as native runtime WebAssembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BrowserWasmWebcilAssembly_OpensAsManagedMetadata()
    {
        using var analyzer = OpenWebcilFixture();

        Assert.IsTrue(analyzer.HasMetadata);
        Assert.AreEqual(BinaryKind.Managed, analyzer.BinaryKind);
        Assert.IsNotNull(analyzer.WebcilInfo);
        Assert.IsNull(analyzer.WasmModuleInfo);
        Assert.Contains(static t => t.FullName == "WasmCalculator", analyzer.TypeDefs);

        var method = analyzer.MethodDefs.First(static m =>
            m.DeclaringType == "WasmCalculator" && m.Name == "Add");
        var il = new IlDisassembler(analyzer).DisassembleWithText(method);
        Assert.IsNotNull(il);
        Assert.Contains("IL_", il.Value.Text);
        Assert.Contains("ldarg", il.Value.Text);
    }

    /// <summary>
    /// WebAssembly functions become native symbols with WebAssembly provenance and file offsets.
    /// The symbol names come from the SDK sidecar when <c>dotnet.native.js.symbols</c> is present.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BrowserWasmNativeModule_NativeSymbolsUseSymbolMap()
    {
        using var analyzer = OpenWasmFixture();

        var info = analyzer.NativeSymbols;
        Assert.IsNotNull(info);
        Assert.AreEqual(NativeSymbolSource.WebAssembly, info.Source);
        Assert.AreEqual(NativeSymbolStatus.Loaded, info.Status);
        Assert.AreEqual(NativeArchitecture.Wasm32, info.Architecture);
        Assert.IsNotNull(info.Path);
        Assert.IsNotEmpty(info.Symbols);
        TestAssert.All(info.Symbols.Take(100), symbol =>
        {
            Assert.AreEqual(NativeSymbolKind.Function, symbol.Kind);
            Assert.IsNotNull(symbol.FileOffset);
            Assert.IsGreaterThan(0, symbol.Size);
        });
    }

    /// <summary>
    /// Disassembling a real SDK-produced Wasm function produces full instruction coverage and
    /// resolves direct call operands to imported or defined function names.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BrowserWasmNativeModule_DisassemblesRealFunctionAndNamesDirectCalls()
    {
        using var analyzer = OpenWasmFixture();
        var symbol = FindFunctionWithNamedCall(analyzer);

        var result = NativeDisassembler.DisassembleSymbol(analyzer, symbol);
        Assert.IsNotNull(result);

        var instructions = result.Value.Instructions;
        Assert.IsNotEmpty(instructions);
        Assert.DoesNotContain(static instruction => instruction.IsFallback, instructions);
        Assert.AreEqual(symbol.Size, instructions.Sum(static instruction => instruction.Length));
        Assert.Contains(static instruction =>
            instruction.Mnemonic is "call" or "return_call"
            && instruction.TargetName is not null, instructions);
    }

    /// <summary>
    /// Wasm disassembly annotates function-index, local, and table/type operands without treating
    /// function indexes as native virtual addresses.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BrowserWasmNativeModule_DisassemblyAnnotatesWasmOperands()
    {
        using var analyzer = OpenWasmFixture();
        var info = analyzer.NativeSymbols;
        Assert.IsNotNull(info);

        var annotated = info.Symbols
            .Take(512)
            .Select(symbol => NativeDisassembler.DisassembleSymbol(analyzer, symbol))
            .Where(static result => result is not null)
            .SelectMany(static result => result!.Value.Instructions)
            .Where(static instruction => instruction.OperandText.Contains('<', StringComparison.Ordinal))
            .ToList();

        Assert.Contains(static instruction =>
            instruction.Mnemonic is "call" or "return_call"
            && instruction.TargetName is not null, annotated);
        Assert.Contains(static instruction =>
            instruction.Mnemonic is "local.get" or "local.set" or "local.tee", annotated);
    }

    /// <summary>
    /// The Size Map treats a raw Wasm module as native code: function bodies, data segments, and
    /// remaining sections are sized from file-backed Wasm payloads rather than managed IL.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BrowserWasmNativeModule_SizeTreeUsesWasmPayloads()
    {
        using var analyzer = OpenWasmFixture();

        var tree = SizeAnalyzer.BuildSizeTree(analyzer);
        Assert.IsTrue(tree.Name.EndsWith("(Wasm)", StringComparison.Ordinal), tree.Name);
        Assert.IsGreaterThan(0, tree.Size);
        Assert.Contains(static child => child.Name == "Functions" && child.Size > 0, tree.Children);
        Assert.Contains(static child => child.Name == "Data" && child.Size > 0, tree.Children);
        Assert.Contains(static child => child.Name == "Sections" && child.Size > 0, tree.Children);
    }

    private static AssemblyAnalyzer OpenWasmFixture()
    {
        TestSkip.When(
            Samples.WasmConsoleNativeWasm is null && Samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return new AssemblyAnalyzer(Samples.WasmConsoleNativeWasm ?? Samples.ReadyToRunConsoleWasmNativeWasm!);
    }

    private static AssemblyAnalyzer OpenWebcilFixture()
    {
        TestSkip.When(
            Samples.WasmConsoleWebcilWasm is null,
            "browser-wasm publish did not produce the Webcil app assembly on this leg.");

        return new AssemblyAnalyzer(Samples.WasmConsoleWebcilWasm!);
    }

    private static NativeSymbol FindFunctionWithNamedCall(AssemblyAnalyzer analyzer)
    {
        var info = analyzer.NativeSymbols;
        Assert.IsNotNull(info);
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
            Assert.IsGreaterThan(0, parsedCount, $"Expected parsed {name} entries for section {sectionId}.");
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

        TestAssert.All(values, index =>
            Assert.IsGreaterThanOrEqualTo(importCount, index, $"{label} index {index} should account for {importCount} imports."));
    }
}
