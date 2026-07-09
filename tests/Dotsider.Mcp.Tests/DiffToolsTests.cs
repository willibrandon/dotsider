using System.Text.Json;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Tests for the diff_assemblies MCP tool and its pagination limits.
/// </summary>
/// <summary>
/// Creates the tests using the shared sample assembly fixture.
/// </summary>
[TestClass]
public class DiffToolsTests : McpServerTestBase
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Comparing v1 to v2 of the same library produces a non-error diff payload.
    /// </summary>
    [TestMethod]
    public async Task DiffAssemblies_V1VsV2_ReturnsDifferences()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = Samples.RichLibraryDll,
                ["rightPath"] = Samples.RichLibraryV2Dll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    /// <summary>
    /// Diffing an assembly against itself produces an empty, error-free diff.
    /// </summary>
    [TestMethod]
    public async Task DiffAssemblies_SameAssembly_ReturnsNoDifferences()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = Samples.HelloWorldDll,
                ["rightPath"] = Samples.HelloWorldDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.DoesNotContain("Error", text);
    }

    /// <summary>
    /// maxTypeDiffs caps the typeDiffs array while metadataSummary retains full counts.
    /// </summary>
    [TestMethod]
    public async Task DiffAssemblies_MaxTypeDiffs_LimitsTypeOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = Samples.RichLibraryDll,
                ["rightPath"] = Samples.RichLibraryV2Dll,
                ["maxTypeDiffs"] = 2
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        var typeDiffs = json.GetProperty("typeDiffs");
        Assert.IsLessThanOrEqualTo(2, typeDiffs.GetArrayLength());

        // Summary should still reflect full counts
        var summary = json.GetProperty("metadataSummary");
        var totalTypes = summary.GetProperty("typesAdded").GetInt32()
            + summary.GetProperty("typesRemoved").GetInt32()
            + summary.GetProperty("typesChanged").GetInt32();
        Assert.IsGreaterThan(2, totalTypes, "Summary should reflect all diffs, not the limited output");
    }

    /// <summary>
    /// maxMethodDiffs caps the methodDiffs array without altering the aggregate summary.
    /// </summary>
    [TestMethod]
    public async Task DiffAssemblies_MaxMethodDiffs_LimitsMethodOutput()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = Samples.RichLibraryDll,
                ["rightPath"] = Samples.RichLibraryV2Dll,
                ["maxMethodDiffs"] = 5
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        var methodDiffs = json.GetProperty("methodDiffs");
        Assert.IsLessThanOrEqualTo(5, methodDiffs.GetArrayLength());

        // Summary should still reflect full counts
        var summary = json.GetProperty("metadataSummary");
        var totalMethods = summary.GetProperty("methodsAdded").GetInt32()
            + summary.GetProperty("methodsRemoved").GetInt32()
            + summary.GetProperty("methodsChanged").GetInt32();
        Assert.IsGreaterThan(5, totalMethods, "Summary should reflect all diffs, not the limited output");
    }

    /// <summary>
    /// Type and method limits compose independently on the same diff invocation.
    /// </summary>
    [TestMethod]
    public async Task DiffAssemblies_BothLimits_LimitsBothOutputs()
    {
        await StartServerAsync();
        await using var client = await CreateClientAsync();

        var result = await client.CallToolAsync(
            "diff_assemblies",
            new Dictionary<string, object?>
            {
                ["leftPath"] = Samples.RichLibraryDll,
                ["rightPath"] = Samples.RichLibraryV2Dll,
                ["maxTypeDiffs"] = 3,
                ["maxMethodDiffs"] = 10
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsLessThanOrEqualTo(3, json.GetProperty("typeDiffs").GetArrayLength());
        Assert.IsLessThanOrEqualTo(10, json.GetProperty("methodDiffs").GetArrayLength());
    }

    // --- diff_size ---

    private static readonly string[] s_telemetryZeroGrowthBudget = ["ns=NativeAotConsole.Telemetry:growth=0"];
    private static readonly string[] s_generousGrowthBudget = ["total:growth=1000%"];
    private static readonly string[] s_bareGrowthBudget = ["growth=1%"];

    private static (string V1, string V2) RequireMstats()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        TestSkip.When(Samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        return (Samples.NativeAotConsoleMstat!, Samples.NativeAotConsoleV2Mstat!);
    }

    /// <summary>
    /// diff_size over the real V1/V2 mstat pair returns the summary, aggregates, and top
    /// contributors — and no tree unless asked.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreNotEqual(0, json.GetProperty("summary").GetProperty("delta").GetInt64());
        Assert.IsGreaterThan(0, json.GetProperty("assemblyDeltas").GetArrayLength());
        Assert.IsGreaterThan(0, json.GetProperty("namespaceDeltas").GetArrayLength());
        Assert.IsLessThanOrEqualTo(5, json.GetProperty("contributors").GetArrayLength());
        Assert.IsFalse(json.TryGetProperty("root", out var root) && root.ValueKind != JsonValueKind.Null);
    }

    /// <summary>diff_size of a report against itself returns a zero delta and no contributors.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreEqual(0, json.GetProperty("summary").GetProperty("delta").GetInt64());
        Assert.AreEqual(0, json.GetProperty("contributors").GetArrayLength());
    }

    /// <summary>
    /// includeTree with a tight node cap emits a pruned tree and says so through the
    /// truncation metadata — deterministic, never a silent sample.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.AreNotEqual(JsonValueKind.Null, json.GetProperty("root").ValueKind);
        Assert.IsTrue(json.GetProperty("treeTruncated").GetBoolean());
        Assert.IsGreaterThan(10, json.GetProperty("treeTotalNodes").GetInt32());
        Assert.IsLessThanOrEqualTo(10, json.GetProperty("treeIncludedNodes").GetInt32());
    }

    /// <summary>diff_size rejects an input that is not mstat-backed with a message naming the fix.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
                ["rightPath"] = Samples.RichLibraryDll
            },
            cancellationToken: TestCancellationToken);

        var text = GetTextContent(result);
        Assert.IsNotNull(text);
        Assert.Contains("not mstat-backed", text);
    }

    // --- check_size_budgets ---

    /// <summary>
    /// A zero-growth budget on the namespace added in V2 breaches, and the report carries the
    /// violation and its scoped contributors.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsFalse(json.GetProperty("passed").GetBoolean());
        var evaluation = json.GetProperty("evaluations")[0];
        Assert.IsGreaterThan(0, evaluation.GetProperty("violations").GetArrayLength());
        Assert.IsGreaterThan(0, evaluation.GetProperty("topContributors").GetArrayLength());
    }

    /// <summary>A generous budget passes.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.GetProperty("passed").GetBoolean());
    }

    /// <summary>
    /// budgetsJson carries the object form — named budgets with warning severity — at full
    /// parity with the CLI's budget file.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        var json = JsonSerializer.Deserialize<JsonElement>(text);
        Assert.IsTrue(json.GetProperty("passed").GetBoolean(), "warning severity must not fail the check");
        Assert.IsTrue(json.GetProperty("hasWarnings").GetBoolean());
        var evaluation = json.GetProperty("evaluations")[0];
        Assert.IsFalse(evaluation.GetProperty("passed").GetBoolean());
        Assert.AreEqual("telemetry-watch",
            evaluation.GetProperty("budget").GetProperty("name").GetString());
        Assert.IsLessThanOrEqualTo(3, evaluation.GetProperty("topContributors").GetArrayLength());
    }

    /// <summary>budgetFilePath loads the same document schema from disk.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
            Assert.IsNotNull(text);
            var json = JsonSerializer.Deserialize<JsonElement>(text);
            Assert.IsTrue(json.GetProperty("passed").GetBoolean());
        }
        finally
        {
            File.Delete(budgetFile);
        }
    }

    /// <summary>Every budget source absent is an error, not an empty pass.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        Assert.Contains("budget source is required", text);
    }

    /// <summary>A growth budget without a baseline is an error naming the missing parameter.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(text);
        Assert.Contains("baselinePath", text);
    }
}
