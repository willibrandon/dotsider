namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Identifies the type of file embedded in a .NET single-file bundle.
/// </summary>
public enum BundleFileType : byte
{
    /// <summary>Type not determined.</summary>
    Unknown = 0,

    /// <summary>IL and R2R assemblies.</summary>
    Assembly = 1,

    /// <summary>Native binaries.</summary>
    NativeBinary = 2,

    /// <summary>The .deps.json configuration file.</summary>
    DepsJson = 3,

    /// <summary>The .runtimeconfig.json configuration file.</summary>
    RuntimeConfigJson = 4,

    /// <summary>PDB symbol files.</summary>
    Symbols = 5
}
