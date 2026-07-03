namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A node in the size treemap hierarchy. Can be assembly, namespace, type, or method — or,
/// for Native AOT trees, a data category and its entries.
/// </summary>
/// <param name="Name">Display name for this node.</param>
/// <param name="FullPath">Fully qualified path from root (e.g., <c>Assembly/Namespace/Type</c>).</param>
/// <param name="Size">Size in bytes attributed to this node.</param>
/// <param name="Kind">The granularity level of this node.</param>
/// <param name="Children">Child nodes in the hierarchy.</param>
/// <param name="AotNodeName">
/// The ILC dependency-graph node name behind this entry, or null outside Native AOT trees.
/// The name matches a DGML node label, which is what makes "why is this in my binary"
/// answerable for the node.
/// </param>
/// <param name="NativeAddress">
/// The node's virtual address when it maps to a Native AOT function, or null. Cross-view navigation
/// reads this typed field rather than scraping <see cref="FullPath"/>.
/// </param>
public sealed record SizeNode(
    string Name,
    string FullPath,
    long Size,
    SizeNodeKind Kind,
    IReadOnlyList<SizeNode> Children,
    string? AotNodeName,
    ulong? NativeAddress)
{
    /// <summary>
    /// The pre-#178 shape (five or six arguments), preserved so existing construction sites keep
    /// compiling. <see cref="NativeAddress"/> defaults to null.
    /// </summary>
    /// <param name="name">Display name for this node.</param>
    /// <param name="fullPath">Fully qualified path from root.</param>
    /// <param name="size">Size in bytes attributed to this node.</param>
    /// <param name="kind">The granularity level of this node.</param>
    /// <param name="children">Child nodes in the hierarchy.</param>
    /// <param name="aotNodeName">The ILC dependency-graph node name, or null.</param>
    public SizeNode(
        string name, string fullPath, long size, SizeNodeKind kind,
        IReadOnlyList<SizeNode> children, string? aotNodeName = null)
        : this(name, fullPath, size, kind, children, aotNodeName, null)
    {
    }

    /// <summary>The pre-#178 six-output deconstruction, preserved alongside the generated seven-output one.</summary>
    /// <param name="name">Display name.</param>
    /// <param name="fullPath">Fully qualified path from root.</param>
    /// <param name="size">Size in bytes.</param>
    /// <param name="kind">The granularity level.</param>
    /// <param name="children">Child nodes.</param>
    /// <param name="aotNodeName">The ILC dependency-graph node name, or null.</param>
    public void Deconstruct(
        out string name, out string fullPath, out long size, out SizeNodeKind kind,
        out IReadOnlyList<SizeNode> children, out string? aotNodeName)
    {
        name = Name;
        fullPath = FullPath;
        size = Size;
        kind = Kind;
        children = Children;
        aotNodeName = AotNodeName;
    }
}
