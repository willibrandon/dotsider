using System.Text.Json;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class DiffToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    [Fact]
    public async Task DiffAssemblies_V1VsV2_ReturnsDifferences()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.RichLibraryDll,
                ["rightPath"] = samples.RichLibraryV2Dll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    [Fact]
    public async Task DiffAssemblies_SameAssembly_ReturnsNoDifferences()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.HelloWorldDll,
                ["rightPath"] = samples.HelloWorldDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.DoesNotContain("Error", text);
    }
}
