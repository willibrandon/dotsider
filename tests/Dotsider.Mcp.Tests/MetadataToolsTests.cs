using System.Text.Json;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class MetadataToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    [Fact]
    public async Task GetPeHeaders_ValidAssembly_ReturnsHeaders()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_pe_headers",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    [Fact]
    public async Task GetClrHeader_ValidAssembly_ReturnsClrInfo()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_clr_header",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    [Fact]
    public async Task GetSections_ValidAssembly_ReturnsSections()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_sections",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var sections = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(sections.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetCustomAttributes_ValidAssembly_ReturnsAttributes()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_custom_attributes",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var attrs = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(attrs.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetCustomAttributes_DefaultFiltering_ExcludesCompilerGenerated()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_custom_attributes",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.DoesNotContain("CompilerGeneratedAttribute", text);
        Assert.DoesNotContain("NullableContextAttribute", text);
        Assert.DoesNotContain("NullableAttribute", text);
        Assert.DoesNotContain("DebuggerBrowsableAttribute", text);
    }

    [Fact]
    public async Task GetCustomAttributes_IncludeCompilerGenerated_ReturnsAll()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_custom_attributes",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["includeCompilerGenerated"] = true
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        // With includeCompilerGenerated=true, these should be present
        Assert.Contains("CompilerGeneratedAttribute", text);
    }

    [Fact]
    public async Task GetCustomAttributes_ToolSchema_IncludesFilterParameter()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var tools = await client.ListToolsAsync(cancellationToken: TestCancellationToken);
        var customAttrTool = tools.First(t => t.Name == "get_custom_attributes");
        var schema = customAttrTool.JsonSchema.ToString();
        Assert.Contains("includeCompilerGenerated", schema);
    }

    [Fact]
    public async Task GetResources_ValidAssembly_ReturnsResourceList()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_resources",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.HelloWorldDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Equal(JsonValueKind.Array,
            JsonSerializer.Deserialize<JsonElement>(text).ValueKind);
    }

    [Fact]
    public async Task ResolveToken_ValidToken_ReturnsResolvedName()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        // 0x02000002 is typically a TypeDef token for the first user type
        var result = await client.CallToolAsync(
            "resolve_token",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.HelloWorldDll,
                ["token"] = 0x02000002
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.TryGetProperty("resolved", out var resolved));
        Assert.False(string.IsNullOrEmpty(resolved.GetString()));
    }
}
