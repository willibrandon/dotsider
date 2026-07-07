using System.Reflection.PortableExecutable;

namespace Dotsider.Core.Analysis.Wasm;

/// <summary>
/// Describes one debug-directory entry from a Webcil image.
/// The values are stored in Webcil-relative form and translated by the reader when displayed.
/// Dotsider uses these entries to recover portable-PDB and Source Link data from Webcil assemblies.
/// </summary>
/// <param name="Stamp">The debug directory timestamp or content id value.</param>
/// <param name="MajorVersion">The debug entry major version.</param>
/// <param name="MinorVersion">The debug entry minor version.</param>
/// <param name="Type">The debug directory entry type.</param>
/// <param name="DataSize">The payload size in bytes.</param>
/// <param name="DataRva">The payload RVA recorded by the Webcil debug entry.</param>
/// <param name="DataPointer">The Webcil-relative payload pointer.</param>
internal readonly record struct WebcilDebugEntry(
    uint Stamp,
    ushort MajorVersion,
    ushort MinorVersion,
    DebugDirectoryEntryType Type,
    int DataSize,
    int DataRva,
    int DataPointer);
