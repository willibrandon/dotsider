namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A node in the size treemap hierarchy. Can be assembly, namespace, type, or method.
/// </summary>
/// <param name="Name">Display name for this node.</param>
/// <param name="FullPath">Fully qualified path from root (e.g., <c>Assembly/Namespace/Type</c>).</param>
/// <param name="Size">Size in bytes attributed to this node.</param>
/// <param name="Kind">The granularity level of this node.</param>
/// <param name="Children">Child nodes in the hierarchy.</param>
public sealed record SizeNode(
    string Name,
    string FullPath,
    long Size,
    SizeNodeKind Kind,
    IReadOnlyList<SizeNode> Children);
