using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for Native AOT-specific MCP tools.
/// </summary>
[TestClass]
public class NativeAotToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>get_native_aot_info reports Native AOT identity and sidecar availability.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeAotInfo_NativeAot_ReturnsSummary()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_aot_info",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("nativeAot", json.GetProperty("binaryKind").GetString());
        Assert.IsGreaterThan(0, json.GetProperty("readyToRunSections").GetInt32());
        Assert.IsGreaterThan(0, json.GetProperty("recoveredTypes").GetInt32());
        Assert.IsTrue(json.TryGetProperty("hasMstat", out _));
    }

    /// <summary>list_native_aot_sections returns Native AOT RTR module sections.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListNativeAotSections_NativeAot_ReturnsRtrSections()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_native_aot_sections",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, json.GetProperty("sectionCount").GetInt32());
        var first = json.GetProperty("sections").EnumerateArray().First();
        Assert.IsTrue(first.TryGetProperty("sectionId", out _));
        Assert.IsTrue(first.TryGetProperty("address", out _));
    }

    /// <summary>get_native_aot_size_contributors returns normalized mstat contributors.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetNativeAotSizeContributors_NativeAot_ReturnsTopMstatRows()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_native_aot_size_contributors",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.NativeAotConsoleExe,
                ["section"] = "Method",
                ["topN"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, json.GetProperty("totalMatches").GetInt32());
        var rows = json.GetProperty("contributors");
        Assert.IsGreaterThan(0, rows.GetArrayLength());
        TestAssert.All(rows.EnumerateArray(), row =>
        {
            Assert.AreEqual("method", row.GetProperty("section").GetString());
            Assert.IsGreaterThan(0, row.GetProperty("size").GetInt64());
        });
    }

    /// <summary>explain_native_aot_size returns a DGML root chain for a real contributor.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ExplainNativeAotSize_NativeAot_ReturnsRootChain()
    {
        TestSkip.When(Samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var contributors = await client.CallToolAsync(
            "get_native_aot_size_contributors",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.NativeAotConsoleExe,
                ["section"] = "Method",
                ["topN"] = 20
            },
            cancellationToken: TestCancellationToken);

        var contributorText = GetTextContent(contributors);
        Assert.IsNotNull(contributorText);
        var contributorJson = JsonSerializer.Deserialize<JsonElement>(contributorText);
        var target = contributorJson.GetProperty("contributors")
            .EnumerateArray()
            .First(e => e.GetProperty("nodeNames").GetArrayLength() > 0)
            .GetProperty("fullPath")
            .GetString();
        Assert.IsNotNull(target);

        var result = await client.CallToolAsync(
            "explain_native_aot_size",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.NativeAotConsoleExe,
                ["target"] = target
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("resolved", json.GetProperty("outcome").GetString());
        var chains = json.GetProperty("contributor").GetProperty("whyChains");
        Assert.IsGreaterThan(0, chains.GetArrayLength());
    }
}
