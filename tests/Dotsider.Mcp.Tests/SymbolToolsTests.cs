using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the native-symbol MCP tool: <c>get_native_symbols</c> against the real Native AOT
/// fixture and the managed-assembly error path.
/// </summary>
[TestClass]
public class SymbolToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// get_native_symbols returns the symbol list with its provenance for a Native AOT binary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeSymbols_NativeAot_ReturnsSymbolsWithProvenance()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_symbols",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.IsFalse(text.StartsWith("Error", StringComparison.Ordinal),
            "the tool reported an error instead of symbols");
        Assert.Contains("\"source\"", text);
        Assert.Contains("\"status\"", text);
        Assert.Contains("\"symbols\"", text);
    }

    /// <summary>
    /// get_native_symbols reports an error for a managed assembly instead of an empty list.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeSymbols_Managed_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_symbols",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("Error", text);
        Assert.Contains("managed", text);
    }

    /// <summary>get_native_disassembly decodes a function by address into structured instructions.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeDisassembly_NativeAot_ByAddress_ReturnsInstructions()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        ulong va;
        using (var analyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(Samples.NativeAotConsoleExe!))
        {
            var fn = analyzer.NativeSymbols?.Symbols.FirstOrDefault(s =>
                s.Kind == Dotsider.Core.Analysis.Models.NativeSymbolKind.Function
                && s.ManagedName is not null && s.FileOffset is not null && s.Size > 0);
            Assert.IsNotNull(fn);
            va = fn!.VirtualAddress;
        }

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_disassembly",
            new Dictionary<string, object?> { ["address"] = $"0x{va:x}", ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.IsFalse(text.StartsWith("Error", StringComparison.Ordinal), "the tool reported an error");
        Assert.Contains("\"mnemonic\"", text);
    }

    /// <summary>
    /// get_native_symbols returns WebAssembly function symbols for a raw SDK browser-wasm module.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeSymbols_Wasm_ReturnsWebAssemblySymbols()
    {
        var wasmPath = GetWasmNativePath();

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_symbols",
            new Dictionary<string, object?> { ["assemblyPath"] = wasmPath },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("webAssembly", json.GetProperty("source").GetString());
        Assert.AreEqual("wasm32", json.GetProperty("architecture").GetString());
        Assert.IsGreaterThan(0, json.GetProperty("symbols").GetArrayLength());
    }

    /// <summary>
    /// get_native_disassembly decodes a WebAssembly function from a real <c>dotnet.native.wasm</c>
    /// module through the same native-tool surface used for PE, ELF, Mach-O, and ReadyToRun code.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeDisassembly_Wasm_ByAddress_ReturnsInstructions()
    {
        var wasmPath = GetWasmNativePath();

        string address;
        using (var analyzer = new AssemblyAnalyzer(wasmPath))
        {
            var symbol = FindWasmFunctionWithNamedCall(analyzer);
            address = $"0x{symbol.VirtualAddress:x}";
        }

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_disassembly",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = wasmPath,
                ["address"] = address
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.IsFalse(text.StartsWith("Error", StringComparison.Ordinal), text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("Wasm32", json.GetProperty("architecture").GetString());
        Assert.Contains(static instruction =>
            instruction.TryGetProperty("targetName", out var targetName)
            && targetName.ValueKind == JsonValueKind.String, json.GetProperty("instructions").EnumerateArray());
    }

    /// <summary>
    /// get_native_disassembly accepts WebAssembly <c>func:N</c> identifiers through the same
    /// symbolName parameter used for PE, ELF, Mach-O, ReadyToRun, and Native AOT symbols.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeDisassembly_Wasm_ByFunctionIndex_ReturnsInstructions()
    {
        var wasmPath = GetWasmNativePath();

        string funcAlias;
        using (var analyzer = new AssemblyAnalyzer(wasmPath))
        {
            var symbol = analyzer.NativeSymbols!.Symbols.First(s =>
                s.Aliases.Any(static alias => alias.StartsWith("func:", StringComparison.Ordinal)));
            funcAlias = symbol.Aliases.First(static alias => alias.StartsWith("func:", StringComparison.Ordinal));
        }

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_disassembly",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = wasmPath,
                ["symbolName"] = funcAlias
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.IsFalse(text.StartsWith("Error", StringComparison.Ordinal), text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("Wasm32", json.GetProperty("architecture").GetString());
        Assert.IsGreaterThan(0, json.GetProperty("instructions").GetArrayLength());
    }

    /// <summary>get_native_disassembly reports an error for a managed assembly.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeDisassembly_Managed_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_disassembly",
            new Dictionary<string, object?> { ["symbolName"] = "Foo", ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("Error", text);
    }

    /// <summary>
    /// get_assembly_info carries the native symbol provenance fields.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetAssemblyInfo_NativeAot_CarriesSymbolProvenance()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("nativeSymbolCount", text);
        Assert.Contains("nativeSymbolSource", text);
        Assert.Contains("nativeSymbolStatus", text);
    }

    private static NativeSymbol FindWasmFunctionWithNamedCall(AssemblyAnalyzer analyzer)
    {
        var info = analyzer.NativeSymbols;
        Assert.IsNotNull(info);
        foreach (var symbol in info.Symbols.Take(512))
        {
            var result = NativeDisassembler.DisassembleSymbol(analyzer, symbol);
            if (result is null)
                continue;

            if (result.Value.Instructions.Any(static instruction => instruction.TargetName is not null))
                return symbol;
        }

        throw new InvalidOperationException("No Wasm function with a named direct call was found.");
    }

    private static string GetWasmNativePath()
    {
        TestSkip.When(Samples.WasmConsoleNativeWasm is null && Samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return Samples.WasmConsoleNativeWasm ?? Samples.ReadyToRunConsoleWasmNativeWasm!;
    }
}
