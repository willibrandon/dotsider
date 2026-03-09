using System.Reflection.PortableExecutable;

namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Aggregated PE header information for a .NET assembly.
/// </summary>
/// <param name="Machine">The target machine architecture.</param>
/// <param name="Characteristics">COFF header characteristics flags.</param>
/// <param name="TimeDateStamp">The linker timestamp from the COFF header.</param>
/// <param name="Magic">PE magic number (PE32 or PE32+).</param>
/// <param name="MajorLinkerVersion">Major version of the linker that produced the image.</param>
/// <param name="MinorLinkerVersion">Minor version of the linker that produced the image.</param>
/// <param name="SizeOfCode">Total size of all code sections.</param>
/// <param name="EntryPointRva">RVA of the entry point function.</param>
/// <param name="ImageBase">Preferred base address of the image.</param>
/// <param name="SectionAlignment">Alignment of sections in memory.</param>
/// <param name="FileAlignment">Alignment of sections on disk.</param>
/// <param name="SizeOfImage">Total size of the image in memory.</param>
/// <param name="SizeOfHeaders">Combined size of all headers.</param>
/// <param name="Subsystem">The Windows subsystem required to run the image.</param>
/// <param name="DllCharacteristics">DLL characteristics flags (ASLR, DEP, etc.).</param>
/// <param name="NumberOfSections">Number of sections in the PE file.</param>
public sealed record PeHeaders(
    Machine Machine,
    Characteristics Characteristics,
    int TimeDateStamp,
    PEMagic Magic,
    byte MajorLinkerVersion,
    byte MinorLinkerVersion,
    int SizeOfCode,
    int EntryPointRva,
    ulong ImageBase,
    int SectionAlignment,
    int FileAlignment,
    int SizeOfImage,
    int SizeOfHeaders,
    Subsystem Subsystem,
    DllCharacteristics DllCharacteristics,
    int NumberOfSections);
