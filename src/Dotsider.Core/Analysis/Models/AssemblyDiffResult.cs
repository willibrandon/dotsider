namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The complete diff result between two assemblies.
/// </summary>
/// <param name="TypeDiffs">Diff entries for type definitions.</param>
/// <param name="MethodDiffs">Diff entries for method definitions.</param>
/// <param name="AssemblyRefDiffs">Diff entries for assembly references.</param>
/// <param name="MetadataSummary">Aggregate counts of added, removed, and changed items.</param>
public sealed record AssemblyDiffResult(
    IReadOnlyList<DiffEntry<TypeDefInfo>> TypeDiffs,
    IReadOnlyList<DiffEntry<MethodDefInfo>> MethodDiffs,
    IReadOnlyList<DiffEntry<AssemblyRefInfo>> AssemblyRefDiffs,
    DiffSummary MetadataSummary);
