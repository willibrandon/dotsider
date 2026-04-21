using Dotsider.Core.Analysis.Models;

namespace Dotsider.Views;

/// <summary>
/// A placed render-time projection of a single <see cref="GraphNode"/>. Carries the
/// rendered label, the pre-computed box geometry in character coordinates, and the depth
/// within the visible rooted subgraph so filter changes produce a compact layout rather
/// than reusing stale positions from the full graph.
/// </summary>
/// <param name="Node">The underlying topology node.</param>
/// <param name="Label">The rendered label (with any <c>?</c> or <c>!</c> prefix).</param>
/// <param name="X">Left column of the box in character coordinates.</param>
/// <param name="Y">Top row of the box in character coordinates.</param>
/// <param name="Width">Box width in columns.</param>
/// <param name="Height">Box height in rows (always 3 in the current renderer).</param>
/// <param name="VisibleDepth">Depth from the root within the currently visible graph.</param>
internal sealed record GraphRenderNode(
    GraphNode Node,
    string Label,
    int X,
    int Y,
    int Width,
    int Height,
    int VisibleDepth);
