using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Size differences between two mstat-backed inputs.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record SizeDiffPayload(
    string Left,
    string Right,
    SizeBasis TotalBasis,
    long? LeftTotal,
    long RightTotal,
    string LeftFormatVersion,
    string RightFormatVersion,
    SizeDiffSummary Summary,
    IReadOnlyList<SizeDiffAggregate> AssemblyDeltas,
    IReadOnlyList<SizeDiffAggregate> NamespaceDeltas,
    IReadOnlyList<SizeDiffContributor> Contributors,
    SizeDiffNode? Root,
    bool? TreeTruncated,
    int? TreeTotalNodes,
    int? TreeIncludedNodes);
