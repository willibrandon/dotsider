namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A PDB local variable slot and the IL range where its name is active.
/// </summary>
/// <param name="Slot">The local variable slot index.</param>
/// <param name="Name">The local variable name.</param>
/// <param name="StartOffset">The first IL offset where the name is active.</param>
/// <param name="EndOffset">The exclusive end IL offset for the local scope.</param>
/// <param name="IsDebuggerHidden">Whether the local is marked debugger-hidden.</param>
public sealed record LocalSlotInfo(
    int Slot,
    string Name,
    int StartOffset,
    int EndOffset,
    bool IsDebuggerHidden);
