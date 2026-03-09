using System.Text.Json;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class AssemblyToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    [Fact]
    public async Task GetAssemblyInfo_HelloWorld_ReturnsAssemblyMetadata()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("HelloWorld", json.GetProperty("assemblyName").GetString());
        Assert.True(json.GetProperty("hasMetadata").GetBoolean());
        Assert.True(json.GetProperty("typeCount").GetInt32() > 0);
        Assert.True(json.GetProperty("methodCount").GetInt32() > 0);
    }

    [Fact]
    public async Task GetAssemblyInfo_RichLibrary_IncludesVersion()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("RichLibrary", json.GetProperty("assemblyName").GetString());
        Assert.True(json.GetProperty("assemblyRefCount").GetInt32() > 0);
    }

    [Fact]
    public async Task GetAssemblyInfo_NoParams_ReturnsError()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_assembly_info",
            new Dictionary<string, object?>(),
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("Error", text);
    }

    [Fact]
    public async Task ListTypes_HelloWorld_ReturnsTypes()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(types.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ListTypes_WithQuery_FiltersResults()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["query"] = "UserService"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        foreach (var type in types.EnumerateArray())
        {
            Assert.Contains("UserService", type.GetProperty("fullName").GetString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ListTypes_WithMaxResults_LimitsOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["maxResults"] = 3
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(types.GetArrayLength() <= 3);
    }

    [Fact]
    public async Task ListMethods_HelloWorld_ReturnsMethods()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_methods",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(methods.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ListMethods_FilterByTypeName_ReturnsFilteredMethods()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_methods",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["typeName"] = "UserService"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(methods.GetArrayLength() > 0);
        foreach (var method in methods.EnumerateArray())
        {
            Assert.Contains("UserService", method.GetProperty("declaringType").GetString(),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task FindMembers_SearchQuery_ReturnsMatchingMembers()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "find_members",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["query"] = "User"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.TryGetProperty("types", out _) || json.TryGetProperty("methods", out _));
    }

    [Fact]
    public async Task ListTypes_EmptyLib_ReturnsMinimalTypes()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "list_types",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.EmptyLibDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var types = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(JsonValueKind.Array, types.ValueKind);
    }
}
