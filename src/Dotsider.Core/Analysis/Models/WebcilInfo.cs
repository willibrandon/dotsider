namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Parsed provenance for a Webcil managed assembly, including whether it was wrapped inside
/// a WebAssembly module. Webcil is a .NET metadata container used by browser-wasm publishes,
/// so dotsider routes it through the managed metadata and IL experience.
/// </summary>
/// <param name="VersionMajor">The Webcil major format version.</param>
/// <param name="VersionMinor">The Webcil minor format version.</param>
/// <param name="IsWasmWrapped">True when the Webcil payload was found inside a Wasm wrapper module.</param>
/// <param name="PayloadOffset">The file offset of the Webcil payload in the opened file.</param>
/// <param name="SectionCount">The number of Webcil section records.</param>
/// <param name="MetadataSize">The size of the ECMA-335 metadata blob.</param>
/// <param name="DebugDirectorySize">The size of the Webcil debug directory, when present.</param>
public sealed record WebcilInfo(
    int VersionMajor,
    int VersionMinor,
    bool IsWasmWrapped,
    long PayloadOffset,
    int SectionCount,
    int MetadataSize,
    int DebugDirectorySize);
