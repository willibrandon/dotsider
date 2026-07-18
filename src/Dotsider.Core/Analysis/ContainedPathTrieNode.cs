using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Represents one canonical path segment in a <see cref="ContainedPathTrie"/>.
/// </summary>
/// <param name="comparer">The platform filesystem segment comparer.</param>
internal sealed class ContainedPathTrieNode(StringComparer comparer)
{
    private readonly Dictionary<string, ContainedPathTrieNode> _children = new(comparer);

    /// <summary>
    /// Gets every package entry whose destination is at or beneath this node.
    /// </summary>
    internal List<NuGetFileEntry> SubtreeEntries { get; } = [];

    /// <summary>
    /// Gets the package entries whose destinations end at this node.
    /// </summary>
    internal List<NuGetFileEntry> TerminalEntries { get; } = [];

    /// <summary>
    /// Gets or creates the child for a canonical path segment.
    /// </summary>
    /// <param name="segment">The canonical path segment.</param>
    /// <returns>The existing or newly created child node.</returns>
    internal ContainedPathTrieNode GetOrAddChild(string segment)
    {
        if (!_children.TryGetValue(segment, out var child))
        {
            child = new ContainedPathTrieNode(comparer);
            _children.Add(segment, child);
        }

        return child;
    }
}
