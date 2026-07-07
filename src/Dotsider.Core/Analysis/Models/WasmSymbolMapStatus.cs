namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The outcome of probing a WebAssembly module's <c>dotnet.native.js.symbols</c> sidecar.
/// </summary>
public enum WasmSymbolMapStatus
{
    /// <summary>No sidecar was expected or found.</summary>
    Missing,

    /// <summary>The sidecar was found and parsed.</summary>
    Loaded,

    /// <summary>The sidecar was found but no valid entries could be read.</summary>
    Corrupt
}
