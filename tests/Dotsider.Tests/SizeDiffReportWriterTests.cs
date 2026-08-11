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

        Assert.Contains("| +24.1&nbsp;KB | Method | added |", markdown);
        Assert.DoesNotContain("| +24.1 KB | Method | added |", markdown);
    }

    private static SizeDiffReportWriter.Context CreateContext(
        string contributorName,
        long rightSize = 24_700)
    {
        var contributor = new SizeDiffContributor(
            contributorName,
            $"Methods/Scout.Automata/{contributorName}",
            SizeNodeKind.Method,
            DiffKind.Added,
            LeftSize: 0,
            RightSize: rightSize,
            Delta: rightSize,
            AssemblyName: "Scout.Automata",
            Namespace: "Scout",
            LeftEntryCount: 0,
            RightEntryCount: 1,
            LeftNodeNames: [],
            RightNodeNames: []);
        var summary = new SizeDiffSummary(
            LeftTotal: 0,
            RightTotal: contributor.RightSize,
            Delta: contributor.Delta,
            UnchangedTotal: 0,
            Counts: [new SizeDiffKindCounts(SizeNodeKind.Method, 1, 0, 0, 0, 0)],
            LeftDeduplicatedMethods: 0,
            RightDeduplicatedMethods: 0);
        var root = new SizeDiffNode(
            "root",
            "root",
            SizeNodeKind.Assembly,
            DiffKind.Added,
            LeftSize: 0,
            RightSize: contributor.RightSize,
            Delta: contributor.Delta,
            Children: [],
            LeftEntryCount: 0,
            RightEntryCount: 1,
            LeftNodeNames: [],
            RightNodeNames: []);
        var diff = new MstatDiffResult(
            "0.0",
            "2.2",
            root,
            summary,
            [contributor],
            AssemblyDeltas: [],
            NamespaceDeltas: []);
        return new SizeDiffReportWriter.Context(
            "/tmp/picket",
            BaselinePath: null,
            diff,
            SizeBasis.FileSize,
            RightTotal: 36_647_704,
            LeftTotal: null,
            Top: 20,
            WhyPaths: null,
            Budgets: null);
    }
}
