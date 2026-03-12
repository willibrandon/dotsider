namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A single diff entry wrapping an item from either side.
/// </summary>
/// <typeparam name="T">The type of item being compared (e.g., <see cref="TypeDefInfo"/>).</typeparam>
/// <param name="Kind">Whether the item was added, removed, or changed.</param>
/// <param name="Left">The item from the left (baseline) assembly, or <see langword="null"/> if added.</param>
/// <param name="Right">The item from the right (updated) assembly, or <see langword="null"/> if removed.</param>
/// <param name="ChangeDescription">A human-readable description of what changed, or <see langword="null"/>.</param>
public sealed record DiffEntry<T>(
    DiffKind Kind,
    T? Left,
    T? Right,
    string? ChangeDescription);
