namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One row of a crossgen2 image's <c>READYTORUN_SECTION</c> table: a section type and the
/// <c>{RVA, Size}</c> data directory that locates it. Rendered by the PE/Metadata "R2R Sections"
/// tab for ReadyToRun images.
/// </summary>
/// <param name="Type">The raw <c>ReadyToRunSectionType</c> id.</param>
/// <param name="Name">A human-readable name for the section type.</param>
/// <param name="Rva">The section's relative virtual address.</param>
/// <param name="Size">The section size in bytes.</param>
/// <param name="FileOffset">The file offset the RVA maps to, or null when it is not file-backed.</param>
public sealed record ReadyToRunSectionEntry(
    int Type,
    string Name,
    int Rva,
    int Size,
    int? FileOffset);
