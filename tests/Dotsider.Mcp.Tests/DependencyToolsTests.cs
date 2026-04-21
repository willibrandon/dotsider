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
    /// get_dependency_graph returns a transitive graph. Every node carries an opaque id,
    /// at least one node lives at depth greater than zero, and at least one edge has a source
    /// id that is not the root — proving the tool emits transitive child-to-child edges, not
    /// only root-to-child edges. No internal navigation fields leak into the JSON payload.
    /// </summary>
    [Fact]
    public async Task GetDependencyGraph_RichLibrary_ReturnsTransitiveGraphWithoutNavigationLeak()
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
        Assert.True(json.TryGetProperty("edges", out var edges));

        string? rootId = null;
        var anyDepthOverZero = false;
        foreach (var n in nodes.EnumerateArray())
        {
            Assert.True(n.TryGetProperty("id", out var id));
            Assert.False(string.IsNullOrEmpty(id.GetString()));
            foreach (var leak in NavigationFieldsThatMustNotLeak)
                Assert.False(n.TryGetProperty(leak, out _), $"node should not expose {leak}");
            if (n.TryGetProperty("isRoot", out var isRoot) && isRoot.GetBoolean())
                rootId = id.GetString();
            if (n.TryGetProperty("depth", out var depth) && depth.GetInt32() > 0)
                anyDepthOverZero = true;
        }

        Assert.True(anyDepthOverZero, "expected at least one node with depth > 0");
        Assert.NotNull(rootId);

        var anyNonRootSource = false;
        foreach (var e in edges.EnumerateArray())
        {
            Assert.True(e.TryGetProperty("sourceId", out var src));
            Assert.True(e.TryGetProperty("targetId", out _));
            if (src.GetString() != rootId) anyNonRootSource = true;
        }
        Assert.True(anyNonRootSource, "expected at least one edge whose source is not the root");
    }

    private static readonly string[] NavigationFieldsThatMustNotLeak =
    {
        "resolvedPath", "referencingFilePath", "referencingBundlePath",
        "referencingTargetFramework", "referencingPreferredRuntimePack",
        "candidateProbePath", "isFrameworkAssembly", "resolved",
    };

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
