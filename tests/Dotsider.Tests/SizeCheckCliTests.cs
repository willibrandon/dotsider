using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Subprocess integration tests for <c>dotsider size-check</c> and the mstat routing of
/// <c>dotsider diff</c>, asserting exit codes (0 pass, 1 error, 2 budget exceeded), stream
/// content, and the JSON document shape against the real V1/V2 published pair.
/// </summary>
[TestClass]
public class SizeCheckCliTests
{
    private static SampleAssemblyFixture Fixture => SampleAssemblyHost.Instance;

    private static (string V1, string V2) RequireMstats()
    {
        TestSkip.When(Fixture.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        TestSkip.When(Fixture.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        return (Fixture.NativeAotConsoleMstat!, Fixture.NativeAotConsoleV2Mstat!);
    }

    /// <summary>Verifies a self-baseline report succeeds with exit 0 and prints the basis.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_SelfBaseline_Exit0()
    {
        var (v1, _) = RequireMstats();

        var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", v1, "--baseline", v1);

        Assert.AreEqual(0, exitCode);
        Assert.Contains("Basis:      mstatTotal", stdout);
        Assert.Contains("Formats:    2.2 -> 2.2", stdout);
    }

    /// <summary>
    /// Verifies a zero-growth budget on the namespace added in V2 exits 2 and prints the
    /// namespace's own members as the top contributors.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_TelemetryZeroGrowth_Exit2_PrintsContributors()
    {
        var (v1, v2) = RequireMstats();

        var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", v2, "--baseline", v1,
            "--budget", "ns=NativeAotConsole.Telemetry:growth=0");

        Assert.AreEqual(2, exitCode);
        Assert.Contains("FAIL", stdout);
        Assert.Contains("NativeAotConsole.Telemetry", stdout);
        Assert.Contains("Top contributors:", stdout);
        Assert.Contains("Summarize()", stdout);
        Assert.Contains("Result: FAIL", stdout);
    }

    /// <summary>
    /// Verifies a warning-severity breach reports but exits 0 — warnings never gate.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_WarningOnlyBreach_Exit0()
    {
        var (v1, v2) = RequireMstats();
        var budgetFile = Path.Combine(Path.GetTempPath(), $"dotsider-budgets-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(budgetFile, """
                { "budgets": [ {
                    "name": "telemetry-watch",
                    "scope": "ns=NativeAotConsole.Telemetry",
                    "growth": "0",
                    "severity": "warning"
                } ] }
                """, CancellationToken.None);

            var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
                "size-check", v2, "--baseline", v1, "--budget-file", budgetFile);

            Assert.AreEqual(0, exitCode);
            Assert.Contains("WARN", stdout);
            Assert.Contains("telemetry-watch", stdout);
            Assert.Contains("Result: PASS (with warnings)", stdout);
        }
        finally
        {
            File.Delete(budgetFile);
        }
    }

    /// <summary>
    /// Verifies a binary with no mstat sidecar exits 1 with a precise error naming the fix.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_MissingMstat_Exit1_ErrorOnStderr()
    {
        TestSkip.When(Fixture.NativeAotConsoleExe is null, "AOT binary was not produced");
        TestSkip.When(!File.Exists(Fixture.NativeAotConsoleExe), "AOT binary missing on disk");

        // The real exe copied alone: no sidecar in the temp directory to discover.
        var tempDir = Directory.CreateTempSubdirectory("dotsider-nomstat-");
        try
        {
            var lonelyExe = Path.Combine(tempDir.FullName, Path.GetFileName(Fixture.NativeAotConsoleExe!));
            File.Copy(Fixture.NativeAotConsoleExe!, lonelyExe);

            var (exitCode, _, stderr) = await TestHelpers.RunDotsiderAsync(
                "size-check", lonelyExe, "--budget", "max=1gb");

            Assert.AreEqual(1, exitCode);
            Assert.Contains("not mstat-backed", stderr);
            Assert.Contains("IlcGenerateMstatFile", stderr);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a corrupt (byte-truncated copy of the real) baseline is handled
    /// deterministically: either the reader recovers a partial prefix and the report
    /// succeeds, or the input is rejected with exit 1 — never a crash.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_TruncatedBaseline_NoCrash()
    {
        var (v1, v2) = RequireMstats();
        var truncated = Path.Combine(Path.GetTempPath(), $"dotsider-badbase-{Guid.NewGuid():N}.mstat");
        try
        {
            var bytes = await File.ReadAllBytesAsync(v1, CancellationToken.None);
            await File.WriteAllBytesAsync(
                truncated, bytes[..(bytes.Length / 3)], CancellationToken.None);

            var (exitCode, _, stderr) = await TestHelpers.RunDotsiderAsync(
                "size-check", v2, "--baseline", truncated);

            Assert.IsTrue(exitCode is 0 or 1, $"unexpected exit {exitCode}: {stderr}");
            if (exitCode == 1)
                Assert.Contains("not mstat-backed", stderr);
        }
        finally
        {
            File.Delete(truncated);
        }
    }

    /// <summary>Verifies an invalid budget spec exits 1 naming the offending part.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_InvalidBudgetSpec_Exit1()
    {
        var (v1, v2) = RequireMstats();

        var (exitCode, _, stderr) = await TestHelpers.RunDotsiderAsync(
            "size-check", v2, "--baseline", v1, "--budget", "cap=25mb");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("cap=25mb", stderr);
    }

    /// <summary>Verifies a growth budget without a baseline exits 1.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_GrowthBudgetWithoutBaseline_Exit1()
    {
        var (_, v2) = RequireMstats();

        var (exitCode, _, stderr) = await TestHelpers.RunDotsiderAsync(
            "size-check", v2, "--budget", "growth=1%");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("needs --baseline", stderr);
    }

    /// <summary>Verifies bare report mode without a baseline exits 1 pointing at analyze --size.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_NoBaselineNoBudgets_Exit1()
    {
        var (_, v2) = RequireMstats();

        var (exitCode, _, stderr) = await TestHelpers.RunDotsiderAsync("size-check", v2);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("--baseline", stderr);
        Assert.Contains("analyze", stderr);
    }

    /// <summary>Verifies invalid budget-file JSON exits 1.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_InvalidBudgetFileJson_Exit1()
    {
        var (v1, v2) = RequireMstats();
        var budgetFile = Path.Combine(Path.GetTempPath(), $"dotsider-badjson-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(budgetFile, "{ not json", CancellationToken.None);

            var (exitCode, _, stderr) = await TestHelpers.RunDotsiderAsync(
                "size-check", v2, "--baseline", v1, "--budget-file", budgetFile);

            Assert.AreEqual(1, exitCode);
            Assert.Contains("not valid JSON", stderr);
        }
        finally
        {
            File.Delete(budgetFile);
        }
    }

    /// <summary>
    /// Verifies the JSON document shape: camelCase fields, the basis, both totals, summary,
    /// aggregates, contributors trimmed to --top, and the budgets block with a failed
    /// evaluation.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_Json_DocumentShape()
    {
        var (v1, v2) = RequireMstats();

        var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", v2, "--baseline", v1, "--top", "5",
            "--budget", "ns=NativeAotConsole.Telemetry:growth=0", "--json");

        Assert.AreEqual(2, exitCode);
        var json = JsonSerializer.Deserialize<JsonElement>(stdout);
        Assert.AreEqual(1, json.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("mstatTotal", json.GetProperty("totalBasis").GetString());
        Assert.IsGreaterThan(0, json.GetProperty("leftTotal").GetInt64());
        Assert.IsGreaterThan(0, json.GetProperty("rightTotal").GetInt64());
        Assert.AreEqual("2.2", json.GetProperty("leftFormatVersion").GetString());
        Assert.AreNotEqual(0, json.GetProperty("summary").GetProperty("delta").GetInt64());
        Assert.IsGreaterThan(0, json.GetProperty("assemblyDeltas").GetArrayLength());
        Assert.IsGreaterThan(0, json.GetProperty("namespaceDeltas").GetArrayLength());
        Assert.IsLessThanOrEqualTo(5, json.GetProperty("contributors").GetArrayLength());
        var budgets = json.GetProperty("budgets");
        Assert.IsFalse(budgets.GetProperty("passed").GetBoolean());
        var evaluation = budgets.GetProperty("evaluations")[0];
        Assert.IsFalse(evaluation.GetProperty("passed").GetBoolean());
        Assert.IsGreaterThan(0, evaluation.GetProperty("violations").GetArrayLength());
        Assert.IsGreaterThan(0, evaluation.GetProperty("topContributors").GetArrayLength());
    }

    /// <summary>
    /// Verifies the basis rules: an mstat pair reports mstatTotal, a binary pair reports
    /// fileSize with both bases surfaced, and a mixed pair falls back to mstatTotal.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_BasisFollowsInputKinds()
    {
        var (v1, v2) = RequireMstats();
        TestSkip.When(Fixture.NativeAotConsoleExe is null, "V1 AOT binary was not produced");
        TestSkip.When(Fixture.NativeAotConsoleV2Exe is null, "V2 AOT binary was not produced");

        var (_, mstatPair, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", v2, "--baseline", v1, "--json");
        var mstatJson = JsonSerializer.Deserialize<JsonElement>(mstatPair);
        Assert.AreEqual("mstatTotal", mstatJson.GetProperty("totalBasis").GetString());

        var (_, binaryPair, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", Fixture.NativeAotConsoleV2Exe!,
            "--baseline", Fixture.NativeAotConsoleExe!, "--json");
        var binaryJson = JsonSerializer.Deserialize<JsonElement>(binaryPair);
        Assert.AreEqual("fileSize", binaryJson.GetProperty("totalBasis").GetString());
        Assert.IsGreaterThan(0, binaryJson.GetProperty("leftMstatTotal").GetInt64());
        Assert.IsGreaterThan(0, binaryJson.GetProperty("rightMstatTotal").GetInt64());

        var (_, mixedPair, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", Fixture.NativeAotConsoleV2Exe!, "--baseline", v1, "--json");
        var mixedJson = JsonSerializer.Deserialize<JsonElement>(mixedPair);
        Assert.AreEqual("mstatTotal", mixedJson.GetProperty("totalBasis").GetString());
    }

    /// <summary>Verifies markdown output renders tables and the verdict line.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_FormatMarkdown_EmitsTables()
    {
        var (v1, v2) = RequireMstats();

        var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", v2, "--baseline", v1, "--format", "markdown",
            "--budget", "ns=NativeAotConsole.Telemetry:growth=0");

        Assert.AreEqual(2, exitCode);
        Assert.Contains("## Size check", stdout);
        Assert.Contains("| Kind | Added | Removed | Grown | Shrunk | Unchanged |", stdout);
        Assert.Contains("### Budgets", stdout);
        Assert.Contains("> ❌ **FAIL** — a size budget was exceeded.", stdout);
    }

    /// <summary>
    /// Verifies --summary-file writes the markdown report alongside the text stdout — the
    /// GitHub step-summary wiring.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_SummaryFile_WritesMarkdownAlongsideText()
    {
        var (v1, v2) = RequireMstats();
        var summaryFile = Path.Combine(Path.GetTempPath(), $"dotsider-summary-{Guid.NewGuid():N}.md");
        try
        {
            var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
                "size-check", v2, "--baseline", v1, "--summary-file", summaryFile);

            Assert.AreEqual(0, exitCode);
            Assert.Contains("Size check:", stdout);
            var markdown = await File.ReadAllTextAsync(summaryFile, CancellationToken.None);
            Assert.Contains("## Size check", markdown);
        }
        finally
        {
            File.Delete(summaryFile);
        }
    }

    /// <summary>Verifies --json conflicts with a non-json --format.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_JsonConflictingFormat_Exit1()
    {
        var (v1, v2) = RequireMstats();

        var (exitCode, _, stderr) = await TestHelpers.RunDotsiderAsync(
            "size-check", v2, "--baseline", v1, "--json", "--format", "markdown");

        Assert.AreEqual(1, exitCode);
        Assert.Contains("--json conflicts", stderr);
    }

    /// <summary>
    /// Verifies markdown output carries the resolved why chains — a CI step summary must
    /// keep the dependency-chain information the text and JSON outputs include.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_MarkdownWhy_EmitsChains()
    {
        var (v1, v2) = RequireMstats();
        TestSkip.When(Fixture.NativeAotConsoleV2Dgml is null, "V2 DGML sidecar was not produced");

        var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", v2, "--baseline", v1, "--top", "25", "--why", "--format", "markdown");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("### Why did these appear?", stdout);
        Assert.Contains("kept by (root first):", stdout);
    }

    /// <summary>
    /// Verifies added rows are selected for why-chains before the top-N cut: diffing in the
    /// shrinking direction (V2 as the baseline) makes removals dominate the overall top-N by
    /// absolute delta, yet the regressions section — added rows — must still carry chains.
    /// Selecting top-N before filtering to added rows would resolve none here.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_Why_CoversAddedRowsBeyondOverallTopN()
    {
        var (v1, v2) = RequireMstats();
        TestSkip.When(Fixture.NativeAotConsoleDgml is null, "V1 DGML sidecar was not produced");

        var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", v1, "--baseline", v2, "--top", "3", "--why");

        Assert.AreEqual(0, exitCode);
        Assert.Contains("  why ", stdout);
    }

    /// <summary>
    /// Verifies --why attaches dependency chains for the top added contributors when the
    /// target's DGML sits beside it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task SizeCheck_Why_AttachesPaths()
    {
        var (v1, v2) = RequireMstats();
        TestSkip.When(Fixture.NativeAotConsoleV2Dgml is null, "V2 DGML sidecar was not produced");

        var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
            "size-check", v2, "--baseline", v1, "--top", "25", "--why", "--json");

        Assert.AreEqual(0, exitCode);
        var json = JsonSerializer.Deserialize<JsonElement>(stdout);
        var withWhy = json.GetProperty("contributors").EnumerateArray()
            .Where(c => c.TryGetProperty("whyPath", out var why)
                && why.ValueKind == JsonValueKind.Array && why.GetArrayLength() > 0)
            .ToList();
        Assert.IsNotEmpty(withWhy);
    }

    /// <summary>
    /// Verifies diff refuses to compare an mstat-backed input against a managed assembly —
    /// the two sides would measure different things.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Diff_MixedMstatAndAssembly_Exit1()
    {
        var (v1, _) = RequireMstats();

        var (exitCode, _, stderr) = await TestHelpers.RunDotsiderAsync(
            "diff", v1, Fixture.RichLibraryDll);

        Assert.AreEqual(1, exitCode);
        Assert.Contains("mstat-backed", stderr);
    }

    /// <summary>
    /// Verifies diff --json on an mstat pair emits the size-diff document headlessly instead
    /// of opening the TUI — subprocess-safe because no alternate screen is entered.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Diff_MstatPairJson_EmitsSizeDiffDocument()
    {
        var (v1, v2) = RequireMstats();

        var (exitCode, stdout, _) = await TestHelpers.RunDotsiderAsync(
            "diff", v1, v2, "--json");

        Assert.AreEqual(0, exitCode);
        var json = JsonSerializer.Deserialize<JsonElement>(stdout);
        Assert.AreEqual("mstatTotal", json.GetProperty("totalBasis").GetString());
        Assert.AreNotEqual(0, json.GetProperty("summary").GetProperty("delta").GetInt64());
        Assert.IsGreaterThan(0, json.GetProperty("contributors").GetArrayLength());
        Assert.IsFalse(json.TryGetProperty("budgets", out var budgets)
            && budgets.ValueKind != JsonValueKind.Null);
    }
}
