using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Infrastructure;

namespace Dotsider.Tests;

/// <summary>Verifies the shared headless size-report rendering.</summary>
[TestClass]
public sealed class SizeDiffReportWriterTests
{
    /// <summary>
    /// Verifies CLR generic-arity backticks remain inside one Markdown code span instead of
    /// terminating the contributor cell's formatting.
    /// </summary>
    [TestMethod]
    public void BuildMarkdown_GenericContributor_UsesLongerCodeFence()
    {
        const string contributorName = "Compile(System.Nullable`1<ulong>, "
            + "System.ReadOnlyMemory`1<byte>, System.Nullable`1<Scout.RegexCompileOptions>)";
        var context = CreateContext(contributorName);

        var markdown = SizeDiffReportWriter.BuildMarkdown(context);

        Assert.Contains(
            "``Compile(System.Nullable`1<ulong>, System.ReadOnlyMemory`1<byte>, "
                + "System.Nullable`1<Scout.RegexCompileOptions>)  [Scout.Automata]``",
            markdown);
        Assert.DoesNotContain(
            "| `Compile(System.Nullable`1<ulong>",
            markdown);
    }

    /// <summary>
    /// Verifies the size and unit stay together in a narrow Markdown table column.
    /// </summary>
    [TestMethod]
    public void BuildMarkdown_ContributorDelta_UsesNonbreakingSpace()
    {
        var context = CreateContext("Compile()", rightSize: 24_700);

        var markdown = SizeDiffReportWriter.BuildMarkdown(context);

        Assert.Contains("| 24.1&nbsp;KB | Method |", markdown);
        Assert.DoesNotContain("| 24.1 KB | Method |", markdown);
    }

    /// <summary>
    /// Verifies an absolute check is presented as a current-build snapshot rather than a
    /// comparison against a synthetic empty build.
    /// </summary>
    [TestMethod]
    public void BuildMarkdown_WithoutBaseline_UsesSnapshotLayout()
    {
        var context = CreateContext("Compile()", withBudget: true);

        var markdown = SizeDiffReportWriter.BuildMarkdown(context);

        Assert.Contains("> ✅ **PASS** — all size budgets passed.", markdown);
        Assert.Contains("> ℹ️ **Snapshot** — no baseline was supplied.", markdown);
        Assert.Contains("### Overview", markdown);
        Assert.Contains("### Budgets", markdown);
        Assert.Contains("| Status | Budget | Current | Basis |", markdown);
        Assert.Contains("`total: max 40.0 MB`", markdown);
        Assert.Contains("### Contents", markdown);
        Assert.Contains("| Kind | Count |", markdown);
        Assert.Contains("### Assemblies", markdown);
        Assert.Contains("| Name | Size |", markdown);
        Assert.Contains("### Largest contributors (top 20)", markdown);
        Assert.IsLessThan(markdown.IndexOf("### Largest contributors", StringComparison.Ordinal),
            markdown.IndexOf("### Contents", StringComparison.Ordinal));
        Assert.DoesNotContain("| Kind | Added |", markdown);
        Assert.DoesNotContain("| Name | Baseline | Current |", markdown);
        Assert.DoesNotContain("### Regressions", markdown);
        Assert.DoesNotContain("| Change |", markdown);
        Assert.DoesNotContain("| added |", markdown);
    }

    /// <summary>
    /// Verifies a real baseline retains the comparison columns and regression terminology.
    /// </summary>
    [TestMethod]
    public void BuildMarkdown_WithBaseline_UsesComparisonLayout()
    {
        var context = CreateContext("Compile()", withBaseline: true);

        var markdown = SizeDiffReportWriter.BuildMarkdown(context);

        Assert.Contains("| Baseline | `/tmp/picket-baseline` |", markdown);
        Assert.Contains("### Changes", markdown);
        Assert.Contains("| Kind | Added | Removed | Grown | Shrunk | Unchanged |", markdown);
        Assert.Contains("| Name | Baseline | Current | Δ |", markdown);
        Assert.Contains("### Regressions (top 20)", markdown);
        Assert.Contains("| +14.4&nbsp;KB | Method | grown |", markdown);
        Assert.DoesNotContain("### Contents", markdown);
        Assert.DoesNotContain("### Largest contributors", markdown);
    }

    /// <summary>
    /// Verifies very long Native AOT method signatures do not dominate a CI summary while
    /// the machine-readable report keeps the complete symbol name.
    /// </summary>
    [TestMethod]
    public void BuildMarkdown_LongMethodName_CompactsSummaryAndPreservesJsonName()
    {
        const string contributorName = "Compile(Scout.RegexNfa, Scout.RegexPrefilter, "
            + "System.Nullable`1<ulong>, Scout.RegexLiteralSetEngine, "
            + "Scout.RegexAlternationSetEngine, System.ReadOnlyMemory`1<byte>, "
            + "System.Nullable`1<Scout.RegexCompileOptions>, Scout.RegexNfa)";
        var context = CreateContext(contributorName);

        var markdown = SizeDiffReportWriter.BuildMarkdown(context);
        var document = SizeDiffReportWriter.BuildDocument(context);

        Assert.Contains("`Compile(Scout.RegexNfa, Scout.RegexPrefilter, …)  [Scout.Automata]`", markdown);
        Assert.Contains("_Full contributor names remain available in the JSON report._", markdown);
        Assert.DoesNotContain(contributorName, markdown);
        Assert.HasCount(1, document.Contributors);
        Assert.AreEqual(contributorName, document.Contributors[0].Name);
    }

    private static SizeDiffReportWriter.Context CreateContext(
        string contributorName,
        long rightSize = 24_700,
        bool withBaseline = false,
        bool withBudget = false)
    {
        var leftSize = withBaseline ? 10_000 : 0;
        var delta = rightSize - leftSize;
        var contributor = new SizeDiffContributor(
            contributorName,
            $"Methods/Scout.Automata/{contributorName}",
            SizeNodeKind.Method,
            withBaseline ? DiffKind.Changed : DiffKind.Added,
            LeftSize: leftSize,
            RightSize: rightSize,
            Delta: delta,
            AssemblyName: "Scout.Automata",
            Namespace: "Scout",
            LeftEntryCount: withBaseline ? 1 : 0,
            RightEntryCount: 1,
            LeftNodeNames: withBaseline ? [contributorName] : [],
            RightNodeNames: [contributorName]);
        var summary = new SizeDiffSummary(
            LeftTotal: leftSize,
            RightTotal: contributor.RightSize,
            Delta: contributor.Delta,
            UnchangedTotal: 0,
            Counts: [new SizeDiffKindCounts(
                SizeNodeKind.Method,
                Added: withBaseline ? 0 : 1,
                Removed: 0,
                Grown: withBaseline ? 1 : 0,
                Shrunk: 0,
                Unchanged: 0)],
            LeftDeduplicatedMethods: 0,
            RightDeduplicatedMethods: 0);
        var root = new SizeDiffNode(
            "root",
            "root",
            SizeNodeKind.Assembly,
            withBaseline ? DiffKind.Changed : DiffKind.Added,
            LeftSize: leftSize,
            RightSize: contributor.RightSize,
            Delta: delta,
            Children: [],
            LeftEntryCount: withBaseline ? 1 : 0,
            RightEntryCount: 1,
            LeftNodeNames: withBaseline ? [contributorName] : [],
            RightNodeNames: [contributorName]);
        var diff = new MstatDiffResult(
            "0.0",
            "2.2",
            root,
            summary,
            [contributor],
            AssemblyDeltas: [new SizeDiffAggregate("Scout.Automata", leftSize, rightSize, delta)],
            NamespaceDeltas: [new SizeDiffAggregate("Scout", leftSize, rightSize, delta)]);
        const long rightTotal = 36_647_704;
        long? leftTotal = withBaseline ? 30_000_000 : null;
        SizeBudgetReport? budgets = null;
        if (withBudget)
        {
            var budget = SizeBudgetParser.Parse("max=40mb");
            var evaluation = new SizeBudgetEvaluation(
                budget,
                Passed: true,
                SizeBasis.FileSize,
                ActualBytes: rightTotal,
                BaselineBytes: null,
                Violations: [],
                TopContributors: [contributor]);
            budgets = new SizeBudgetReport(
                Passed: true,
                HasWarnings: false,
                SizeBasis.FileSize,
                LeftTotal: 0,
                RightTotal: rightTotal,
                LeftMstatTotal: null,
                RightMstatTotal: rightSize,
                Evaluations: [evaluation]);
        }

        return new SizeDiffReportWriter.Context(
            "/tmp/picket",
            BaselinePath: withBaseline ? "/tmp/picket-baseline" : null,
            diff,
            SizeBasis.FileSize,
            RightTotal: rightTotal,
            LeftTotal: leftTotal,
            Top: 20,
            WhyPaths: null,
            Budgets: budgets);
    }
}
