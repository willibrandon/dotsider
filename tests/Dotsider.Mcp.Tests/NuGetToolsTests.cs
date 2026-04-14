using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the analyze_nupkg MCP tool over a published NuGet package.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[Collection("SampleAssemblies")]
public class NuGetToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// analyze_nupkg surfaces packageId and packageVersion from the .nuspec manifest.
    /// </summary>
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

    /// <summary>
    /// analyze_nupkg enumerates the DLLs bundled in the lib/ folders of the package.
    /// </summary>
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
