namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One edge of an ILC dependency graph: the source node depends on the target node, so the
/// target is in the binary because the source needed it.
/// </summary>
/// <param name="SourceId">The depender's node id.</param>
/// <param name="TargetId">The dependee's node id.</param>
/// <param name="Reason">The compiler's explanation for the dependency, or null when it gave none.</param>
public sealed record DgmlLink(
    int SourceId,
    int TargetId,
    string? Reason);
