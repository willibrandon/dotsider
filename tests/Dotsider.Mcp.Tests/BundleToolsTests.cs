using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for single-file bundle MCP tools: get_bundle_info and list_bundle_entries.
/// </summary>
[Collection("SampleAssemblies")]
public class BundleToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    [Fact(Timeout = 30_000)]
    public async Task GetBundleInfo_ReturnsBundleMetadata()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_bundle_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.SelfContainedConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("isBundle").GetBoolean());
        Assert.True(json.GetProperty("fileCount").GetInt32() > 0);
    }

    [Fact(Timeout = 30_000)]
    public async Task GetBundleInfo_NonBundle_ReturnsFalse()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_bundle_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.False(json.GetProperty("isBundle").GetBoolean());
    }

    [Fact(Timeout = 30_000)]
    public async Task ListBundleEntries_ReturnsEntries()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_bundle_entries",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.SelfContainedConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var entries = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(JsonValueKind.Array, entries.ValueKind);
        Assert.True(entries.GetArrayLength() > 0);
    }

    [Fact(Timeout = 30_000)]
    public async Task ListTools_IncludesBundleTools()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        var tools = await client.ListToolsAsync(cancellationToken: TestCancellationToken);
        var names = tools.Select(t => t.Name).ToList();
        Assert.Contains("get_bundle_info", names);
        Assert.Contains("list_bundle_entries", names);
    }
}
