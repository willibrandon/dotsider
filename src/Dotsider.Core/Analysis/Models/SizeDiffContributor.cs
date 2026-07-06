namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One changed entry of a size diff in flat form — the shape a CI log or a budget violation
/// prints. Contributors carry the same identity and attribution as their tree leaves.
/// </summary>
/// <param name="Name">Display name. Method contributors carry their parameter list so overloads stay distinct.</param>
/// <param name="FullPath">The entry's deterministic path, matching its node in the delta tree.</param>
/// <param name="Kind">The entry's node kind.</param>
/// <param name="Diff">Added, removed, or changed (grown when <see cref="Delta"/> is positive, shrunk when negative).</param>
/// <param name="LeftSize">The baseline bytes, or 0 when added.</param>
/// <param name="RightSize">The comparison-side bytes, or 0 when removed.</param>
/// <param name="Delta"><see cref="RightSize"/> minus <see cref="LeftSize"/>.</param>
/// <param name="AssemblyName">
/// The assembly the bytes are attributed to (owner-based for frozen objects,
/// <see cref="Dotsider.Core.Analysis.MstatSizeIndex.UnattributedName"/> when unknowable), or an
/// empty string for global sections.
/// </param>
/// <param name="Namespace">The namespace the bytes are attributed to, or an empty string where none applies.</param>
/// <param name="LeftEntryCount">The number of raw baseline rows behind the entry; greater than one marks an aggregate.</param>
/// <param name="RightEntryCount">The number of raw comparison-side rows behind the entry.</param>
/// <param name="LeftNodeNames">Every baseline dependency-graph node name behind the entry.</param>
/// <param name="RightNodeNames">Every comparison-side dependency-graph node name behind the entry — the join keys for "why did this appear".</param>
public sealed record SizeDiffContributor(
    string Name,
    string FullPath,
    SizeNodeKind Kind,
    DiffKind Diff,
    long LeftSize,
    long RightSize,
    long Delta,
    string AssemblyName,
    string Namespace,
    int LeftEntryCount,
    int RightEntryCount,
    IReadOnlyList<string> LeftNodeNames,
    IReadOnlyList<string> RightNodeNames);
