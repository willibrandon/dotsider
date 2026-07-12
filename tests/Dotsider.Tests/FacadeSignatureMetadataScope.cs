using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Owns an in-memory metadata image containing one row for every signature-decoding facade root.
/// </summary>
internal sealed class FacadeSignatureMetadataScope : IDisposable
{
    private readonly MemoryStream _stream;
    private readonly PEReader _peReader;

    private FacadeSignatureMetadataScope(
        MemoryStream stream,
        PEReader peReader,
        MetadataReader reader,
        byte[] image,
        MethodDefinitionHandle methodDefinition,
        FieldDefinitionHandle fieldDefinition,
        PropertyDefinitionHandle propertyDefinition,
        StandaloneSignatureHandle standaloneMethod,
        StandaloneSignatureHandle localSignature,
        MemberReferenceHandle memberReferenceMethod,
        MemberReferenceHandle memberReferenceField,
        MethodSpecificationHandle methodSpecification,
        IReadOnlyList<TypeSpecificationHandle> typeSpecifications)
    {
        _stream = stream;
        _peReader = peReader;
        Reader = reader;
        Image = image;
        MethodDefinition = methodDefinition;
        FieldDefinition = fieldDefinition;
        PropertyDefinition = propertyDefinition;
        StandaloneMethod = standaloneMethod;
        LocalSignature = localSignature;
        MemberReferenceMethod = memberReferenceMethod;
        MemberReferenceField = memberReferenceField;
        MethodSpecification = methodSpecification;
        TypeSpecifications = typeSpecifications;
    }

    /// <summary>Gets the metadata reader.</summary>
    public MetadataReader Reader { get; }

    /// <summary>Gets the serialized managed PE image.</summary>
    public byte[] Image { get; }

    /// <summary>Gets the MethodDef row.</summary>
    public MethodDefinitionHandle MethodDefinition { get; }

    /// <summary>Gets the Field row.</summary>
    public FieldDefinitionHandle FieldDefinition { get; }

    /// <summary>Gets the Property row.</summary>
    public PropertyDefinitionHandle PropertyDefinition { get; }

    /// <summary>Gets the standalone method-signature row.</summary>
    public StandaloneSignatureHandle StandaloneMethod { get; }

    /// <summary>Gets the local-signature row.</summary>
    public StandaloneSignatureHandle LocalSignature { get; }

    /// <summary>Gets the method MemberRef row.</summary>
    public MemberReferenceHandle MemberReferenceMethod { get; }

    /// <summary>Gets the field MemberRef row.</summary>
    public MemberReferenceHandle MemberReferenceField { get; }

    /// <summary>Gets the MethodSpec row.</summary>
    public MethodSpecificationHandle MethodSpecification { get; }

    /// <summary>Gets the TypeSpec rows in insertion order.</summary>
    public IReadOnlyList<TypeSpecificationHandle> TypeSpecifications { get; }

    /// <summary>Creates a metadata image with the supplied root signatures.</summary>
    /// <param name="method">The MethodDef signature.</param>
    /// <param name="field">The Field signature.</param>
    /// <param name="property">The Property signature.</param>
    /// <param name="standaloneMethod">The standalone method signature.</param>
    /// <param name="local">The local-variable signature.</param>
    /// <param name="memberReferenceMethod">The method MemberRef signature.</param>
    /// <param name="memberReferenceField">The field MemberRef signature.</param>
    /// <param name="methodSpecification">The MethodSpec instantiation signature.</param>
    /// <param name="typeSpecifications">The TypeSpec signatures.</param>
    /// <param name="emitMethodBody">Whether the MethodDef should have a body referencing the local signature.</param>
    /// <returns>A disposable metadata scope.</returns>
    public static FacadeSignatureMetadataScope Create(
        byte[]? method = null,
        byte[]? field = null,
        byte[]? property = null,
        byte[]? standaloneMethod = null,
        byte[]? local = null,
        byte[]? memberReferenceMethod = null,
        byte[]? memberReferenceField = null,
        byte[]? methodSpecification = null,
        IReadOnlyList<byte[]>? typeSpecifications = null,
        bool emitMethodBody = false)
    {
        var metadata = new MetadataBuilder();
        metadata.AddAssembly(
            metadata.GetOrAddString("FacadeSignatures"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        var module = metadata.AddModule(
            0,
            metadata.GetOrAddString("FacadeSignatures.dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);

        metadata.AddTypeDefinition(
            default,
            default,
            metadata.GetOrAddString("<Module>"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));
        var owner = metadata.AddTypeDefinition(
            TypeAttributes.Public,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("Owner"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));
        var referencedType = metadata.AddTypeReference(
            module,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("Referenced"));

        var fieldDefinition = metadata.AddFieldDefinition(
            FieldAttributes.Public,
            metadata.GetOrAddString("Field"),
            metadata.GetOrAddBlob(field ?? [0x06, 0x08]));
        var standaloneMethodHandle = metadata.AddStandaloneSignature(
            metadata.GetOrAddBlob(standaloneMethod ?? [0x00, 0x00, 0x08]));
        var localHandle = metadata.AddStandaloneSignature(
            metadata.GetOrAddBlob(local ?? [0x07, 0x01, 0x08]));

        var ilStream = new BlobBuilder();
        var bodyOffset = 0;
        if (emitMethodBody)
        {
            var code = new BlobBuilder();
            var instructions = new InstructionEncoder(code);
            instructions.OpCode(ILOpCode.Ret);
            bodyOffset = new MethodBodyStreamEncoder(ilStream).AddMethodBody(
                instructions,
                maxStack: 0,
                localVariablesSignature: localHandle);
        }

        var methodDefinition = metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL,
            metadata.GetOrAddString("Method"),
            metadata.GetOrAddBlob(method ?? [0x00, 0x00, 0x08]),
            bodyOffset,
            parameterList: MetadataTokens.ParameterHandle(1));
        var propertyDefinition = metadata.AddProperty(
            PropertyAttributes.None,
            metadata.GetOrAddString("Property"),
            metadata.GetOrAddBlob(property ?? [0x08, 0x00, 0x08]));
        metadata.AddPropertyMap(owner, propertyDefinition);

        var memberReferenceMethodHandle = metadata.AddMemberReference(
            referencedType,
            metadata.GetOrAddString("ReferencedMethod"),
            metadata.GetOrAddBlob(memberReferenceMethod ?? [0x00, 0x00, 0x08]));
        var memberReferenceFieldHandle = metadata.AddMemberReference(
            referencedType,
            metadata.GetOrAddString("ReferencedField"),
            metadata.GetOrAddBlob(memberReferenceField ?? [0x06, 0x08]));
        var methodSpecificationHandle = metadata.AddMethodSpecification(
            memberReferenceMethodHandle,
            metadata.GetOrAddBlob(methodSpecification ?? [0x0A, 0x01, 0x08]));

        var typeSpecificationHandles = new List<TypeSpecificationHandle>(typeSpecifications?.Count ?? 0);
        if (typeSpecifications is not null)
        {
            foreach (var signature in typeSpecifications)
            {
                typeSpecificationHandles.Add(metadata.AddTypeSpecification(metadata.GetOrAddBlob(signature)));
            }
        }

        var peBuilder = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream);
        var image = new BlobBuilder();
        peBuilder.Serialize(image);

        var imageBytes = image.ToArray();
        var stream = new MemoryStream(imageBytes, writable: false);
        var peReader = new PEReader(stream);
        return new FacadeSignatureMetadataScope(
            stream,
            peReader,
            peReader.GetMetadataReader(),
            imageBytes,
            methodDefinition,
            fieldDefinition,
            propertyDefinition,
            standaloneMethodHandle,
            localHandle,
            memberReferenceMethodHandle,
            memberReferenceFieldHandle,
            methodSpecificationHandle,
            typeSpecificationHandles);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _peReader.Dispose();
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}
