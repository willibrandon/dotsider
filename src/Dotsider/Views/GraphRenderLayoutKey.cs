namespace Dotsider.Views;

/// <summary>
/// Cache key for a computed <see cref="GraphRenderLayout"/>. The layout must be recomputed
/// whenever any of these inputs changes: the cached graph identity (a new analyzer or build
/// replaces the nodes list), the active scope, the framework filter flag, or the viewport
/// width or height. Mouse moves do not invalidate — hover is resolved separately without
/// rebuilding geometry.
/// </summary>
/// <param name="NodesRef">
/// Identity handle for the cached graph's node list. Compared by reference, so a new build
/// replacing the list invalidates the layout even if it contains the same node ids.
/// </param>
/// <param name="Scope">The active scope control.</param>
/// <param name="HideFramework">Whether the framework filter is on.</param>
/// <param name="Width">Current surface width in columns.</param>
/// <param name="Height">Current surface height in rows.</param>
internal readonly record struct GraphRenderLayoutKey(
    object? NodesRef,
    DependencyGraphScope Scope,
    bool HideFramework,
    int Width,
    int Height);
