namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One step of a root-to-node dependency chain — the answer to "why is this in my binary,"
/// read top-down: the root kept the second step, which kept the third, and so on to the node
/// that was asked about.
/// </summary>
/// <param name="Label">The node name at this step.</param>
/// <param name="Reason">Why the previous step depends on this one, or null on the root step.</param>
public sealed record DgmlPathStep(
    string Label,
    string? Reason);
