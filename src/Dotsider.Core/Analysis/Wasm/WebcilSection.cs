namespace Dotsider.Core.Analysis.Wasm;

/// <summary>
/// Represents one entry in a Webcil section table.
/// </summary>
/// <param name="VirtualSize">The section's virtual size.</param>
/// <param name="VirtualAddress">The section's starting RVA.</param>
/// <param name="SizeOfRawData">The number of file-backed bytes.</param>
/// <param name="PointerToRawData">The payload-relative offset of the file-backed bytes.</param>
internal readonly record struct WebcilSection(
    uint VirtualSize,
    uint VirtualAddress,
    uint SizeOfRawData,
    uint PointerToRawData);
