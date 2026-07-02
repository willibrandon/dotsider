using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the extract_strings MCP tool and its filter parameters.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[Collection("SampleAssemblies")]
public class StringToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// extract_strings returns user, metadata, and raw string categories in a single payload.
    /// </summary>
    [Fact]
    public async Task ExtractStrings_RichLibrary_ReturnsStringCategories()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.TryGetProperty("userStrings", out _));
        Assert.True(json.TryGetProperty("metadataStrings", out _));
        Assert.True(json.TryGetProperty("rawStrings", out _));
    }

    /// <summary>
    /// A query filter restricts extract_strings output to substring-matching entries.
    /// </summary>
    [Fact]
    public async Task ExtractStrings_WithQuery_FiltersResults()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["query"] = "Hello"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.TryGetProperty("userStrings", out _));
    }

    /// <summary>
    /// maxResults caps each string category to avoid flooding the MCP client.
    /// </summary>
    [Fact]
    public async Task ExtractStrings_WithMaxResults_LimitsOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["maxResults"] = 2
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        if (json.TryGetProperty("userStrings", out var user))
        {
            Assert.True(user.GetArrayLength() <= 2);
        }
    }

    /// <summary>
    /// extract_strings on a Native AOT executable returns raw ASCII and raw UTF-16
    /// strings even though the metadata heaps are absent.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ExtractStrings_NativeAot_ReturnsRawAndUtf16()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.NativeAotConsoleExe,
                ["minLength"] = 8,
                ["maxResults"] = 50
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Empty(json.GetProperty("userStrings").EnumerateArray());
        Assert.True(json.GetProperty("rawStrings").GetArrayLength() > 0);
        Assert.True(json.GetProperty("rawUtf16Strings").GetArrayLength() > 0);
    }

    /// <summary>
    /// extract_strings always includes the rawUtf16Strings category for managed
    /// assemblies too.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task ExtractStrings_Managed_HasRawUtf16Field()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.TryGetProperty("rawUtf16Strings", out _));
    }
}
