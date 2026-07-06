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

    // --- diff_size ---

    private static readonly string[] s_telemetryZeroGrowthBudget = ["ns=NativeAotConsole.Telemetry:growth=0"];
    private static readonly string[] s_generousGrowthBudget = ["total:growth=1000%"];
    private static readonly string[] s_bareGrowthBudget = ["growth=1%"];

    private (string V1, string V2) RequireMstats()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        Assert.SkipWhen(samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        return (samples.NativeAotConsoleMstat!, samples.NativeAotConsoleV2Mstat!);
    }

    /// <summary>
    /// diff_size over the real V1/V2 mstat pair returns the summary, aggregates, and top
    /// contributors — and no tree unless asked.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task DiffSize_V1V2Mstats_ReturnsSummaryAndContributors()
    {
        var (v1, v2) = RequireMstats();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_size",
            new Dictionary<string, object?>
            {
                ["leftPath"] = v1,
                ["rightPath"] = v2,
                ["topN"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.NotEqual(0, json.GetProperty("summary").GetProperty("delta").GetInt64());
        Assert.True(json.GetProperty("assemblyDeltas").GetArrayLength() > 0);
        Assert.True(json.GetProperty("namespaceDeltas").GetArrayLength() > 0);
        Assert.True(json.GetProperty("contributors").GetArrayLength() <= 5);
        Assert.False(json.TryGetProperty("root", out var root) && root.ValueKind != JsonValueKind.Null);
    }

    /// <summary>diff_size of a report against itself returns a zero delta and no contributors.</summary>
    [Fact(Timeout = 30_000)]
    public async Task DiffSize_SelfDiff_ZeroDelta()
    {
        var (v1, _) = RequireMstats();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_size",
            new Dictionary<string, object?>
            {
                ["leftPath"] = v1,
                ["rightPath"] = v1
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.Equal(0, json.GetProperty("summary").GetProperty("delta").GetInt64());
        Assert.Equal(0, json.GetProperty("contributors").GetArrayLength());
    }

    /// <summary>
    /// includeTree with a tight node cap emits a pruned tree and says so through the
    /// truncation metadata — deterministic, never a silent sample.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task DiffSize_IncludeTreeWithCap_SetsTruncationMetadata()
    {
        var (v1, v2) = RequireMstats();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_size",
            new Dictionary<string, object?>
            {
                ["leftPath"] = v1,
                ["rightPath"] = v2,
                ["includeTree"] = true,
                ["maxNodes"] = 10
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.NotEqual(JsonValueKind.Null, json.GetProperty("root").ValueKind);
        Assert.True(json.GetProperty("treeTruncated").GetBoolean());
        Assert.True(json.GetProperty("treeTotalNodes").GetInt32() > 10);
        Assert.True(json.GetProperty("treeIncludedNodes").GetInt32() <= 10);
    }

    /// <summary>diff_size rejects an input that is not mstat-backed with a message naming the fix.</summary>
    [Fact(Timeout = 30_000)]
    public async Task DiffSize_NonMstatInput_ReturnsError()
    {
        var (v1, _) = RequireMstats();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_size",
            new Dictionary<string, object?>
            {
                ["leftPath"] = v1,
                ["rightPath"] = samples.RichLibraryDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("not mstat-backed", text);
    }

    // --- check_size_budgets ---

    /// <summary>
    /// A zero-growth budget on the namespace added in V2 breaches, and the report carries the
    /// violation and its scoped contributors.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task CheckSizeBudgets_Breach_ReportsFailed()
    {
        var (v1, v2) = RequireMstats();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "check_size_budgets",
            new Dictionary<string, object?>
            {
                ["targetPath"] = v2,
                ["baselinePath"] = v1,
                ["budgets"] = s_telemetryZeroGrowthBudget
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.False(json.GetProperty("passed").GetBoolean());
        var evaluation = json.GetProperty("evaluations")[0];
        Assert.True(evaluation.GetProperty("violations").GetArrayLength() > 0);
        Assert.True(evaluation.GetProperty("topContributors").GetArrayLength() > 0);
    }

    /// <summary>A generous budget passes.</summary>
    [Fact(Timeout = 30_000)]
    public async Task CheckSizeBudgets_Pass_ReportsPassed()
    {
        var (v1, v2) = RequireMstats();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "check_size_budgets",
            new Dictionary<string, object?>
            {
                ["targetPath"] = v2,
                ["baselinePath"] = v1,
                ["budgets"] = s_generousGrowthBudget
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("passed").GetBoolean());
    }

    /// <summary>
    /// budgetsJson carries the object form — named budgets with warning severity — at full
    /// parity with the CLI's budget file.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task CheckSizeBudgets_BudgetsJson_ObjectFormHonored()
    {
        var (v1, v2) = RequireMstats();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "check_size_budgets",
            new Dictionary<string, object?>
            {
                ["targetPath"] = v2,
                ["baselinePath"] = v1,
                ["budgetsJson"] = """
                    { "budgets": [ {
                        "name": "telemetry-watch",
                        "scope": "ns=NativeAotConsole.Telemetry",
                        "growth": "0",
                        "severity": "warning",
                        "topN": 3
                    } ] }
                    """
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.True(json.GetProperty("passed").GetBoolean(), "warning severity must not fail the check");
        Assert.True(json.GetProperty("hasWarnings").GetBoolean());
        var evaluation = json.GetProperty("evaluations")[0];
        Assert.False(evaluation.GetProperty("passed").GetBoolean());
        Assert.Equal("telemetry-watch",
            evaluation.GetProperty("budget").GetProperty("name").GetString());
        Assert.True(evaluation.GetProperty("topContributors").GetArrayLength() <= 3);
    }

    /// <summary>budgetFilePath loads the same document schema from disk.</summary>
    [Fact(Timeout = 30_000)]
    public async Task CheckSizeBudgets_BudgetFilePath_Honored()
    {
        var (v1, v2) = RequireMstats();
        var budgetFile = Path.Combine(Path.GetTempPath(), $"dotsider-mcp-budgets-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(
                budgetFile, """{ "budgets": ["total:growth=1000%"] }""", TestCancellationToken);
            await StartServerAsync();
            await using var client = await CreateClientAsync();

            var result = await client.CallToolAsync(
                "check_size_budgets",
                new Dictionary<string, object?>
                {
                    ["targetPath"] = v2,
                    ["baselinePath"] = v1,
                    ["budgetFilePath"] = budgetFile
                },
                cancellationToken: TestCancellationToken);

            var text = GetTextContent(result);
            Assert.NotNull(text);
            var json = JsonSerializer.Deserialize<JsonElement>(text);
            Assert.True(json.GetProperty("passed").GetBoolean());
        }
        finally
        {
            File.Delete(budgetFile);
        }
    }

    /// <summary>Every budget source absent is an error, not an empty pass.</summary>
    [Fact(Timeout = 30_000)]
    public async Task CheckSizeBudgets_NoBudgetSource_ReturnsError()
    {
        var (v1, v2) = RequireMstats();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "check_size_budgets",
            new Dictionary<string, object?>
            {
                ["targetPath"] = v2,
                ["baselinePath"] = v1
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("budget source is required", text);
    }

    /// <summary>A growth budget without a baseline is an error naming the missing parameter.</summary>
    [Fact(Timeout = 30_000)]
    public async Task CheckSizeBudgets_GrowthWithoutBaseline_ReturnsError()
    {
        var (_, v2) = RequireMstats();
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "check_size_budgets",
            new Dictionary<string, object?>
            {
                ["targetPath"] = v2,
                ["budgets"] = s_bareGrowthBudget
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.NotNull(text);
        Assert.Contains("baselinePath", text);
    }
}
