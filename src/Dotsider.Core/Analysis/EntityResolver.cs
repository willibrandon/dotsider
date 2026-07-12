using Dotsider.Core.Analysis.Signatures;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves mstat entity tokens to display names with assembly attribution.
/// </summary>
internal sealed class EntityResolver : ISignatureTypeProvider<TypeAttribution, object?>
{
    private readonly MetadataReader _metadataReader;

    internal EntityResolver(MetadataReader metadataReader)
    {
        _metadataReader = metadataReader;
    }

    /// <summary>Resolves an mstat method or field token.</summary>
    /// <param name="token">The metadata token.</param>
    /// <returns>The resolved member attribution, or an unknown attribution.</returns>
    public MemberAttribution ResolveMethod(int token)
    {
        var handle = MetadataTokens.EntityHandle(token);
        try
        {
            switch (handle.Kind)
            {
                case HandleKind.MemberReference:
                    return ResolveMemberReference((MemberReferenceHandle)handle);

                case HandleKind.MethodSpecification:
                    var specification = _metadataReader.GetMethodSpecification(
                        (MethodSpecificationHandle)handle);
                    if (specification.Method.Kind != HandleKind.MemberReference)
                    {
                        return new MemberAttribution("?", TypeAttribution.Unknown);
                    }
                    var member = ResolveMemberReference((MemberReferenceHandle)specification.Method);
                    try
                    {
                        var arguments = SafeSignatureDecoder.DecodeMethodSpecificationSignature(
                            _metadataReader,
                            (MethodSpecificationHandle)handle,
                            this,
                            genericContext: null);
                        return member with { Name = $"{member.Name}<{Join(arguments)}>" };
                    }
                    catch (BadImageFormatException)
                    {
                        return member;
                    }

                default:
                    return new MemberAttribution("?", TypeAttribution.Unknown);
            }
        }
        catch (BadImageFormatException)
        {
            return new MemberAttribution("?", TypeAttribution.Unknown);
        }
    }

    /// <summary>Resolves an mstat type token.</summary>
    /// <param name="token">The metadata token.</param>
    /// <returns>The resolved type attribution, or an unknown attribution.</returns>
    public TypeAttribution ResolveType(int token)
    {
        var handle = MetadataTokens.EntityHandle(token);
        try
        {
            return handle.Kind switch
            {
                HandleKind.TypeReference => ResolveTypeReference((TypeReferenceHandle)handle),
                HandleKind.TypeSpecification => SafeSignatureDecoder.DecodeType(
                    _metadataReader, (TypeSpecificationHandle)handle, this, genericContext: null),
                _ => TypeAttribution.Unknown,
            };
        }
        catch (BadImageFormatException)
        {
            return TypeAttribution.Unknown;
        }
    }

    private MemberAttribution ResolveMemberReference(MemberReferenceHandle handle)
    {
        var member = _metadataReader.GetMemberReference(handle);
        var name = _metadataReader.GetString(member.Name);
        var type = member.Parent.Kind switch
        {
            HandleKind.TypeReference => ResolveTypeReference((TypeReferenceHandle)member.Parent),
            HandleKind.TypeSpecification => SafeSignatureDecoder.DecodeType(
                _metadataReader, (TypeSpecificationHandle)member.Parent, this, genericContext: null),
            _ => TypeAttribution.Unknown,
        };

        var signature = string.Empty;
        MemberReferenceKind kind;
        try
        {
            kind = member.GetKind();
        }
        catch (BadImageFormatException)
        {
            return new MemberAttribution(name, type);
        }

        if (kind == MemberReferenceKind.Method)
        {
            try
            {
                var decoded = SafeSignatureDecoder.DecodeMemberReferenceMethodSignature(
                    _metadataReader, handle, this, genericContext: null);
                signature = $"({string.Join(", ", decoded.ParameterTypes.Select(type => type.Display))})";
            }
            catch (BadImageFormatException)
            {
                // A damaged signature degrades to the name-only identity.
            }
        }

        return new MemberAttribution(name, type, signature);
    }

    private TypeAttribution ResolveTypeReference(TypeReferenceHandle handle)
    {
        var chain = MetadataNestingWalker.ResolutionScopeChain(_metadataReader, handle);
        if (!MetadataNestingWalker.TryFormatTypeReferenceName(
            chain, out var fullName, out var namespaceName))
        {
            return TypeAttribution.Unknown;
        }

        var assemblyName = string.Empty;
        if (chain.Terminal.Kind == HandleKind.AssemblyReference)
        {
            try
            {
                var assemblyReference = _metadataReader.GetAssemblyReference(
                    (AssemblyReferenceHandle)chain.Terminal);
                assemblyName = _metadataReader.GetString(assemblyReference.Name);
            }
            catch (BadImageFormatException)
            {
                return TypeAttribution.Unknown;
            }
        }

        return new TypeAttribution(fullName, namespaceName, assemblyName);
    }

    private static string Join(ImmutableArray<TypeAttribution> arguments) =>
        string.Join(", ", arguments.Select(argument => argument.Display));

    /// <inheritdoc/>
    public TypeAttribution GetPrimitiveType(PrimitiveTypeCode typeCode)
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
        return new TypeAttribution(display, "System", string.Empty);
    }

    /// <inheritdoc/>
    public TypeAttribution GetTypeFromDefinition(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        byte rawTypeKind)
    {
        var chain = MetadataNestingWalker.DeclaringTypeChain(reader, handle);
        if (!MetadataNestingWalker.TryFormatTypeDefinitionName(
            chain,
            out var fullName,
            out var namespaceName))
        {
            return TypeAttribution.Unknown;
        }

        return new TypeAttribution(fullName, namespaceName, string.Empty);
    }

    /// <inheritdoc/>
    public TypeAttribution GetTypeFromReference(
        MetadataReader reader,
        TypeReferenceHandle handle,
        byte rawTypeKind) =>
        ResolveTypeReference(handle);

    /// <inheritdoc/>
    public TypeAttribution GetTypeFromSpecification(
        MetadataReader reader,
        object? genericContext,
        TypeSpecificationHandle handle,
        byte rawTypeKind) =>
        throw new BadImageFormatException(
            "TypeSpec callbacks must be handled by a SafeSignatureDecoder validation session.");

    /// <inheritdoc/>
    public TypeAttribution GetGenericInstantiation(
        TypeAttribution genericType,
        ImmutableArray<TypeAttribution> typeArguments) =>
        genericType with { Display = $"{genericType.Display}<{Join(typeArguments)}>" };

    /// <inheritdoc/>
    public TypeAttribution GetSZArrayType(TypeAttribution elementType) =>
        elementType with { Display = $"{elementType.Display}[]" };

    /// <inheritdoc/>
    public TypeAttribution GetArrayType(TypeAttribution elementType, ArrayShape shape) =>
        elementType with { Display = $"{elementType.Display}[{new string(',', shape.Rank - 1)}]" };

    /// <inheritdoc/>
    public TypeAttribution GetByReferenceType(TypeAttribution elementType) =>
        elementType with { Display = $"ref {elementType.Display}" };

    /// <inheritdoc/>
    public TypeAttribution GetPointerType(TypeAttribution elementType) =>
        elementType with { Display = $"{elementType.Display}*" };

    /// <inheritdoc/>
    public TypeAttribution GetGenericMethodParameter(object? genericContext, int index) =>
        new($"!!{index}", string.Empty, string.Empty);

    /// <inheritdoc/>
    public TypeAttribution GetGenericTypeParameter(object? genericContext, int index) =>
        new($"!{index}", string.Empty, string.Empty);

    /// <inheritdoc/>
    public TypeAttribution GetModifiedType(
        TypeAttribution modifier,
        TypeAttribution unmodifiedType,
        bool isRequired) =>
        unmodifiedType;

    /// <inheritdoc/>
    public TypeAttribution GetPinnedType(TypeAttribution elementType) => elementType;

    /// <inheritdoc/>
    public TypeAttribution GetFunctionPointerType(MethodSignature<TypeAttribution> signature) =>
        new("fnptr", string.Empty, string.Empty);
}
