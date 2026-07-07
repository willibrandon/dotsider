namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One WebAssembly function type from the type section.
/// </summary>
/// <param name="Index">The zero-based type index.</param>
/// <param name="ParamTypes">The raw WebAssembly parameter value-type bytes.</param>
/// <param name="ResultTypes">The raw WebAssembly result value-type bytes.</param>
public sealed record WasmTypeInfo(
    int Index,
    IReadOnlyList<byte> ParamTypes,
    IReadOnlyList<byte> ResultTypes);
