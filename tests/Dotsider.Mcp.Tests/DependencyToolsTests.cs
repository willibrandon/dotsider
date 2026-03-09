using System.Text.Json;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class DependencyToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
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
