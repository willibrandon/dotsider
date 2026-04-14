using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the PE/CLR metadata inspection MCP tool suite.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[Collection("SampleAssemblies")]
public class MetadataToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// get_pe_headers returns parsed PE header info without errors for a valid assembly.
    /// </summary>
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

    /// <summary>
    /// get_clr_header returns CLR directory info without errors for a managed assembly.
    /// </summary>
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

    /// <summary>
    /// get_sections enumerates the PE section table as a non-empty JSON array.
    /// </summary>
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

    /// <summary>
    /// get_custom_attributes returns at least one attribute for a real library.
    /// </summary>
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

    /// <summary>
    /// By default, compiler-generated attributes like Nullable/CompilerGenerated are filtered out.
    /// </summary>
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

    /// <summary>
    /// Opting in via includeCompilerGenerated re-exposes the noisy compiler attributes.
    /// </summary>
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

    /// <summary>
    /// The advertised tool schema surfaces the includeCompilerGenerated parameter to clients.
    /// </summary>
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

    /// <summary>
    /// get_resources always returns a JSON array, even for assemblies with no embedded resources.
    /// </summary>
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

    /// <summary>
    /// resolve_token turns a raw metadata token into a human-readable member name.
    /// </summary>
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
