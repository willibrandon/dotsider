namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One method-body fold from an ILC size report (format 2.2+): the compiler emitted a single
/// body and pointed these identical methods at it, so only the original contributes size.
/// </summary>
/// <param name="Name">The original method's display name, including its declaring type.</param>
/// <param name="TargetNames">The dependency-graph node names of the methods folded into the original.</param>
public sealed record MstatDeduplicatedMethod(
    string Name,
    IReadOnlyList<string> TargetNames);
