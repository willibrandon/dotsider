using Dotsider.Core.Analysis.Models;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Dotsider;

/// <summary>
/// An immutable dependency-graph publication containing topology and its matching navigation
/// metadata.
/// </summary>
internal sealed class DependencyGraphSnapshot
{
    /// <summary>
    /// Creates an immutable snapshot from the specified graph components.
    /// </summary>
    /// <param name="nodes">The graph nodes.</param>
    /// <param name="edges">The graph edges.</param>
    /// <param name="navigationById">Navigation metadata keyed by node id, if available.</param>
    internal DependencyGraphSnapshot(
        IEnumerable<GraphNode> nodes,
        IEnumerable<GraphEdge> edges,
        IEnumerable<KeyValuePair<string, GraphNavigationContext>>? navigationById)
    {
        Nodes = [.. nodes];
        Edges = [.. edges];
        NavigationById = navigationById?.ToFrozenDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
    }

    /// <summary>Gets the immutable graph nodes.</summary>
    internal ImmutableArray<GraphNode> Nodes { get; }

    /// <summary>Gets the immutable graph edges.</summary>
    internal ImmutableArray<GraphEdge> Edges { get; }

    /// <summary>Gets immutable navigation metadata keyed by node id, if available.</summary>
    internal FrozenDictionary<string, GraphNavigationContext>? NavigationById { get; }
}
