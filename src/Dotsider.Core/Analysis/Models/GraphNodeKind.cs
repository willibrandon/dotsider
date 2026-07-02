namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// What a dependency-graph node represents. Managed graphs contain only assemblies; the
/// Native AOT graph adds the binary's native import modules.
/// </summary>
public enum GraphNodeKind
{
    /// <summary>A managed assembly, identified by its full assembly identity.</summary>
    Assembly,

    /// <summary>A native module the binary imports (for example <c>kernel32.dll</c>).</summary>
    NativeImport
}
