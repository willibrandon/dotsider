using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for field definition listing MCP tools.
/// </summary>
[TestClass]
public class FieldToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// list_fields returns a non-empty JSON array of field entries for a real library.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListFields_ReturnsFields()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_fields",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var fields = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual(JsonValueKind.Array, fields.ValueKind);
        Assert.IsGreaterThan(0, fields.GetArrayLength());
    }

    /// <summary>
    /// A query narrows list_fields to fields whose names match the substring.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListFields_WithQuery_Filters()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_fields",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["query"] = "_counter"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var fields = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual(JsonValueKind.Array, fields.ValueKind);
        // All results should contain "_counter" in their name
        foreach (var field in fields.EnumerateArray())
            Assert.Contains("_counter", field.GetProperty("name").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// typeName restricts list_fields to members of the specified declaring type.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListFields_WithTypeName_Filters()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_fields",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["typeName"] = "IlNavigationFixture"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var fields = JsonSerializer.Deserialize<JsonElement>(text);
        foreach (var field in fields.EnumerateArray())
            Assert.Contains("IlNavigationFixture", field.GetProperty("declaringType").GetString()!);
    }

    /// <summary>
    /// Tool registry advertises list_fields alongside the other assembly tools.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ListTools_IncludesFieldTools()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        var tools = await client.ListToolsAsync(cancellationToken: TestCancellationToken);
        Assert.Contains(t => t.Name == "list_fields", tools);
    }
}
