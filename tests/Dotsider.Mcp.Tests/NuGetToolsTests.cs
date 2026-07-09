using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the analyze_nupkg MCP tool over a published NuGet package.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[TestClass]
public class NuGetToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// analyze_nupkg surfaces packageId and packageVersion from the .nuspec manifest.
    /// </summary>
    [TestMethod]
    public async Task AnalyzeNupkg_RichLibrary_ReturnsPackageMetadata()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "analyze_nupkg",
            new Dictionary<string, object?> { ["nupkgPath"] = Samples.RichLibraryNupkg },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual("RichLibrary", json.GetProperty("packageId").GetString());
        Assert.AreEqual("2.5.1", json.GetProperty("packageVersion").GetString());
    }

    /// <summary>
    /// analyze_nupkg enumerates the DLLs bundled in the lib/ folders of the package.
    /// </summary>
    [TestMethod]
    public async Task AnalyzeNupkg_RichLibrary_ListsDlls()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "analyze_nupkg",
            new Dictionary<string, object?> { ["nupkgPath"] = Samples.RichLibraryNupkg },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.TryGetProperty("dllFiles", out var dlls));
        Assert.IsGreaterThan(0, dlls.GetArrayLength());
    }
}
