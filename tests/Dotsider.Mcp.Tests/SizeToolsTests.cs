using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for assembly size-breakdown and largest-method MCP tools.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[TestClass]
public class SizeToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// get_size_breakdown produces an error-free hierarchical size payload for a real library.
    /// </summary>
    [TestMethod]
    public async Task GetSizeBreakdown_RichLibrary_ReturnsSizeTree()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_size_breakdown",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    /// <summary>
    /// get_largest_methods honors maxResults and returns the top-N largest IL bodies.
    /// </summary>
    [TestMethod]
    public async Task GetLargestMethods_RichLibrary_ReturnsSortedMethods()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_largest_methods",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["maxResults"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, methods.GetArrayLength());
        Assert.IsLessThanOrEqualTo(5, methods.GetArrayLength());
    }

    /// <summary>
    /// Without an explicit limit, get_largest_methods caps output at the 20-method default.
    /// </summary>
    [TestMethod]
    public async Task GetLargestMethods_DefaultCount_Returns20OrFewer()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_largest_methods",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.ComplexAppDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsLessThanOrEqualTo(20, methods.GetArrayLength());
    }

    /// <summary>
    /// get_size_breakdown on a Native AOT binary with an mstat sidecar returns the AOT tree:
    /// assembly subtrees plus category nodes, not an empty root.
    /// </summary>
    [TestMethod]
    public async Task GetSizeBreakdown_NativeAot_ReturnsAotTree()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_size_breakdown",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var tree = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, tree.GetProperty("size").GetInt64());
        Assert.Contains("category", text);
        Assert.Contains("System.Private.CoreLib", text);
    }

    /// <summary>
    /// get_largest_methods uses Native AOT mstat method sizes when IL bodies are absent.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task GetLargestMethods_NativeAot_ReturnsMstatMethods()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_largest_methods",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.NativeAotConsoleExe,
                ["maxResults"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsGreaterThan(0, methods.GetArrayLength());
        TestAssert.All(methods.EnumerateArray(), m =>
        {
            Assert.AreEqual("Mstat", m.GetProperty("source").GetString());
            Assert.IsGreaterThan(0, m.GetProperty("size").GetInt64());
        });
    }
}
