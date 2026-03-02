using System.Reflection.PortableExecutable;

namespace Dotsider.Analysis.Models;

/// <summary>
/// Information about a single PE section (e.g., .text, .rsrc, .reloc).
/// </summary>
/// <param name="Name">The section name (up to 8 characters).</param>
/// <param name="VirtualAddress">The RVA of the section when loaded into memory.</param>
/// <param name="VirtualSize">The size of the section in memory.</param>
/// <param name="RawDataOffset">The file offset of the section's raw data.</param>
/// <param name="RawDataSize">The size of the section's raw data on disk.</param>
/// <param name="Characteristics">Section characteristic flags (readable, writable, executable, etc.).</param>
public sealed record SectionInfo(
    string Name,
    int VirtualAddress,
    int VirtualSize,
    int RawDataOffset,
    int RawDataSize,
    SectionCharacteristics Characteristics);
