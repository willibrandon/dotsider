namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One WebAssembly global declaration.
/// </summary>
/// <param name="Index">The zero-based global index.</param>
/// <param name="ValueType">The raw WebAssembly value-type byte.</param>
/// <param name="ValueTypeName">The display name for the value type.</param>
/// <param name="IsMutable">Whether the global is mutable.</param>
public sealed record WasmGlobalInfo(
    int Index,
    byte ValueType,
    string ValueTypeName,
    bool IsMutable);
