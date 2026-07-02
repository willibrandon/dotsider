namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One node of an ILC dependency graph. The label is the compiler's node name — the same
/// string an mstat size entry stores as its <c>NodeName</c>, which is how the two files join.
/// </summary>
/// <param name="Id">The node id, unique within the graph.</param>
/// <param name="Label">The node name.</param>
public sealed record DgmlNode(
    int Id,
    string Label);
