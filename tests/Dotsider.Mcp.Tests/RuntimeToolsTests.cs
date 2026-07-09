using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for runtime discovery and assembly resolution MCP tools.
/// </summary>
[TestClass]
public class RuntimeToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// find_framework_assembly resolves System.Runtime to a path inside the shared framework pack.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task FindFrameworkAssembly_SystemRuntime_ReturnsPathAndPack()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("find_framework_assembly",
            new Dictionary<string, object?> { ["assemblyName"] = "System.Runtime" },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsNotEmpty(json.GetProperty("path").GetString()!);
        Assert.AreEqual("Microsoft.NETCore.App", json.GetProperty("runtimePack").GetString());
    }

    /// <summary>
    /// Unknown framework assembly names yield a serialized null rather than an error.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task FindFrameworkAssembly_Nonexistent_ReturnsNull()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("find_framework_assembly",
            new Dictionary<string, object?> { ["assemblyName"] = "DoesNotExist.Fake" },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.AreEqual("null", text);
    }

    /// <summary>
    /// resolve_assembly routes a shared framework dependency to a file-kind resolution.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ResolveAssembly_Direct_SharedFramework()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("resolve_assembly",
            new Dictionary<string, object?>
            {
                ["assemblyName"] = "System.Runtime",
                ["assemblyPath"] = Samples.RichLibraryDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("file", json.GetProperty("kind").GetString());
    }

    /// <summary>
    /// Tool registry advertises the two runtime discovery tools by canonical name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
