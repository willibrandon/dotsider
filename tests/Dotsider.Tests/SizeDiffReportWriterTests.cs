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
    /// Verifies the size and unit stay together in a narrow Markdown summary.
    /// </summary>
    [TestMethod]
    public void BuildMarkdown_ContributorDelta_UsesNonbreakingSpace()
    {
        var context = CreateContext("Compile()", rightSize: 24_700);

        var markdown = SizeDiffReportWriter.BuildMarkdown(context);

        Assert.Contains("- **24.1&nbsp;KB** · Method —", markdown);
        Assert.DoesNotContain("- **24.1 KB** · Method —", markdown);
    }

    /// <summary>
    /// Verifies current-build mode reports absolute size without synthetic changes.
    /// </summary>
    [TestMethod]
    public void BuildMarkdown_WithoutBaseline_UsesCurrentBuildLayout()
    {
        var context = CreateContext("Compile()", withBudget: true);

        var markdown = SizeDiffReportWriter.BuildMarkdown(context);

        Assert.Contains(
            "✅ **PASS** — all size budgets passed. No baseline comparison was run.",
            markdown);
        Assert.DoesNotContain("> ✅", markdown);
        Assert.DoesNotContain("Snapshot", markdown);
        Assert.DoesNotContain("Legend", markdown);
        Assert.Contains("### Overview", markdown);
        Assert.Contains("- **Mode:** Current build", markdown);
        Assert.Contains("### Budgets", markdown);
        Assert.Contains("- **✅ PASS** — `total: max 40.0 MB`", markdown);
        Assert.Contains("  - **Value:** 34.9&nbsp;MB", markdown);
        Assert.Contains("  - **Basis:** fileSize", markdown);
        Assert.Contains("`total: max 40.0 MB`", markdown);
        Assert.Contains("### Contents", markdown);
        Assert.Contains("- **Method:** 1", markdown);
        Assert.Contains("### Assemblies", markdown);
        Assert.Contains("- `Scout.Automata` — **24.1&nbsp;KB**", markdown);
        Assert.Contains("### Largest contributors (top 20)", markdown);
        Assert.IsLessThan(markdown.IndexOf("### Largest contributors", StringComparison.Ordinal),
            markdown.IndexOf("### Contents", StringComparison.Ordinal));
        var gap = $"---{Environment.NewLine}{Environment.NewLine}### ";
        Assert.Contains(gap + "Overview", markdown);
        Assert.Contains(gap + "Budgets", markdown);
        Assert.Contains(gap + "Contents", markdown);
        Assert.Contains(gap + "Assemblies", markdown);
        Assert.Contains(gap + "Namespaces", markdown);
        Assert.Contains(gap + "Largest contributors", markdown);
        Assert.DoesNotContain("|", markdown);
        Assert.DoesNotContain("### Regressions", markdown);
    }

    /// <summary>
    /// Verifies a real baseline retains the comparison columns and regression terminology.
    /// </summary>
    [TestMethod]
    public void BuildMarkdown_WithBaseline_UsesComparisonLayout()
    {
        var context = CreateContext("Compile()", withBaseline: true);

        var markdown = SizeDiffReportWriter.BuildMarkdown(context);

        Assert.Contains("- **Baseline:** `/tmp/picket-baseline`", markdown);
        Assert.Contains("- **Mode:** Compared with baseline", markdown);
        Assert.Contains("### Changes", markdown);
        Assert.Contains("- **Method:** added 0 · removed 0 · grown 1 · shrunk 0 · unchanged 0", markdown);
        Assert.Contains("- `Scout.Automata` — 9.8&nbsp;KB → 24.1&nbsp;KB (Δ+14.4&nbsp;KB)", markdown);
        Assert.Contains("### Regressions (top 20)", markdown);
        Assert.Contains("- **+14.4&nbsp;KB** · Method · grown —", markdown);
        Assert.DoesNotContain("|", markdown);
        Assert.DoesNotContain("### Contents", markdown);
        Assert.DoesNotContain("### Largest contributors", markdown);
    }

    /// <summary>
    /// Verifies the Markdown report displays the complete Native AOT contributor name.
    /// </summary>
    [TestMethod]
    public void BuildMarkdown_LongMethodName_DisplaysCompleteContributorName()
    {
        const string contributorName = "Compile(Scout.RegexNfa, Scout.RegexPrefilter, "
            + "System.Nullable`1<ulong>, Scout.RegexLiteralSetEngine, "
            + "Scout.RegexAlternationSetEngine, System.ReadOnlyMemory`1<byte>, "
            + "System.Nullable`1<Scout.RegexCompileOptions>, Scout.RegexNfa)";
        var context = CreateContext(contributorName);

        var markdown = SizeDiffReportWriter.BuildMarkdown(context);
        var document = SizeDiffReportWriter.BuildDocument(context);

        Assert.Contains(contributorName, markdown);
        Assert.DoesNotContain("Full contributor names", markdown);
        Assert.DoesNotContain("…", markdown);
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
                BaselineBytes: withBaseline ? leftTotal : null,
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
