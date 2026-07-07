namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A run-length encoded local declaration inside a WebAssembly function body.
/// </summary>
/// <param name="Count">The number of locals in this run.</param>
/// <param name="ValueType">The raw Wasm value type byte.</param>
/// <param name="DisplayType">The display name of the value type.</param>
public sealed record WasmLocalInfo(uint Count, byte ValueType, string DisplayType);
