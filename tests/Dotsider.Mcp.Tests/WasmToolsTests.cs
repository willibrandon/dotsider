using Dotsider.Core.Protocol;
using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the MCP WebAssembly tools over real SDK-produced browser-wasm output.
/// </summary>
[TestClass]
public sealed class WasmToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// list_wasm_sections returns raw Wasm section payload offsets and sizes from dotnet.native.wasm.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, json.GetProperty("sectionCount").GetInt32());
        Assert.Contains(static section =>
            section.GetProperty("name").GetString() == "code", json.GetProperty("sections").EnumerateArray());
    }

    /// <summary>
    /// list_wasm_functions returns imported and file-backed functions in Wasm function-index order.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, json.GetProperty("functionCount").GetInt32());
        var functions = json.GetProperty("functions").EnumerateArray().ToArray();
        Assert.Contains(static function => function.GetProperty("isImported").GetBoolean(), functions);
        Assert.Contains(static function => !function.GetProperty("isImported").GetBoolean(), functions);
    }

    /// <summary>
    /// list_wasm_functions forwards session requests to a running dotsider instance unchanged.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual(1, json.GetProperty("functionCount").GetInt32());
        Assert.AreEqual("func_0", json.GetProperty("functions")[0].GetProperty("name").GetString());
    }

    private static string GetWasmNativePath()
    {
        TestSkip.When(Samples.WasmConsoleNativeWasm is null && Samples.ReadyToRunConsoleWasmNativeWasm is null,
            "browser-wasm publish did not run on this leg.");

        return Samples.WasmConsoleNativeWasm ?? Samples.ReadyToRunConsoleWasmNativeWasm!;
    }
}
