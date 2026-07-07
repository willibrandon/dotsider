using Dotsider.Core.Protocol;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the MCP WebAssembly tools over real SDK-produced browser-wasm output.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class WasmToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// list_wasm_sections returns raw Wasm section payload offsets and sizes from dotnet.native.wasm.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListWasmSections_Direct_ReturnsSections()
    {
        var wasmPath = GetWasmNativePath();

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_wasm_sections",
            new Dictionary<string, object?> { ["assemblyPath"] = wasmPath },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("sectionCount").GetInt32() > 0);
        Assert.Contains(json.GetProperty("sections").EnumerateArray(), static section =>
            section.GetProperty("name").GetString() == "code");
    }

    /// <summary>
    /// list_wasm_functions returns imported and file-backed functions in Wasm function-index order.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListWasmFunctions_Direct_ReturnsFunctionInventory()
    {
        var wasmPath = GetWasmNativePath();

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_wasm_functions",
            new Dictionary<string, object?> { ["assemblyPath"] = wasmPath },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("functionCount").GetInt32() > 0);
        var functions = json.GetProperty("functions").EnumerateArray().ToArray();
        Assert.Contains(functions, static function => function.GetProperty("isImported").GetBoolean());
        Assert.Contains(functions, static function => !function.GetProperty("isImported").GetBoolean());
    }

    /// <summary>
    /// list_wasm_functions forwards session requests to a running dotsider instance unchanged.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListWasmFunctions_Session_ForwardsRequest()
    {
        await using var socket = new TestDotsiderSocket(999_997, "dotnet.native.wasm");
        socket.OnMethod("list-wasm-functions", _ => DotsiderResponse.Ok(new
        {
            FunctionCount = 1,
            Functions = new[] { new { Index = 0, Name = "func_0" } }
        }));

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_wasm_functions",
            new Dictionary<string, object?> { ["sessionId"] = socket.Pid },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(1, json.GetProperty("functionCount").GetInt32());
        Assert.Equal("func_0", json.GetProperty("functions")[0].GetProperty("name").GetString());
    }

    private string GetWasmNativePath()
    {
        Assert.SkipWhen(samples.WasmConsoleNativeWasm is null && samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return samples.WasmConsoleNativeWasm ?? samples.ReadyToRunConsoleWasmNativeWasm!;
    }
}
