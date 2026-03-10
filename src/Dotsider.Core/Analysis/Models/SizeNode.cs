namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A node in the size treemap hierarchy. Can be assembly, namespace, type, or method.
/// </summary>
public sealed record SizeNode(
    string Name,
    string FullPath,
    long Size,
    SizeNodeKind Kind,
    IReadOnlyList<SizeNode> Children);
