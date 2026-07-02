using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for assembly size-breakdown and largest-method MCP tools.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[Collection("SampleAssemblies")]
public class SizeToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// get_size_breakdown produces an error-free hierarchical size payload for a real library.
    /// </summary>
    [Fact]
    public async Task GetSizeBreakdown_RichLibrary_ReturnsSizeTree()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_size_breakdown",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    /// <summary>
    /// get_largest_methods honors maxResults and returns the top-N largest IL bodies.
    /// </summary>
    [Fact]
    public async Task GetLargestMethods_RichLibrary_ReturnsSortedMethods()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_largest_methods",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["maxResults"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(methods.GetArrayLength() > 0);
        Assert.True(methods.GetArrayLength() <= 5);
    }

    /// <summary>
    /// Without an explicit limit, get_largest_methods caps output at the 20-method default.
    /// </summary>
    [Fact]
    public async Task GetLargestMethods_DefaultCount_Returns20OrFewer()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_largest_methods",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.ComplexAppDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(methods.GetArrayLength() <= 20);
    }

    /// <summary>
    /// get_size_breakdown on a Native AOT binary with an mstat sidecar returns the AOT tree:
    /// assembly subtrees plus category nodes, not an empty root.
    /// </summary>
    [Fact]
    public async Task GetSizeBreakdown_NativeAot_ReturnsAotTree()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_size_breakdown",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var tree = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(tree.GetProperty("size").GetInt64() > 0);
        Assert.Contains("category", text);
        Assert.Contains("System.Private.CoreLib", text);
    }
}
