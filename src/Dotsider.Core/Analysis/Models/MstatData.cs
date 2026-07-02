namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The contents of an ILC size report (<c>.mstat</c>), produced by publishing a Native AOT
/// project with <c>IlcGenerateMstatFile</c>. The file is itself a valid ECMA-335 assembly whose
/// assembly version carries the format version and whose data lives in IL streams; this record
/// is the decoded result. Sections absent from older format versions are empty lists.
/// </summary>
/// <param name="FormatMajorVersion">The format major version (1 = .NET 7, 2 = .NET 8+).</param>
/// <param name="FormatMinorVersion">The format minor version (2.1 adds RVA field, frozen object, and resource detail; 2.2 adds deduplicated methods).</param>
/// <param name="Assemblies">The managed assemblies the report references, in AssemblyRef table order.</param>
/// <param name="Methods">Every compiled method body with its code, GC info, and EH info sizes.</param>
/// <param name="Types">Every constructed MethodTable with its size.</param>
/// <param name="Blobs">Global data regions (metadata, dehydrated data, hydration tables) by name.</param>
/// <param name="RvaFields">Field RVA data entries (format 2.1+); their bytes also appear in <paramref name="Blobs"/> for back-compat.</param>
/// <param name="FrozenObjects">Frozen object entries (format 2.1+); their bytes also appear in <paramref name="Blobs"/> for back-compat.</param>
/// <param name="ManifestResources">Embedded manifest resources (format 2.1+); their bytes also appear in <paramref name="Blobs"/> for back-compat.</param>
/// <param name="DeduplicatedMethods">Method bodies folded into an identical original (format 2.2+).</param>
public sealed record MstatData(
    int FormatMajorVersion,
    int FormatMinorVersion,
    IReadOnlyList<AssemblyRefInfo> Assemblies,
    IReadOnlyList<MstatMethod> Methods,
    IReadOnlyList<MstatType> Types,
    IReadOnlyList<MstatBlob> Blobs,
    IReadOnlyList<MstatRvaField> RvaFields,
    IReadOnlyList<MstatFrozenObject> FrozenObjects,
    IReadOnlyList<MstatManifestResource> ManifestResources,
    IReadOnlyList<MstatDeduplicatedMethod> DeduplicatedMethods);
