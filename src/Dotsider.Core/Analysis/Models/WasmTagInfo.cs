namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One WebAssembly exception tag declaration.
/// </summary>
/// <param name="Index">The zero-based tag index.</param>
/// <param name="Attribute">The tag attribute value.</param>
/// <param name="TypeIndex">The function type index used by the tag.</param>
public sealed record WasmTagInfo(
    int Index,
    uint Attribute,
    int TypeIndex);
