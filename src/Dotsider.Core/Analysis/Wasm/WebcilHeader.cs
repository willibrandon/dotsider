namespace Dotsider.Core.Analysis.Wasm;

/// <summary>
/// Represents the fixed fields in a version 0 or version 1 Webcil header.
/// </summary>
/// <param name="Id">The Webcil signature.</param>
/// <param name="VersionMajor">The format major version.</param>
/// <param name="VersionMinor">The format minor version.</param>
/// <param name="CoffSections">The number of section table entries.</param>
/// <param name="PeCliHeaderRva">The CLR header RVA.</param>
/// <param name="PeCliHeaderSize">The CLR header size.</param>
/// <param name="PeDebugRva">The debug directory RVA.</param>
/// <param name="PeDebugSize">The debug directory size.</param>
/// <param name="TableBase">The version 1 table base, or <see cref="uint.MaxValue"/> for version 0.</param>
internal readonly record struct WebcilHeader(
    uint Id,
    int VersionMajor,
    int VersionMinor,
    int CoffSections,
    uint PeCliHeaderRva,
    uint PeCliHeaderSize,
    uint PeDebugRva,
    uint PeDebugSize,
    uint TableBase);
