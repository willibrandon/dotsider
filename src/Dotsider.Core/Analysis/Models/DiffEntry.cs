namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A single diff entry wrapping an item from either side.
/// </summary>
public sealed record DiffEntry<T>(
    DiffKind Kind,
    T? Left,
    T? Right,
    string? ChangeDescription);
