using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for Native AOT-specific MCP tools.
/// </summary>
[Collection("SampleAssemblies")]
public class NativeAotToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>get_native_aot_info reports Native AOT identity and sidecar availability.</summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeAotInfo_NativeAot_ReturnsSummary()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_aot_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("nativeAot", json.GetProperty("binaryKind").GetString());
        Assert.True(json.GetProperty("readyToRunSections").GetInt32() > 0);
        Assert.True(json.GetProperty("recoveredTypes").GetInt32() > 0);
        Assert.True(json.TryGetProperty("hasMstat", out _));
    }

    /// <summary>list_native_aot_sections returns Native AOT RTR module sections.</summary>
    [Fact(Timeout = 30_000)]
    public async Task ListNativeAotSections_NativeAot_ReturnsRtrSections()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_native_aot_sections",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("sectionCount").GetInt32() > 0);
        var first = json.GetProperty("sections").EnumerateArray().First();
        Assert.True(first.TryGetProperty("sectionId", out _));
        Assert.True(first.TryGetProperty("address", out _));
    }

    /// <summary>get_native_aot_size_contributors returns normalized mstat contributors.</summary>
    [Fact(Timeout = 30_000)]
    public async Task GetNativeAotSizeContributors_NativeAot_ReturnsTopMstatRows()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_aot_size_contributors",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.NativeAotConsoleExe,
                ["section"] = "Method",
                ["topN"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("totalMatches").GetInt32() > 0);
        var rows = json.GetProperty("contributors");
        Assert.True(rows.GetArrayLength() > 0);
        Assert.All(rows.EnumerateArray(), row =>
        {
            Assert.Equal("method", row.GetProperty("section").GetString());
            Assert.True(row.GetProperty("size").GetInt64() > 0);
        });
    }

    /// <summary>explain_native_aot_size returns a DGML root chain for a real contributor.</summary>
    [Fact(Timeout = 30_000)]
    public async Task ExplainNativeAotSize_NativeAot_ReturnsRootChain()
    {
        Assert.SkipWhen(samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var contributors = await client.CallToolAsync(
            "get_native_aot_size_contributors",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.NativeAotConsoleExe,
                ["section"] = "Method",
                ["topN"] = 20
            },
            cancellationToken: TestCancellationToken);

        var contributorText = GetTextContent(contributors);
        Assert.NotNull(contributorText);
        var contributorJson = JsonSerializer.Deserialize<JsonElement>(contributorText);
        var target = contributorJson.GetProperty("contributors")
            .EnumerateArray()
            .First(e => e.GetProperty("nodeNames").GetArrayLength() > 0)
            .GetProperty("fullPath")
            .GetString();
        Assert.NotNull(target);

        var result = await client.CallToolAsync(
            "explain_native_aot_size",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.NativeAotConsoleExe,
                ["target"] = target
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("resolved", json.GetProperty("outcome").GetString());
        var chains = json.GetProperty("contributor").GetProperty("whyChains");
        Assert.True(chains.GetArrayLength() > 0);
    }
}
