namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One normalized entry of an <see cref="Dotsider.Core.Analysis.MstatSizeIndex"/>: raw report
/// rows aggregated under a build-stable identity key, with the structured hierarchy fields a
/// consumer needs to place the entry in an assembly → namespace → type → leaf tree without
/// parsing display strings.
/// </summary>
/// <param name="Section">The report section the entry came from.</param>
/// <param name="Key">
/// The build-stable identity key the entry's rows were aggregated under. Keys are comparable
/// across two builds of the same application; they are not display strings.
/// </param>
/// <param name="AssemblyName">
/// The assembly the bytes are attributed to. For frozen objects this is the owning type's
/// assembly — the code that caused the bytes — or
/// <see cref="Dotsider.Core.Analysis.MstatSizeIndex.UnattributedName"/> when the object has no
/// owner (string literals). Empty for global sections (blobs).
/// </param>
/// <param name="Namespace">
/// The namespace the bytes are attributed to, an empty string for the global namespace, or
/// <see cref="Dotsider.Core.Analysis.MstatSizeIndex.UnattributedName"/> for ownerless frozen
/// objects. Blobs and resources carry no namespace.
/// </param>
/// <param name="TypeName">The type-level grouping name (declaring type for methods, the type itself for MethodTables, the owning type for owned frozen objects), or an empty string for sections without one.</param>
/// <param name="LeafName">The leaf display name, disambiguated where identity requires it (method names carry their parameter list).</param>
/// <param name="DisplayName">The undecorated display name (a method's bare name, a blob's name).</param>
/// <param name="FullPath">A deterministic, key-derived path for the entry, unique within the index.</param>
/// <param name="Size">The aggregated size in bytes.</param>
/// <param name="EntryCount">
/// The number of raw report rows folded into this entry. Greater than one means the entry is
/// an aggregate (overload display collisions, folded MethodTables, frozen objects grouped by
/// owner) and consumers must present it as such.
/// </param>
/// <param name="NodeNames">
/// Every dependency-graph node name behind the aggregated rows, in row order. These join to
/// DGML node labels and to native symbol names; an aggregate maps to as many nodes as it has
/// rows with names.
/// </param>
public sealed record MstatSizeEntry(
    MstatSectionKind Section,
    string Key,
    string AssemblyName,
    string Namespace,
    string TypeName,
    string LeafName,
    string DisplayName,
    string FullPath,
    long Size,
    int EntryCount,
    IReadOnlyList<string> NodeNames);
