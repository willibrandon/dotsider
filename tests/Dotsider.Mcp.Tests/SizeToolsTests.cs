using System.Text.Json;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class SizeToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    [Fact]
    public async Task GetSizeBreakdown_RichLibrary_ReturnsSizeTree()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_size_breakdown",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    [Fact]
    public async Task GetLargestMethods_RichLibrary_ReturnsSortedMethods()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_largest_methods",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = samples.RichLibraryDll,
                ["maxResults"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(methods.GetArrayLength() > 0);
        Assert.True(methods.GetArrayLength() <= 5);
    }

    [Fact]
    public async Task GetLargestMethods_DefaultCount_Returns20OrFewer()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "get_largest_methods",
            new Dictionary<string, object?> { ["assemblyPath"] = samples.ComplexAppDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var methods = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(methods.GetArrayLength() <= 20);
    }
}
