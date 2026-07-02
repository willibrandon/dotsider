namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One entry in a Native AOT binary's ReadyToRun section table. Each section describes a
/// runtime data region — frozen objects, GC statics, dehydrated data, or a readonly blob
/// such as the embedded metadata — the way an ECMA-335 table describes a managed assembly.
/// </summary>
/// <param name="SectionId">The <c>ReadyToRunSectionType</c> id (e.g. 206 = FrozenObjectRegion).</param>
/// <param name="Name">A human-readable name for the section id.</param>
/// <param name="VirtualAddress">The section's absolute virtual address.</param>
/// <param name="Size">The section size in bytes, or 0 when the header does not record it.</param>
/// <param name="FileOffset">
/// The file offset the virtual address maps to, or null when the section exists only in
/// memory (for example an ELF NOBITS region that the runtime fills at startup).
/// </param>
public sealed record RtrSection(
    int SectionId,
    string Name,
    ulong VirtualAddress,
    long Size,
    int? FileOffset);
