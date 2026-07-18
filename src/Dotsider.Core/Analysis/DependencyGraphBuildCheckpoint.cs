namespace Dotsider.Core.Analysis;

/// <summary>
/// Identifies a completed unit of dependency-graph traversal work.
/// </summary>
internal enum DependencyGraphBuildCheckpoint
{
    /// <summary>A managed assembly reference was resolved and recorded in the graph.</summary>
    ManagedAssemblyReferenceProcessed,

    /// <summary>A Native AOT DGML link was inspected for assembly-level aggregation.</summary>
    DgmlLinkProcessed,
}
