using Dotsider.Core.Analysis.Models;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads an ILC size report (<c>.mstat</c>), the file <c>IlcGenerateMstatFile</c> emits when
/// publishing a Native AOT project. The report is itself a valid ECMA-335 assembly: its
/// assembly version carries the format version, and its data is encoded as IL instruction
/// streams in global methods named <c>Methods</c>, <c>Types</c>, <c>Blobs</c>, and (in newer
/// formats) <c>RvaFields</c>, <c>FrozenObjects</c>, <c>ManifestResources</c>, and
/// <c>DeduplicatedMethods</c>. Format 2.0+ also stores each entry's dependency-graph node name
/// in a custom <c>.names</c> PE section; those names equal the node labels in the DGML graphs
/// <c>IlcGenerateDgmlFile</c> emits, which is how sizes join to dependency chains.
///
/// Malformed input never throws: unreadable files return null, and damage within an IL stream,
/// including an impossible nested count, yields the entries parsed before the damage.
/// </summary>
public static class MstatReader
{
    private const int MinimumDeduplicatedTargetEncodingSize = 6;

    /// <summary>
    /// Reads an ILC size report from a file.
    /// </summary>
    /// <param name="filePath">The path of the <c>.mstat</c> file.</param>
    /// <returns>The decoded report, or null when the file is missing or is not an mstat.</returns>
    public static MstatData? Read(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return Read(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Cheaply tests whether a file looks like an ILC size report, without decoding any IL
    /// streams or node names: the PE must carry metadata, the assembly version must be a
    /// known format version with the writer's unset Build/Revision sentinels, and
    /// <c>&lt;Module&gt;</c> must declare both the <c>Methods</c> and <c>Types</c> global
    /// methods — every format version emits both, and requiring the pair keeps an ordinary
    /// managed module that happens to define one such global method from being misclassified.
    /// A positive probe is a sniff, not a guarantee — follow it with <see cref="Read(string)"/>
    /// for the decoded report. Never throws.
    /// </summary>
    /// <param name="filePath">The path of the candidate file.</param>
    /// <returns>True when the file plausibly is an mstat; false otherwise.</returns>
    public static bool Probe(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return Probe(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Cheaply tests whether a stream looks like an ILC size report. The stream is left open.
    /// See <see cref="Probe(string)"/> for what the probe checks.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned at the start of the file.</param>
    /// <returns>True when the content plausibly is an mstat; false otherwise.</returns>
    public static bool Probe(Stream stream)
    {
        try
        {
            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            if (!pe.HasMetadata) return false;

            var mr = pe.GetMetadataReader();
            var version = mr.GetAssemblyDefinition().Version;
            if (version.Major is not (1 or 2)) return false;
            // The writer leaves Build/Revision unset, which reads back as the 65535 sentinel;
            // a real assembly versioned 1.x/2.x almost never does.
            if (version.Build != 65535 || version.Revision != 65535) return false;
            if (mr.TypeDefinitions.Count == 0) return false;

            var hasMethods = false;
            var hasTypes = false;
            var moduleType = mr.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1));
            foreach (var handle in moduleType.GetMethods())
            {
                var name = mr.GetString(mr.GetMethodDefinition(handle).Name);
                if (name == "Methods") hasMethods = true;
                else if (name == "Types") hasTypes = true;
                if (hasMethods && hasTypes) return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads an ILC size report from a stream. The stream is left open.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned at the start of the file.</param>
    /// <returns>The decoded report, or null when the content is not an mstat.</returns>
    public static MstatData? Read(Stream stream)
    {
        try
        {
            using var pe = new PEReader(stream, PEStreamOptions.PrefetchEntireImage | PEStreamOptions.LeaveOpen);
            if (!pe.HasMetadata) return null;

            var mr = pe.GetMetadataReader();
            var version = mr.GetAssemblyDefinition().Version;
            // Build/Revision are unset sentinels (65535); only Major.Minor carry the format.
            if (version.Major is not (1 or 2)) return null;

            var streams = FindGlobalMethodStreams(pe, mr);
            if (streams.Count == 0) return null;

            var resolver = new EntityResolver(mr);
            var names = FindNamesSection(pe);
            var hasNames = version.Major >= 2;

            return new MstatData(
                version.Major,
                version.Minor,
                ReadAssemblyRefs(mr),
                ReadMethods(streams, resolver, names, hasNames),
                ReadTypes(streams, resolver, names, hasNames),
                ReadBlobs(streams, mr),
                ReadRvaFields(streams, resolver, names),
                ReadFrozenObjects(streams, resolver, names),
                ReadManifestResources(streams, mr),
                ReadDeduplicatedMethods(streams, resolver, names));
        }
        catch
        {
            // Not a PE, no metadata, or damaged beyond the lenient per-stream recovery.
            return null;
        }
    }

    /// <summary>
    /// Collects the IL of the report's global methods — the methods on <c>&lt;Module&gt;</c>
    /// (always TypeDef row 1) whose names identify data streams.
    /// </summary>
    private static Dictionary<string, byte[]> FindGlobalMethodStreams(PEReader pe, MetadataReader mr)
    {
        var streams = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (mr.TypeDefinitions.Count == 0) return streams;

        var moduleType = mr.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(1));
        foreach (var handle in moduleType.GetMethods())
        {
            var method = mr.GetMethodDefinition(handle);
            var name = mr.GetString(method.Name);
            if (name is not ("Methods" or "Types" or "Blobs" or "RvaFields" or "FrozenObjects"
                or "ManifestResources" or "DeduplicatedMethods"))
            {
                continue;
            }

            if (method.RelativeVirtualAddress == 0) continue;
            var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
            if (il is not null) streams[name] = il;
        }

        return streams;
    }

    /// <summary>
    /// Locates the custom <c>.names</c> PE section that holds length-prefixed UTF-8 node
    /// names in format 2.0+, or null when the section is absent.
    /// </summary>
    private static PEMemoryBlock? FindNamesSection(PEReader pe)
    {
        foreach (var section in pe.PEHeaders.SectionHeaders)
        {
            if (section.Name == ".names")
                return pe.GetSectionData(section.VirtualAddress);
        }

        return null;
    }

    /// <summary>
    /// Reads the node name at a byte offset into the <c>.names</c> section, or null when the
    /// offset is out of range or the section is absent.
    /// </summary>
    private static string? ReadName(PEMemoryBlock? names, int offset)
    {
        if (names is not { } block || offset < 0 || offset >= block.Length) return null;
        try
        {
            var reader = block.GetReader();
            reader.Offset = offset;
            return reader.ReadSerializedString();
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static List<AssemblyRefInfo> ReadAssemblyRefs(MetadataReader mr)
    {
        var result = new List<AssemblyRefInfo>();
        foreach (var handle in mr.AssemblyReferences)
        {
            var ar = mr.GetAssemblyReference(handle);
            var culture = mr.GetString(ar.Culture);
            if (string.IsNullOrEmpty(culture)) culture = "neutral";

            var pkt = mr.GetBlobBytes(ar.PublicKeyOrToken);
            result.Add(new AssemblyRefInfo(
                mr.GetString(ar.Name),
                ar.Version.ToString(),
                culture,
                pkt.Length > 0 ? Convert.ToHexStringLower(pkt) : null));
        }

        return result;
    }

    private static List<MstatMethod> ReadMethods(
        Dictionary<string, byte[]> streams, EntityResolver resolver,
        PEMemoryBlock? names, bool hasNames)
    {
        var result = new List<MstatMethod>();
        if (!streams.TryGetValue("Methods", out var il)) return result;

        var cursor = new IlCursor(il);
        var nameOffset = 0;
        while (cursor.TryReadToken(out var token)
            && cursor.TryReadInt(out var size)
            && cursor.TryReadInt(out var gcInfoSize)
            && cursor.TryReadInt(out var ehInfoSize)
            && (!hasNames || cursor.TryReadInt(out nameOffset)))
        {
            var method = resolver.ResolveMethod(token);
            result.Add(new MstatMethod(
                method.Name, method.Type.Display, method.Type.Namespace, method.Type.AssemblyName,
                size, gcInfoSize, ehInfoSize,
                hasNames ? ReadName(names, nameOffset) : null,
                method.Signature));
        }

        return result;
    }

    private static List<MstatType> ReadTypes(
        Dictionary<string, byte[]> streams, EntityResolver resolver,
        PEMemoryBlock? names, bool hasNames)
    {
        var result = new List<MstatType>();
        if (!streams.TryGetValue("Types", out var il)) return result;

        var cursor = new IlCursor(il);
        var nameOffset = 0;
        while (cursor.TryReadToken(out var token)
            && cursor.TryReadInt(out var size)
            && (!hasNames || cursor.TryReadInt(out nameOffset)))
        {
            var type = resolver.ResolveType(token);
            result.Add(new MstatType(
                type.Display, type.Namespace, type.AssemblyName, size,
                hasNames ? ReadName(names, nameOffset) : null));
        }

        return result;
    }

    private static List<MstatBlob> ReadBlobs(Dictionary<string, byte[]> streams, MetadataReader mr)
    {
        var result = new List<MstatBlob>();
        if (!streams.TryGetValue("Blobs", out var il)) return result;

        var cursor = new IlCursor(il);
        while (cursor.TryReadUserString(mr, out var name) && cursor.TryReadInt(out var size))
            result.Add(new MstatBlob(name, size));

        return result;
    }

    private static List<MstatRvaField> ReadRvaFields(
        Dictionary<string, byte[]> streams, EntityResolver resolver, PEMemoryBlock? names)
    {
        var result = new List<MstatRvaField>();
        if (!streams.TryGetValue("RvaFields", out var il)) return result;

        var cursor = new IlCursor(il);
        while (cursor.TryReadToken(out var token)
            && cursor.TryReadInt(out var size)
            && cursor.TryReadInt(out var nameOffset))
        {
            var field = resolver.ResolveMethod(token); // MemberRef: same shape as a method ref
            result.Add(new MstatRvaField(
                $"{field.Type.Display}::{field.Name}", field.Type.AssemblyName, size,
                ReadName(names, nameOffset), field.Type.Namespace));
        }

        return result;
    }

    private static List<MstatFrozenObject> ReadFrozenObjects(
        Dictionary<string, byte[]> streams, EntityResolver resolver, PEMemoryBlock? names)
    {
        var result = new List<MstatFrozenObject>();
        if (!streams.TryGetValue("FrozenObjects", out var il)) return result;

        var cursor = new IlCursor(il);
        while (cursor.TryReadToken(out var token)
            && cursor.TryReadInt(out var size)
            && cursor.TryReadInt(out var nameOffset))
        {
            // The fourth element is always present: an owning-type token for serialized
            // statics, or a zero constant for everything else (string literals).
            string? owningType = null;
            string? owningAssembly = null;
            string? owningNamespace = null;
            if (cursor.TryReadToken(out var ownerToken))
            {
                var owner = resolver.ResolveType(ownerToken);
                owningType = owner.Display;
                owningAssembly = owner.AssemblyName;
                owningNamespace = owner.Namespace;
            }
            else if (!cursor.TryReadInt(out _))
            {
                break;
            }

            var type = resolver.ResolveType(token);
            result.Add(new MstatFrozenObject(
                type.Display, type.AssemblyName, size, ReadName(names, nameOffset),
                owningType, owningAssembly, owningNamespace));
        }

        return result;
    }

    private static List<MstatManifestResource> ReadManifestResources(
        Dictionary<string, byte[]> streams, MetadataReader mr)
    {
        var result = new List<MstatManifestResource>();
        if (!streams.TryGetValue("ManifestResources", out var il)) return result;

        var cursor = new IlCursor(il);
        while (cursor.TryReadInt(out var assemblyToken)
            && cursor.TryReadUserString(mr, out var name)
            && cursor.TryReadInt(out var size))
        {
            var assembly = "";
            var handle = MetadataTokens.EntityHandle(assemblyToken);
            if (handle.Kind == HandleKind.AssemblyReference && !handle.IsNil)
                assembly = mr.GetString(mr.GetAssemblyReference((AssemblyReferenceHandle)handle).Name);

            result.Add(new MstatManifestResource(assembly, name, size));
        }

        return result;
    }

    private static List<MstatDeduplicatedMethod> ReadDeduplicatedMethods(
        Dictionary<string, byte[]> streams, EntityResolver resolver, PEMemoryBlock? names)
    {
        var result = new List<MstatDeduplicatedMethod>();
        if (!streams.TryGetValue("DeduplicatedMethods", out var il)) return result;

        var cursor = new IlCursor(il);
        while (cursor.TryReadToken(out var token) && cursor.TryReadInt(out var count))
        {
            // Each target is an ldtoken instruction (five bytes) followed by an ldc.i4
            // instruction (at least one byte). A larger count cannot fit in the remaining
            // stream and must not influence an allocation or traversal.
            if (count < 0
                || count > cursor.RemainingByteCount / MinimumDeduplicatedTargetEncodingSize)
            {
                return result;
            }

            var method = resolver.ResolveMethod(token);
            var targets = new List<string>();
            for (var i = 0; i < count; i++)
            {
                if (!cursor.TryReadToken(out _) || !cursor.TryReadInt(out var nameOffset))
                    return result;
                if (ReadName(names, nameOffset) is { } target) targets.Add(target);
            }

            result.Add(new MstatDeduplicatedMethod($"{method.Type.Display}::{method.Name}", targets));
        }

        return result;
    }
}
