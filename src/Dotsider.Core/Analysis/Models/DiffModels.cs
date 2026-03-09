namespace Dotsider.Core.Analysis.Models;

public enum DiffKind { Added, Removed, Changed, Unchanged }

/// <summary>
/// A single diff entry wrapping an item from either side.
/// </summary>
public sealed record DiffEntry<T>(
    DiffKind Kind,
    T? Left,
    T? Right,
    string? ChangeDescription);

/// <summary>
/// The complete diff result between two assemblies.
/// </summary>
public sealed record AssemblyDiffResult(
    IReadOnlyList<DiffEntry<TypeDefInfo>> TypeDiffs,
    IReadOnlyList<DiffEntry<MethodDefInfo>> MethodDiffs,
    IReadOnlyList<DiffEntry<AssemblyRefInfo>> AssemblyRefDiffs,
    DiffSummary MetadataSummary);

/// <summary>
/// Summary statistics for the diff.
/// </summary>
public sealed record DiffSummary(
    int TypesAdded, int TypesRemoved, int TypesChanged,
    int MethodsAdded, int MethodsRemoved, int MethodsChanged,
    int RefsAdded, int RefsRemoved, int RefsChanged,
    long SizeDelta);
