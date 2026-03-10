namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The complete diff result between two assemblies.
/// </summary>
public sealed record AssemblyDiffResult(
    IReadOnlyList<DiffEntry<TypeDefInfo>> TypeDiffs,
    IReadOnlyList<DiffEntry<MethodDefInfo>> MethodDiffs,
    IReadOnlyList<DiffEntry<AssemblyRefInfo>> AssemblyRefDiffs,
    DiffSummary MetadataSummary);
