namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the native-symbol MCP tool: <c>get_native_symbols</c> against the real Native AOT
/// fixture and the managed-assembly error path.
/// </summary>
[Collection("SampleAssemblies")]
public class SymbolToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// get_native_symbols returns the symbol list with its provenance for a Native AOT binary.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeSymbols_NativeAot_ReturnsSymbolsWithProvenance()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_symbols",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.False(text.StartsWith("Error", StringComparison.Ordinal),
            "the tool reported an error instead of symbols");
        Assert.Contains("\"source\"", text);
        Assert.Contains("\"status\"", text);
        Assert.Contains("\"symbols\"", text);
    }

    /// <summary>
    /// get_native_symbols reports an error for a managed assembly instead of an empty list.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeSymbols_Managed_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_symbols",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("Error", text);
        Assert.Contains("managed", text);
    }

    /// <summary>get_native_disassembly decodes a function by address into structured instructions.</summary>
    [Fact(Timeout = 60_000)]
    public async Task GetNativeDisassembly_NativeAot_ByAddress_ReturnsInstructions()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        ulong va;
        using (var analyzer = new Dotsider.Core.Analysis.AssemblyAnalyzer(samples.NativeAotConsoleExe!))
        {
            var fn = analyzer.NativeSymbols?.Symbols.FirstOrDefault(s =>
                s.Kind == Dotsider.Core.Analysis.Models.NativeSymbolKind.Function
                && s.ManagedName is not null && s.FileOffset is not null && s.Size > 0);
            Assert.NotNull(fn);
            va = fn!.VirtualAddress;
        }

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_disassembly",
            new Dictionary<string, object?> { ["address"] = $"0x{va:x}", ["assemblyPath"] = samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.False(text.StartsWith("Error", StringComparison.Ordinal), "the tool reported an error");
        Assert.Contains("\"mnemonic\"", text);
    }

    /// <summary>get_native_disassembly reports an error for a managed assembly.</summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeDisassembly_Managed_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_disassembly",
            new Dictionary<string, object?> { ["symbolName"] = "Foo", ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("Error", text);
    }

    /// <summary>
    /// get_assembly_info carries the native symbol provenance fields.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task GetAssemblyInfo_NativeAot_CarriesSymbolProvenance()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("nativeSymbolCount", text);
        Assert.Contains("nativeSymbolSource", text);
        Assert.Contains("nativeSymbolStatus", text);
    }
}
