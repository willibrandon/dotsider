using Dotsider.Core.Analysis.Models;

namespace Dotsider.Views;

/// <summary>
/// The laid-out render model for the Dep Graph view. Produced by
/// <see cref="DependencyGraphView.BuildRenderLayout"/> from a <see cref="VisibleGraphModel"/>
/// plus the current viewport size, and cached by invalidation key so repeated renders in
/// the same frame or across mouse moves reuse the same geometry.
/// </summary>
/// <param name="Nodes">Placed render nodes in draw order.</param>
/// <param name="Edges">Edges connecting placed nodes.</param>
/// <param name="IndexById">Map from <see cref="GraphNode.Id"/> to index into <see cref="Nodes"/>.</param>
/// <param name="ContentHeight">
/// Total vertical extent of the laid-out graph in character rows — the Y just past the bottom
/// of the lowest box. When this exceeds the viewport height, the view enables vertical
/// scrolling; when it is less than or equal to the viewport height, the layout already
/// redistributes nodes through the available height.
/// </param>
internal sealed record GraphRenderLayout(
    IReadOnlyList<GraphRenderNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyDictionary<string, int> IndexById,
    int ContentHeight);
