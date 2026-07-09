using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for single-file bundle MCP tools: get_bundle_info and list_bundle_entries.
/// </summary>
[TestClass]
public class BundleToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// get_bundle_info on a self-contained apphost surfaces bundle flag and file count.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetBundleInfo_ReturnsBundleMetadata()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_bundle_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.SelfContainedConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.GetProperty("isBundle").GetBoolean());
        Assert.IsGreaterThan(0, json.GetProperty("fileCount").GetInt32());
    }

    /// <summary>
    /// Non-bundle input returns false without false-positive bundle detection.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetBundleInfo_NonBundle_ReturnsFalse()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("get_bundle_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsFalse(json.GetProperty("isBundle").GetBoolean());
    }

    /// <summary>
    /// list_bundle_entries enumerates the files packed inside a single-file apphost.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListBundleEntries_ReturnsEntries()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_bundle_entries",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.SelfContainedConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var entries = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual(JsonValueKind.Array, entries.ValueKind);
        Assert.IsGreaterThan(0, entries.GetArrayLength());
    }

    /// <summary>
    /// Tool registry exposes both bundle tools by their expected identifiers.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
