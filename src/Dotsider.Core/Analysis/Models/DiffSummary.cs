namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Summary statistics for the diff.
/// </summary>
public sealed record DiffSummary(
    int TypesAdded, int TypesRemoved, int TypesChanged,
    int MethodsAdded, int MethodsRemoved, int MethodsChanged,
    int RefsAdded, int RefsRemoved, int RefsChanged,
    long SizeDelta);
