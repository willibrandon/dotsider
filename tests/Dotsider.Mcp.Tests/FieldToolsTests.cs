using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for field definition listing MCP tools.
/// </summary>
[Collection("SampleAssemblies")]
public class FieldToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// list_fields returns a non-empty JSON array of field entries for a real library.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListFields_ReturnsFields()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_fields",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var fields = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(JsonValueKind.Array, fields.ValueKind);
        Assert.True(fields.GetArrayLength() > 0);
    }

    /// <summary>
    /// A query narrows list_fields to fields whose names match the substring.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListFields_WithQuery_Filters()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_fields",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["query"] = "_counter"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var fields = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(JsonValueKind.Array, fields.ValueKind);
        // All results should contain "_counter" in their name
        foreach (var field in fields.EnumerateArray())
            Assert.Contains("_counter", field.GetProperty("name").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// typeName restricts list_fields to members of the specified declaring type.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListFields_WithTypeName_Filters()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync("list_fields",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["typeName"] = "IlNavigationFixture"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var fields = JsonSerializer.Deserialize<JsonElement>(text);
        foreach (var field in fields.EnumerateArray())
            Assert.Contains("IlNavigationFixture", field.GetProperty("declaringType").GetString()!);
    }

    /// <summary>
    /// Tool registry advertises list_fields alongside the other assembly tools.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ListTools_IncludesFieldTools()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();
        var tools = await client.ListToolsAsync(cancellationToken: TestCancellationToken);
        Assert.Contains(tools, t => t.Name == "list_fields");
    }
}
