using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Builds the serializable payloads the <c>diff-size</c> and <c>check-size-budgets</c>
/// surfaces return. The MCP server's direct mode and the running-session protocol handler
/// both call these, so the two transports cannot drift apart in shape or semantics.
/// </summary>
public static class SizeDiffPayloadBuilder
{
    /// <summary>The default contributor count when a caller does not pin one.</summary>
    public const int DefaultTopN = 20;

    /// <summary>The default delta-tree node cap when a caller asks for the tree without one.</summary>
    public const int DefaultMaxNodes = 500;

    /// <summary>
    /// Builds the <c>diff-size</c> payload: the diff's summary, aggregates, and top
    /// contributors, plus — only on request — the delta tree, pruned depth-first by absolute
    /// delta to a node cap with explicit truncation metadata, because a full tree for a real
    /// application is enormous.
    /// </summary>
    /// <param name="left">The baseline input.</param>
    /// <param name="right">The input under comparison.</param>
    /// <param name="topN">How many contributors to include, or null for <see cref="DefaultTopN"/>.</param>
    /// <param name="includeTree">Whether to include the delta tree.</param>
    /// <param name="maxNodes">The tree node cap, or null for <see cref="DefaultMaxNodes"/>.</param>
    /// <returns>The serializable payload.</returns>
    public static SizeDiffPayload BuildDiffPayload(
        MstatSource left, MstatSource right, int? topN, bool includeTree, int? maxNodes)
    {
        var diff = MstatDiffer.Compare(left.Data, right.Data);
        var totals = SizeBasisResolver.Resolve(right, left, diff);
        var top = Math.Max(0, topN ?? DefaultTopN);

        SizeDiffNode? root = null;
        var totalNodes = 0;
        var includedNodes = 0;
        if (includeTree)
        {
            totalNodes = CountNodes(diff.Root);
            var cap = Math.Max(1, maxNodes ?? DefaultMaxNodes);
            root = totalNodes <= cap ? diff.Root : PruneTree(diff.Root, cap);
            includedNodes = totalNodes <= cap ? totalNodes : CountNodes(root!);
        }

        return new SizeDiffPayload(
            left.BinaryPath ?? left.MstatPath,
            right.BinaryPath ?? right.MstatPath,
            totals.Basis,
            totals.LeftTotal,
            totals.RightTotal,
            diff.LeftFormatVersion,
            diff.RightFormatVersion,
            diff.Summary,
            diff.AssemblyDeltas,
            diff.NamespaceDeltas,
            [.. diff.Contributors.Take(top)],
            root,
            includeTree ? includedNodes < totalNodes : null,
            includeTree ? totalNodes : null,
            includeTree ? includedNodes : null);
    }

    /// <summary>
    /// Builds the <c>check-size-budgets</c> payload: the basis-resolved totals and the budget
    /// report. Growth budgets without a baseline are the caller's error to reject; this
    /// builder evaluates what it is given.
    /// </summary>
    /// <param name="target">The build under check.</param>
    /// <param name="baseline">The baseline, or null for an absolute-only gate.</param>
    /// <param name="budgets">The budgets to evaluate.</param>
    /// <param name="topN">Contributors per violated budget, or null for <see cref="DefaultTopN"/>.</param>
    /// <returns>The serializable payload.</returns>
    public static SizeBudgetPayload BuildBudgetPayload(
        MstatSource target, MstatSource? baseline, IReadOnlyList<SizeBudget> budgets, int? topN)
    {
        var diff = MstatDiffer.Compare(baseline?.Data ?? MstatData.Empty, target.Data);
        var totals = SizeBasisResolver.Resolve(target, baseline, diff);
        var report = SizeBudgetEvaluator.Evaluate(
            budgets, diff, totals.Basis, totals.RightTotal, totals.LeftTotal,
            defaultTopN: Math.Max(0, topN ?? DefaultTopN));

        return new SizeBudgetPayload(
            target.BinaryPath ?? target.MstatPath,
            baseline is null ? null : baseline.BinaryPath ?? baseline.MstatPath,
            report.Passed,
            report.HasWarnings,
            report.TotalBasis,
            report.LeftTotal,
            report.RightTotal,
            report.LeftMstatTotal,
            report.RightMstatTotal,
            report.Evaluations);
    }

    private static int CountNodes(SizeDiffNode node) => 1 + node.Children.Sum(CountNodes);

    /// <summary>
    /// Prunes the tree to at most <paramref name="cap"/> nodes, walking children depth-first
    /// in their existing largest-absolute-delta order: the biggest subtrees survive whole
    /// before smaller siblings get any budget — a deterministic cut, never a sample.
    /// </summary>
    private static SizeDiffNode PruneTree(SizeDiffNode root, int cap)
    {
        var remaining = cap - 1; // the root itself
        return root with { Children = PruneChildren(root.Children, ref remaining) };
    }

    private static List<SizeDiffNode> PruneChildren(
        IReadOnlyList<SizeDiffNode> children, ref int remaining)
    {
        var kept = new List<SizeDiffNode>();
        foreach (var child in children)
        {
            if (remaining <= 0) break;
            remaining--;
            kept.Add(child.Children.Count == 0
                ? child
                : child with { Children = PruneChildren(child.Children, ref remaining) });
        }

        return kept;
    }
}
