using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for MCP dependency and reference inspection tools.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[Collection("SampleAssemblies")]
public class DependencyToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// get_assembly_refs returns the AssemblyRef table entries for a real library.
    /// </summary>
    [Fact]
    public async Task GetAssemblyRefs_RichLibrary_ReturnsDependencies()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_refs",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var refs = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(refs.GetArrayLength() > 0);
    }

    /// <summary>
    /// get_dependency_graph returns a graph with nodes and edges for reference visualization.
    /// </summary>
    [Fact]
    public async Task GetDependencyGraph_RichLibrary_ReturnsNodesAndEdges()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_dependency_graph",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.TryGetProperty("nodes", out var nodes));
        Assert.True(nodes.GetArrayLength() > 0);
        Assert.True(json.TryGetProperty("edges", out _));
    }

    /// <summary>
    /// get_type_refs surfaces externally-referenced types imported by the assembly.
    /// </summary>
    [Fact]
    public async Task GetTypeRefs_RichLibrary_ReturnsImportedTypes()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_type_refs",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var refs = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(refs.GetArrayLength() > 0);
    }

    /// <summary>
    /// Even a nearly empty assembly still references System.Runtime at minimum.
    /// </summary>
    [Fact]
    public async Task GetAssemblyRefs_EmptyLib_ReturnsAtLeastSystemRuntime()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_refs",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.EmptyLibDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var refs = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(refs.GetArrayLength() >= 1);
    }
}
