using System.Text.Json;

namespace Dotsider.Mcp.Tests;

[Collection("SampleAssemblies")]
public class NuGetToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    [Fact]
    public async Task AnalyzeNupkg_RichLibrary_ReturnsPackageMetadata()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "analyze_nupkg",
            new Dictionary<string, object?> { ["nupkgPath"] = samples.RichLibraryNupkg },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal("RichLibrary", json.GetProperty("packageId").GetString());
        Assert.Equal("2.5.1", json.GetProperty("packageVersion").GetString());
    }

    [Fact]
    public async Task AnalyzeNupkg_RichLibrary_ListsDlls()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "analyze_nupkg",
            new Dictionary<string, object?> { ["nupkgPath"] = samples.RichLibraryNupkg },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.TryGetProperty("dllFiles", out var dlls));
        Assert.True(dlls.GetArrayLength() > 0);
    }
}
