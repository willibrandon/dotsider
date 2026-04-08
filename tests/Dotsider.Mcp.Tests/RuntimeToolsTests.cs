using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for runtime discovery and assembly resolution MCP tools.
/// </summary>
[Collection("SampleAssemblies")]
public class RuntimeToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    [Fact(Timeout = 30_000)]
    public async Task FindFrameworkAssembly_SystemRuntime_ReturnsPathAndPack()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("find_framework_assembly",
            new Dictionary<string, object?> { ["assemblyName"] = "System.Runtime" },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.NotEmpty(json.GetProperty("path").GetString()!);
        Assert.Equal("Microsoft.NETCore.App", json.GetProperty("runtimePack").GetString());
    }

    [Fact(Timeout = 30_000)]
    public async Task FindFrameworkAssembly_Nonexistent_ReturnsNull()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("find_framework_assembly",
            new Dictionary<string, object?> { ["assemblyName"] = "DoesNotExist.Fake" },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Equal("null", text);
    }

    [Fact(Timeout = 30_000)]
    public async Task ResolveAssembly_Direct_SharedFramework()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("resolve_assembly",
            new Dictionary<string, object?>
            {
                ["assemblyName"] = "System.Runtime",
                ["assemblyPath"] = samples.RichLibraryDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("file", json.GetProperty("kind").GetString());
    }

    [Fact(Timeout = 30_000)]
    public async Task ListTools_IncludesRuntimeTools()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        var tools = await client.ListToolsAsync(cancellationToken: TestCancellationToken);
        var names = tools.Select(t => t.Name).ToList();
        Assert.Contains("find_framework_assembly", names);
        Assert.Contains("resolve_assembly", names);
    }
}
