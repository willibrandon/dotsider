using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the extract_strings MCP tool and its filter parameters.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[TestClass]
public class StringToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// extract_strings returns user, metadata, and raw string categories in a single payload.
    /// </summary>
    [TestMethod]
    public async Task ExtractStrings_RichLibrary_ReturnsStringCategories()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.TryGetProperty("userStrings", out _));
        Assert.IsTrue(json.TryGetProperty("metadataStrings", out _));
        Assert.IsTrue(json.TryGetProperty("rawStrings", out _));
    }

    /// <summary>
    /// A query filter restricts extract_strings output to substring-matching entries.
    /// </summary>
    [TestMethod]
    public async Task ExtractStrings_WithQuery_FiltersResults()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["query"] = "Hello"
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.TryGetProperty("userStrings", out _));
    }

    /// <summary>
    /// maxResults caps each string category to avoid flooding the MCP client.
    /// </summary>
    [TestMethod]
    public async Task ExtractStrings_WithMaxResults_LimitsOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.RichLibraryDll,
                ["maxResults"] = 2
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        if (json.TryGetProperty("userStrings", out var user))
        {
            Assert.IsLessThanOrEqualTo(2, user.GetArrayLength());
        }
    }

    /// <summary>
    /// extract_strings on a Native AOT executable returns raw ASCII and raw UTF-16
    /// strings even though the metadata heaps are absent.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ExtractStrings_NativeAot_ReturnsRawAndUtf16()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?>
            {
                ["assemblyPath"] = Samples.NativeAotConsoleExe,
                ["minLength"] = 8,
                ["maxResults"] = 50
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsEmpty(json.GetProperty("userStrings").EnumerateArray());
        Assert.IsGreaterThan(0, json.GetProperty("rawStrings").GetArrayLength());
        Assert.IsGreaterThan(0, json.GetProperty("rawUtf16Strings").GetArrayLength());
    }

    /// <summary>
    /// extract_strings always includes the rawUtf16Strings category for managed
    /// assemblies too.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ExtractStrings_Managed_HasRawUtf16Field()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.RichLibraryDll },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.TryGetProperty("rawUtf16Strings", out _));
    }

    /// <summary>
    /// extract_strings surfaces frozen string literals from a Native AOT binary on every
    /// platform — from the file-backed region on Windows and macOS, and from the rehydrated
    /// dehydrated data on Linux.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ExtractStrings_NativeAot_IncludesFrozenStrings()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "extract_strings",
            new Dictionary<string, object?> { ["assemblyPath"] = Samples.NativeAotConsoleExe },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        var frozen = json.GetProperty("frozenStrings");
        Assert.AreEqual(JsonValueKind.Array, frozen.ValueKind);
        Assert.Contains(e => e.GetProperty("value").GetString()!.Contains("Hello from NativeAOT!"), frozen.EnumerateArray());
    }
}
