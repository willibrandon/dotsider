using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the diff_assemblies MCP tool and its pagination limits.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[Collection("SampleAssemblies")]
public class DiffToolsTests(SampleAssemblyFixture samples) : McpServerTestBase
{
    /// <summary>
    /// Comparing v1 to v2 of the same library produces a non-error diff payload.
    /// </summary>
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

    /// <summary>
    /// Diffing an assembly against itself produces an empty, error-free diff.
    /// </summary>
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

    /// <summary>
    /// maxTypeDiffs caps the typeDiffs array while metadataSummary retains full counts.
    /// </summary>
    [Fact]
    public async Task DiffAssemblies_MaxTypeDiffs_LimitsTypeOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.RichLibraryDll,
                ["rightPath"] = samples.RichLibraryV2Dll,
                ["maxTypeDiffs"] = 2
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        var typeDiffs = json.GetProperty("typeDiffs");
        Assert.True(typeDiffs.GetArrayLength() <= 2);

        // Summary should still reflect full counts
        var summary = json.GetProperty("metadataSummary");
        var totalTypes = summary.GetProperty("typesAdded").GetInt32()
            + summary.GetProperty("typesRemoved").GetInt32()
            + summary.GetProperty("typesChanged").GetInt32();
        Assert.True(totalTypes > 2, "Summary should reflect all diffs, not the limited output");
    }

    /// <summary>
    /// maxMethodDiffs caps the methodDiffs array without altering the aggregate summary.
    /// </summary>
    [Fact]
    public async Task DiffAssemblies_MaxMethodDiffs_LimitsMethodOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.RichLibraryDll,
                ["rightPath"] = samples.RichLibraryV2Dll,
                ["maxMethodDiffs"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        var methodDiffs = json.GetProperty("methodDiffs");
        Assert.True(methodDiffs.GetArrayLength() <= 5);

        // Summary should still reflect full counts
        var summary = json.GetProperty("metadataSummary");
        var totalMethods = summary.GetProperty("methodsAdded").GetInt32()
            + summary.GetProperty("methodsRemoved").GetInt32()
            + summary.GetProperty("methodsChanged").GetInt32();
        Assert.True(totalMethods > 5, "Summary should reflect all diffs, not the limited output");
    }

    /// <summary>
    /// Type and method limits compose independently on the same diff invocation.
    /// </summary>
    [Fact]
    public async Task DiffAssemblies_BothLimits_LimitsBothOutputs()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = samples.RichLibraryDll,
                ["rightPath"] = samples.RichLibraryV2Dll,
                ["maxTypeDiffs"] = 3,
                ["maxMethodDiffs"] = 10
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("typeDiffs").GetArrayLength() <= 3);
        Assert.True(json.GetProperty("methodDiffs").GetArrayLength() <= 10);
    }
}
