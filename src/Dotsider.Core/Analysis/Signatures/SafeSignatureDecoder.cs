using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Dotsider.Core.Analysis.Signatures;

/// <summary>
/// Validates complete signature graphs before invoking System.Reflection.Metadata's recursive
/// decoder. This is the only sanctioned production entry point for metadata signature decoding.
/// </summary>
internal static class SafeSignatureDecoder
{
    internal static MethodSignature<TType> DecodeMethodSignature<TType, TGenericContext>(
        MetadataReader reader,
        MethodDefinitionHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var definition = reader.GetMethodDefinition(handle);
        var validator = new SignatureBlobValidator(reader);
        validator.ValidateMethodSignature(definition.Signature, SignatureCallerKind.MethodDefinition);
        var validatedProvider = CreateProvider(reader, provider, genericContext, validator);

#pragma warning disable RS0030 // The facade has validated the complete reachable signature graph.
        return definition.DecodeSignature(validatedProvider, genericContext);
#pragma warning restore RS0030
    }

    internal static TType DecodeFieldSignature<TType, TGenericContext>(
        MetadataReader reader,
        FieldDefinitionHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var definition = reader.GetFieldDefinition(handle);
        var validator = new SignatureBlobValidator(reader);
        validator.ValidateFieldSignature(definition.Signature);
        var validatedProvider = CreateProvider(reader, provider, genericContext, validator);

#pragma warning disable RS0030 // The facade has validated the complete reachable signature graph.
        return definition.DecodeSignature(validatedProvider, genericContext);
#pragma warning restore RS0030
    }

    internal static MethodSignature<TType> DecodePropertySignature<TType, TGenericContext>(
        MetadataReader reader,
        PropertyDefinitionHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var definition = reader.GetPropertyDefinition(handle);
        var validator = new SignatureBlobValidator(reader);
        validator.ValidateMethodSignature(definition.Signature, SignatureCallerKind.PropertyDefinition);
        var validatedProvider = CreateProvider(reader, provider, genericContext, validator);

#pragma warning disable RS0030 // The facade has validated the complete reachable signature graph.
        return definition.DecodeSignature(validatedProvider, genericContext);
#pragma warning restore RS0030
    }

    internal static ImmutableArray<TType> DecodeMethodSpecificationSignature<TType, TGenericContext>(
        MetadataReader reader,
        MethodSpecificationHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var specification = reader.GetMethodSpecification(handle);
        var validator = new SignatureBlobValidator(reader);
        validator.ValidateMethodSpecificationSignature(specification.Signature);
        var validatedProvider = CreateProvider(reader, provider, genericContext, validator);

#pragma warning disable RS0030 // The facade has validated the complete reachable signature graph.
        return specification.DecodeSignature(validatedProvider, genericContext);
#pragma warning restore RS0030
    }

    internal static TType DecodeType<TType, TGenericContext>(
        MetadataReader reader,
        TypeSpecificationHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var validator = new SignatureBlobValidator(reader);
        validator.ValidateTypeSpecification(handle);
        var validatedProvider = CreateProvider(reader, provider, genericContext, validator);
        return DecodeTypeSpecificationRaw(reader, handle, validatedProvider, genericContext);
    }

    internal static MethodSignature<TType> DecodeStandaloneMethodSignature<TType, TGenericContext>(
        MetadataReader reader,
        StandaloneSignatureHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var signature = reader.GetStandaloneSignature(handle);
        var validator = new SignatureBlobValidator(reader);
        validator.ValidateMethodSignature(signature.Signature, SignatureCallerKind.StandaloneSignature);
        var validatedProvider = CreateProvider(reader, provider, genericContext, validator);

#pragma warning disable RS0030 // The facade has validated the complete reachable signature graph.
        return signature.DecodeMethodSignature(validatedProvider, genericContext);
#pragma warning restore RS0030
    }

    internal static ImmutableArray<TType> DecodeLocalSignature<TType, TGenericContext>(
        MetadataReader reader,
        StandaloneSignatureHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var signature = reader.GetStandaloneSignature(handle);
        var validator = new SignatureBlobValidator(reader);
        validator.ValidateLocalSignature(signature.Signature);
        var validatedProvider = CreateProvider(reader, provider, genericContext, validator);

#pragma warning disable RS0030 // The facade has validated the complete reachable signature graph.
        return signature.DecodeLocalSignature(validatedProvider, genericContext);
#pragma warning restore RS0030
    }

    internal static MethodSignature<TType> DecodeMemberReferenceMethodSignature<TType, TGenericContext>(
        MetadataReader reader,
        MemberReferenceHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var reference = reader.GetMemberReference(handle);
        var validator = new SignatureBlobValidator(reader);
        validator.ValidateMethodSignature(reference.Signature, SignatureCallerKind.MemberReference);
        var validatedProvider = CreateProvider(reader, provider, genericContext, validator);

#pragma warning disable RS0030 // The facade has validated the complete reachable signature graph.
        return reference.DecodeMethodSignature(validatedProvider, genericContext);
#pragma warning restore RS0030
    }

    internal static TType DecodeMemberReferenceFieldSignature<TType, TGenericContext>(
        MetadataReader reader,
        MemberReferenceHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var reference = reader.GetMemberReference(handle);
        var validator = new SignatureBlobValidator(reader);
        validator.ValidateFieldSignature(reference.Signature);
        var validatedProvider = CreateProvider(reader, provider, genericContext, validator);

#pragma warning disable RS0030 // The facade has validated the complete reachable signature graph.
        return reference.DecodeFieldSignature(validatedProvider, genericContext);
#pragma warning restore RS0030
    }

    private static ISignatureTypeProvider<TType, TGenericContext> CreateProvider<TType, TGenericContext>(
        MetadataReader reader,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext,
        SignatureBlobValidator validator)
    {
        if (validator.TypeSpecificationSummaries is not { } validatedTypeSpecifications)
        {
            return provider;
        }

        ValidatedTypeSpecProvider<TType, TGenericContext>? validatedProvider = null;
        validatedProvider = new ValidatedTypeSpecProvider<TType, TGenericContext>(
            reader,
            provider,
            validatedTypeSpecifications,
            (handle, _) => DecodeTypeSpecificationRaw(
                reader,
                handle,
                validatedProvider!,
                genericContext));
        return validatedProvider;
    }

    private static TType DecodeTypeSpecificationRaw<TType, TGenericContext>(
        MetadataReader reader,
        TypeSpecificationHandle handle,
        ISignatureTypeProvider<TType, TGenericContext> provider,
        TGenericContext genericContext)
    {
        var specification = reader.GetTypeSpecification(handle);
#pragma warning disable RS0030 // The facade has validated the complete reachable signature graph.
        return specification.DecodeSignature(provider, genericContext);
#pragma warning restore RS0030
    }
}
