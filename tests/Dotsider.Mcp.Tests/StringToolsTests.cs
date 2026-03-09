using System.Text.Json;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class StringToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
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
}
