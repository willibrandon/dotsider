namespace Dotsider.Views;

/// <summary>
/// The scope control on the Dep Graph tab that narrows the graph by dependency relationship.
/// Separate from the framework filter — scope decides which nodes are in the graph at all,
/// framework filter hides well-known framework assemblies within that scope. Transitive-only
/// is intentionally not an option: hiding direct parents would produce disconnected islands
/// and remove the explanation path that justifies why deeper nodes are in the closure.
/// </summary>
public enum DependencyGraphScope
{
    /// <summary>Show the full transitive closure. Default.</summary>
    All,

    /// <summary>Show only the root and its depth-1 references plus the edges between them.</summary>
    DirectOnly,
}
