namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The external item kind used by WebAssembly import and export sections.
/// </summary>
public enum WasmExternalKind
{
    /// <summary>A function index.</summary>
    Function,

    /// <summary>A table index.</summary>
    Table,

    /// <summary>A memory index.</summary>
    Memory,

    /// <summary>A global index.</summary>
    Global,

    /// <summary>An exception tag index.</summary>
    Tag,

    /// <summary>An unrecognized external kind.</summary>
    Unknown
}
