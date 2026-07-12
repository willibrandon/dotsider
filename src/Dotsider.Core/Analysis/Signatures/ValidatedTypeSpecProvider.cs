using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis.Signatures;

/// <summary>
/// Delegates signature construction to an application provider while restricting TypeSpec decode
/// callbacks to the immutable graph that was validated for the current root signature.
/// </summary>
internal sealed class ValidatedTypeSpecProvider<TType, TGenericContext> :
    ISignatureTypeProvider<TType, TGenericContext>
{
    private readonly MetadataReader _metadataReader;
    private readonly ISignatureTypeProvider<TType, TGenericContext> _provider;
    private readonly IReadOnlyDictionary<TypeSpecificationHandle, (int SaturatedWork, int MaxRelativeDepth)>
        _validatedTypeSpecifications;
    private readonly Func<TypeSpecificationHandle, byte, TType> _decodeTypeSpecification;
    private HashSet<TypeSpecificationHandle>? _activeDecodes;

    internal ValidatedTypeSpecProvider(
        MetadataReader metadataReader,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        IReadOnlyDictionary<TypeSpecificationHandle, (int SaturatedWork, int MaxRelativeDepth)>
            validatedTypeSpecifications,
        Func<TypeSpecificationHandle, byte, TType> decodeTypeSpecification)
    {
        _metadataReader = metadataReader;
        _provider = provider;
        _validatedTypeSpecifications = validatedTypeSpecifications;
        _decodeTypeSpecification = decodeTypeSpecification;
    }

    /// <inheritdoc/>
    public TType GetArrayType(TType elementType, ArrayShape shape) =>
        _provider.GetArrayType(elementType, shape);

    /// <inheritdoc/>
    public TType GetByReferenceType(TType elementType) =>
        _provider.GetByReferenceType(elementType);

    /// <inheritdoc/>
    public TType GetFunctionPointerType(MethodSignature<TType> signature) =>
        _provider.GetFunctionPointerType(signature);

    /// <inheritdoc/>
    public TType GetGenericInstantiation(TType genericType, ImmutableArray<TType> typeArguments) =>
        _provider.GetGenericInstantiation(genericType, typeArguments);

    /// <inheritdoc/>
    public TType GetGenericMethodParameter(TGenericContext genericContext, int index) =>
        _provider.GetGenericMethodParameter(genericContext, index);

    /// <inheritdoc/>
    public TType GetGenericTypeParameter(TGenericContext genericContext, int index) =>
        _provider.GetGenericTypeParameter(genericContext, index);

    /// <inheritdoc/>
    public TType GetModifiedType(TType modifier, TType unmodifiedType, bool isRequired) =>
        _provider.GetModifiedType(modifier, unmodifiedType, isRequired);

    /// <inheritdoc/>
    public TType GetPinnedType(TType elementType) =>
        _provider.GetPinnedType(elementType);

    /// <inheritdoc/>
    public TType GetPointerType(TType elementType) =>
        _provider.GetPointerType(elementType);

    /// <inheritdoc/>
    public TType GetPrimitiveType(PrimitiveTypeCode typeCode) =>
        _provider.GetPrimitiveType(typeCode);

    /// <inheritdoc/>
    public TType GetSZArrayType(TType elementType) =>
        _provider.GetSZArrayType(elementType);

    /// <inheritdoc/>
    public TType GetTypeFromDefinition(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        byte rawTypeKind) =>
        _provider.GetTypeFromDefinition(reader, handle, rawTypeKind);

    /// <inheritdoc/>
    public TType GetTypeFromReference(
        MetadataReader reader,
        TypeReferenceHandle handle,
        byte rawTypeKind) =>
        _provider.GetTypeFromReference(reader, handle, rawTypeKind);

    /// <inheritdoc/>
    public TType GetTypeFromSpecification(
        MetadataReader reader,
        TGenericContext genericContext,
        TypeSpecificationHandle handle,
        byte rawTypeKind)
    {
        if (!ReferenceEquals(reader, _metadataReader) || !_validatedTypeSpecifications.ContainsKey(handle))
        {
            throw new BadImageFormatException("TypeSpec decode escaped the prevalidated signature graph.");
        }

        _activeDecodes ??= [];
        if (!_activeDecodes.Add(handle))
        {
            throw new BadImageFormatException($"Cyclic TypeSpec decode at token 0x{MetadataTokens.GetToken(handle):X8}.");
        }

        try
        {
            return _decodeTypeSpecification(handle, rawTypeKind);
        }
        finally
        {
            _activeDecodes.Remove(handle);
        }
    }
}
