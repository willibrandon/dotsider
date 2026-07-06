namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A node in the hierarchical size-difference tree between two Native AOT builds. The tree
/// contains changed subtrees only — added, removed, grown, and shrunk entries; unchanged mass
/// is summarized in <see cref="SizeDiffSummary"/> instead of carried as zero-delta nodes.
/// </summary>
/// <param name="Name">Display name for this node. Method leaves carry their parameter list so overloads stay distinct.</param>
/// <param name="FullPath">A deterministic path from the root, unique within the tree.</param>
/// <param name="Kind">The granularity level of this node, in <see cref="SizeNode"/> terms.</param>
/// <param name="Diff">
/// The direction of the difference: <see cref="DiffKind.Added"/> or <see cref="DiffKind.Removed"/>
/// when the whole subtree exists on one side only, otherwise <see cref="DiffKind.Changed"/> —
/// grown when <see cref="Delta"/> is positive, shrunk when negative.
/// </param>
/// <param name="LeftSize">The bytes attributed on the baseline side (changed entries only for interior nodes).</param>
/// <param name="RightSize">The bytes attributed on the comparison side (changed entries only for interior nodes).</param>
/// <param name="Delta"><see cref="RightSize"/> minus <see cref="LeftSize"/>.</param>
/// <param name="Children">Child nodes ordered by absolute delta, largest first.</param>
/// <param name="LeftEntryCount">
/// The number of raw baseline report rows behind this node. Greater than one on a leaf means
/// the leaf is an aggregate (display collisions, frozen objects grouped by owner) and is
/// rendered as such.
/// </param>
/// <param name="RightEntryCount">The number of raw comparison-side report rows behind this node.</param>
/// <param name="LeftNodeNames">
/// Every dependency-graph node name behind the baseline rows. An aggregate maps to many DGML
/// nodes; keeping the full list keeps "why is this in my binary" answers honest.
/// </param>
/// <param name="RightNodeNames">Every dependency-graph node name behind the comparison-side rows.</param>
public sealed record SizeDiffNode(
    string Name,
    string FullPath,
    SizeNodeKind Kind,
    DiffKind Diff,
    long LeftSize,
    long RightSize,
    long Delta,
    IReadOnlyList<SizeDiffNode> Children,
    int LeftEntryCount,
    int RightEntryCount,
    IReadOnlyList<string> LeftNodeNames,
    IReadOnlyList<string> RightNodeNames);
