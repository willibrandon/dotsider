namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Summary statistics for the diff.
/// </summary>
/// <param name="TypesAdded">Number of types present only in the right assembly.</param>
/// <param name="TypesRemoved">Number of types present only in the left assembly.</param>
/// <param name="TypesChanged">Number of types that differ between assemblies.</param>
/// <param name="MethodsAdded">Number of methods present only in the right assembly.</param>
/// <param name="MethodsRemoved">Number of methods present only in the left assembly.</param>
/// <param name="MethodsChanged">Number of methods that differ between assemblies.</param>
/// <param name="RefsAdded">Number of assembly references present only in the right assembly.</param>
/// <param name="RefsRemoved">Number of assembly references present only in the left assembly.</param>
/// <param name="RefsChanged">Number of assembly references that differ between assemblies.</param>
/// <param name="SizeDelta">File size difference in bytes (positive means the right assembly is larger).</param>
public sealed record DiffSummary(
    int TypesAdded, int TypesRemoved, int TypesChanged,
    int MethodsAdded, int MethodsRemoved, int MethodsChanged,
    int RefsAdded, int RefsRemoved, int RefsChanged,
    long SizeDelta);
