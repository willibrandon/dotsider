using Dotsider.Core.Analysis.Models;
using System.Collections.Immutable;
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
/// Malformed input never throws: unreadable files return null, and a truncated IL stream
/// yields the entries parsed before the damage.
/// </summary>
public static class MstatReader
{
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
            var method = resolver.ResolveMethod(token);
            var targets = new List<string>(count);
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

    /// <summary>
    /// A forward-only reader over an mstat IL stream. Each Try method consumes its element or
    /// returns false without advancing, so a truncated or unexpected stream ends the walk with
    /// every fully-parsed entry retained.
    /// </summary>
    private struct IlCursor(byte[] il)
    {
        private readonly byte[] _il = il;
        private int _pos;

        /// <summary>Reads any ldc.i4 form.</summary>
        public bool TryReadInt(out int value)
        {
            value = 0;
            if (_pos >= _il.Length) return false;

            switch (_il[_pos])
            {
                case 0x15: // ldc.i4.m1
                    value = -1;
                    _pos += 1;
                    return true;
                case >= 0x16 and <= 0x1E: // ldc.i4.0 .. ldc.i4.8
                    value = _il[_pos] - 0x16;
                    _pos += 1;
                    return true;
                case 0x1F when _pos + 1 < _il.Length: // ldc.i4.s
                    value = (sbyte)_il[_pos + 1];
                    _pos += 2;
                    return true;
                case 0x20 when _pos + 4 < _il.Length: // ldc.i4
                    value = BitConverter.ToInt32(_il, _pos + 1);
                    _pos += 5;
                    return true;
                default:
                    return false;
            }
        }

        public bool TryReadToken(out int token)
        {
            token = 0;
            if (_pos + 4 >= _il.Length || _il[_pos] != 0xD0) return false; // ldtoken
            token = BitConverter.ToInt32(_il, _pos + 1);
            _pos += 5;
            return true;
        }

        public bool TryReadUserString(MetadataReader mr, out string value)
        {
            value = "";
            if (_pos + 4 >= _il.Length || _il[_pos] != 0x72) return false; // ldstr
            var token = BitConverter.ToInt32(_il, _pos + 1);
            _pos += 5;
            value = mr.GetUserString(MetadataTokens.UserStringHandle(token));
            return true;
        }
    }

    /// <summary>The display name, namespace, and defining assembly of a resolved type.</summary>
    private readonly record struct TypeAttribution(string Display, string Namespace, string AssemblyName)
    {
        public static readonly TypeAttribution Unknown = new("?", "", "");
    }

    /// <summary>
    /// A resolved method or field reference: its name, its declaring type, and — for methods —
    /// the rendered parameter-type list that keeps overloads apart.
    /// </summary>
    private readonly record struct MemberAttribution(string Name, TypeAttribution Type, string Signature = "");

    /// <summary>
    /// Resolves mstat entity tokens to display names with assembly attribution. Types resolve
    /// through TypeRef chains (nested types walk to the outermost scope's AssemblyRef) or
    /// TypeSpec signatures, where a constructed type attributes to its outermost named type —
    /// <c>List&lt;MyType&gt;</c> belongs to the collections assembly no matter what it holds.
    /// </summary>
    private sealed class EntityResolver(MetadataReader mr) : ISignatureTypeProvider<TypeAttribution, object?>
    {
        private readonly MetadataReader _mr = mr;

        public MemberAttribution ResolveMethod(int token)
        {
            var handle = MetadataTokens.EntityHandle(token);
            try
            {
                switch (handle.Kind)
                {
                    case HandleKind.MemberReference:
                        return ResolveMemberRef((MemberReferenceHandle)handle);

                    case HandleKind.MethodSpecification:
                        var spec = _mr.GetMethodSpecification((MethodSpecificationHandle)handle);
                        if (spec.Method.Kind != HandleKind.MemberReference)
                            return new MemberAttribution("?", TypeAttribution.Unknown);
                        var member = ResolveMemberRef((MemberReferenceHandle)spec.Method);
                        var args = spec.DecodeSignature(this, null);
                        return member with { Name = $"{member.Name}<{Join(args)}>" };

                    default:
                        return new MemberAttribution("?", TypeAttribution.Unknown);
                }
            }
            catch (BadImageFormatException)
            {
                return new MemberAttribution("?", TypeAttribution.Unknown);
            }
        }

        public TypeAttribution ResolveType(int token)
        {
            var handle = MetadataTokens.EntityHandle(token);
            try
            {
                return handle.Kind switch
                {
                    HandleKind.TypeReference => ResolveTypeRef((TypeReferenceHandle)handle),
                    HandleKind.TypeSpecification =>
                        _mr.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(this, null),
                    _ => TypeAttribution.Unknown,
                };
            }
            catch (BadImageFormatException)
            {
                return TypeAttribution.Unknown;
            }
        }

        private MemberAttribution ResolveMemberRef(MemberReferenceHandle handle)
        {
            var member = _mr.GetMemberReference(handle);
            var type = member.Parent.Kind switch
            {
                HandleKind.TypeReference => ResolveTypeRef((TypeReferenceHandle)member.Parent),
                HandleKind.TypeSpecification =>
                    _mr.GetTypeSpecification((TypeSpecificationHandle)member.Parent).DecodeSignature(this, null),
                _ => TypeAttribution.Unknown,
            };

            // Decode the method's parameter types so overloads stay distinct: two builds of the
            // same source always render the same signature, so it extends the stable identity.
            // Field references (RVA field entries route through here too) carry field
            // signatures, which have no parameter list.
            var signature = "";
            if (member.GetKind() == MemberReferenceKind.Method)
            {
                try
                {
                    var decoded = member.DecodeMethodSignature(this, null);
                    signature = $"({string.Join(", ", decoded.ParameterTypes.Select(p => p.Display))})";
                }
                catch (BadImageFormatException)
                {
                    // A damaged signature blob degrades to the name-only identity.
                }
            }

            return new MemberAttribution(_mr.GetString(member.Name), type, signature);
        }

        private TypeAttribution ResolveTypeRef(TypeReferenceHandle handle)
        {
            var tr = _mr.GetTypeReference(handle);
            var name = _mr.GetString(tr.Name);

            if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                // Nested type: namespace and assembly come from the outermost type.
                var outer = ResolveTypeRef((TypeReferenceHandle)tr.ResolutionScope);
                return outer with { Display = $"{outer.Display}/{name}" };
            }

            var ns = _mr.GetString(tr.Namespace);
            var assembly = tr.ResolutionScope.Kind == HandleKind.AssemblyReference
                ? _mr.GetString(_mr.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name)
                : "";
            return new TypeAttribution(ns.Length > 0 ? $"{ns}.{name}" : name, ns, assembly);
        }

        private static string Join(ImmutableArray<TypeAttribution> args) =>
            string.Join(", ", args.Select(a => a.Display));

        // ISignatureTypeProvider — display composition; attribution follows the named type.

        TypeAttribution ISimpleTypeProvider<TypeAttribution>.GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            var display = typeCode switch
            {
                PrimitiveTypeCode.Void => "void",
                PrimitiveTypeCode.Boolean => "bool",
                PrimitiveTypeCode.Char => "char",
                PrimitiveTypeCode.SByte => "sbyte",
                PrimitiveTypeCode.Byte => "byte",
                PrimitiveTypeCode.Int16 => "short",
                PrimitiveTypeCode.UInt16 => "ushort",
                PrimitiveTypeCode.Int32 => "int",
                PrimitiveTypeCode.UInt32 => "uint",
                PrimitiveTypeCode.Int64 => "long",
                PrimitiveTypeCode.UInt64 => "ulong",
                PrimitiveTypeCode.Single => "float",
                PrimitiveTypeCode.Double => "double",
                PrimitiveTypeCode.String => "string",
                PrimitiveTypeCode.Object => "object",
                PrimitiveTypeCode.IntPtr => "nint",
                PrimitiveTypeCode.UIntPtr => "nuint",
                PrimitiveTypeCode.TypedReference => "TypedReference",
                _ => typeCode.ToString(),
            };
            return new TypeAttribution(display, "System", "");
        }

        TypeAttribution ISimpleTypeProvider<TypeAttribution>.GetTypeFromDefinition(
            MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var td = reader.GetTypeDefinition(handle);
            var name = reader.GetString(td.Name);
            var ns = reader.GetString(td.Namespace);
            return new TypeAttribution(ns.Length > 0 ? $"{ns}.{name}" : name, ns, "");
        }

        TypeAttribution ISimpleTypeProvider<TypeAttribution>.GetTypeFromReference(
            MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
            ResolveTypeRef(handle);

        TypeAttribution ISignatureTypeProvider<TypeAttribution, object?>.GetTypeFromSpecification(
            MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        TypeAttribution IConstructedTypeProvider<TypeAttribution>.GetGenericInstantiation(
            TypeAttribution genericType, ImmutableArray<TypeAttribution> typeArguments) =>
            genericType with { Display = $"{genericType.Display}<{Join(typeArguments)}>" };

        TypeAttribution ISZArrayTypeProvider<TypeAttribution>.GetSZArrayType(TypeAttribution elementType) =>
            elementType with { Display = $"{elementType.Display}[]" };

        TypeAttribution IConstructedTypeProvider<TypeAttribution>.GetArrayType(
            TypeAttribution elementType, ArrayShape shape) =>
            elementType with { Display = $"{elementType.Display}[{new string(',', shape.Rank - 1)}]" };

        TypeAttribution IConstructedTypeProvider<TypeAttribution>.GetByReferenceType(TypeAttribution elementType) =>
            elementType with { Display = $"ref {elementType.Display}" };

        TypeAttribution IConstructedTypeProvider<TypeAttribution>.GetPointerType(TypeAttribution elementType) =>
            elementType with { Display = $"{elementType.Display}*" };

        TypeAttribution ISignatureTypeProvider<TypeAttribution, object?>.GetGenericMethodParameter(
            object? genericContext, int index) => new($"!!{index}", "", "");

        TypeAttribution ISignatureTypeProvider<TypeAttribution, object?>.GetGenericTypeParameter(
            object? genericContext, int index) => new($"!{index}", "", "");

        TypeAttribution ISignatureTypeProvider<TypeAttribution, object?>.GetModifiedType(
            TypeAttribution modifier, TypeAttribution unmodifiedType, bool isRequired) => unmodifiedType;

        TypeAttribution ISignatureTypeProvider<TypeAttribution, object?>.GetPinnedType(
            TypeAttribution elementType) => elementType;

        TypeAttribution ISignatureTypeProvider<TypeAttribution, object?>.GetFunctionPointerType(
            MethodSignature<TypeAttribution> signature) => new("fnptr", "", "");
    }
}
