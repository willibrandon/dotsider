using System.Text.Json;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class DiffToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    [Fact]
    public async Task DiffAssemblies_V1VsV2_ReturnsDifferences()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.RichLibraryDll,
                ["rightPath"] = samples.RichLibraryV2Dll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    [Fact]
    public async Task DiffAssemblies_SameAssembly_ReturnsNoDifferences()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.HelloWorldDll,
                ["rightPath"] = samples.HelloWorldDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    [Fact]
    public async Task DiffAssemblies_MaxTypeDiffs_LimitsTypeOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.RichLibraryDll,
                ["rightPath"] = samples.RichLibraryV2Dll,
                ["maxTypeDiffs"] = 2
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        var typeDiffs = json.GetProperty("typeDiffs");
        Assert.True(typeDiffs.GetArrayLength() <= 2);

        // Summary should still reflect full counts
        var summary = json.GetProperty("metadataSummary");
        var totalTypes = summary.GetProperty("typesAdded").GetInt32()
            + summary.GetProperty("typesRemoved").GetInt32()
            + summary.GetProperty("typesChanged").GetInt32();
        Assert.True(totalTypes > 2, "Summary should reflect all diffs, not the limited output");
    }

    [Fact]
    public async Task DiffAssemblies_MaxMethodDiffs_LimitsMethodOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.RichLibraryDll,
                ["rightPath"] = samples.RichLibraryV2Dll,
                ["maxMethodDiffs"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        var methodDiffs = json.GetProperty("methodDiffs");
        Assert.True(methodDiffs.GetArrayLength() <= 5);

        // Summary should still reflect full counts
        var summary = json.GetProperty("metadataSummary");
        var totalMethods = summary.GetProperty("methodsAdded").GetInt32()
            + summary.GetProperty("methodsRemoved").GetInt32()
            + summary.GetProperty("methodsChanged").GetInt32();
        Assert.True(totalMethods > 5, "Summary should reflect all diffs, not the limited output");
    }

    [Fact]
    public async Task DiffAssemblies_BothLimits_LimitsBothOutputs()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.RichLibraryDll,
                ["rightPath"] = samples.RichLibraryV2Dll,
                ["maxTypeDiffs"] = 3,
                ["maxMethodDiffs"] = 10
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("typeDiffs").GetArrayLength() <= 3);
        Assert.True(json.GetProperty("methodDiffs").GetArrayLength() <= 10);
    }
}
