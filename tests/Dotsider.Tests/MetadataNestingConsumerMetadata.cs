using Dotsider.Core.Analysis;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Dotsider.Tests;

/// <summary>
/// Builds focused managed metadata images for nesting-consumer fallback tests.
/// </summary>
internal static class MetadataNestingConsumerMetadata
{
    /// <summary>
    /// Builds an assembly containing cyclic TypeDef and TypeRef chains plus MemberRefs whose
    /// declaring types are the cyclic TypeRef and a TypeSpec that transitively uses it.
    /// </summary>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildCyclicConsumerAssembly()
    {
        var metadata = CreateMetadata("CyclicConsumers");

        metadata.AddTypeDefinition(
            TypeAttributes.NestedPublic,
            default,
            metadata.GetOrAddString("InnerDefinition"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(
            TypeAttributes.NestedPublic,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("OuterDefinition"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddNestedType(
            MetadataTokens.TypeDefinitionHandle(2),
            MetadataTokens.TypeDefinitionHandle(3));
        metadata.AddNestedType(
            MetadataTokens.TypeDefinitionHandle(3),
            MetadataTokens.TypeDefinitionHandle(2));

        metadata.AddTypeReference(
            MetadataTokens.TypeReferenceHandle(2),
            default,
            metadata.GetOrAddString("InnerReference"));
        metadata.AddTypeReference(
            MetadataTokens.TypeReferenceHandle(1),
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("OuterReference"));
        metadata.AddMemberReference(
            MetadataTokens.TypeReferenceHandle(1),
            metadata.GetOrAddString("ReferencedMethod"),
            metadata.GetOrAddBlob((byte[])[0x00, 0x00, 0x01]));
        var typeSpecification = metadata.AddTypeSpecification(
            metadata.GetOrAddBlob((byte[])[0x15, 0x12, 0x05, 0x01, 0x08]));
        metadata.AddMemberReference(
            typeSpecification,
            metadata.GetOrAddString("TypeSpecificationMethod"),
            metadata.GetOrAddBlob((byte[])[0x00, 0x00, 0x01]));

        return Serialize(metadata);
    }

    /// <summary>
    /// Builds an assembly whose otherwise-valid MemberRef, MethodSpec, and local signatures refer
    /// to a TypeRef chain that is cyclic, over the nesting limit, or has an unreadable name.
    /// </summary>
    /// <param name="malformedChain">
    /// The malformed chain shape: <c>Cycle</c>, <c>DepthExceeded</c>, or <c>CorruptName</c>.
    /// </param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildMalformedSignatureChainAssembly(string malformedChain)
    {
        var metadata = CreateMetadata("MalformedSignatureChain");
        var externalAssembly = metadata.AddAssemblyReference(
            metadata.GetOrAddString("ExternalTypes"),
            new Version(1, 0, 0, 0),
            default,
            default,
            default,
            default);

        switch (malformedChain)
        {
            case "Cycle":
                metadata.AddTypeReference(
                    MetadataTokens.TypeReferenceHandle(2),
                    default,
                    metadata.GetOrAddString("CycleInner"));
                metadata.AddTypeReference(
                    MetadataTokens.TypeReferenceHandle(1),
                    default,
                    metadata.GetOrAddString("CycleOuter"));
                break;

            case "DepthExceeded":
                {
                    var typeReferenceCount = MetadataNestingWalker.MaxDepth + 2;
                    for (var row = 1; row <= typeReferenceCount; row++)
                    {
                        var scope = row == typeReferenceCount
                            ? (EntityHandle)externalAssembly
                            : MetadataTokens.TypeReferenceHandle(row + 1);
                        metadata.AddTypeReference(
                            scope,
                            row == typeReferenceCount
                                ? metadata.GetOrAddString("Synthetic")
                                : default,
                            metadata.GetOrAddString($"Depth{row}"));
                    }
                    break;
                }

            case "CorruptName":
                metadata.AddTypeReference(
                    externalAssembly,
                    metadata.GetOrAddString("Synthetic"),
                    metadata.GetOrAddString("CorruptName"));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(malformedChain));
        }

        var owner = metadata.AddTypeDefinition(
            TypeAttributes.Public,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("Owner"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        var localSignature = metadata.AddStandaloneSignature(
            metadata.GetOrAddBlob((byte[])[0x07, 0x01, 0x12, 0x05]));

        var ilStream = new BlobBuilder();
        var code = new BlobBuilder();
        var instructions = new InstructionEncoder(code);
        instructions.OpCode(ILOpCode.Ret);
        var bodyOffset = new MethodBodyStreamEncoder(ilStream).AddMethodBody(
            instructions,
            maxStack: 0,
            localVariablesSignature: localSignature);

        var methodSignature = metadata.GetOrAddBlob(
            (byte[])[0x10, 0x01, 0x01, 0x01, 0x12, 0x05]);
        var method = metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL,
            metadata.GetOrAddString("TargetMethod"),
            methodSignature,
            bodyOffset,
            MetadataTokens.ParameterHandle(1));
        metadata.AddGenericParameter(
            method,
            GenericParameterAttributes.None,
            metadata.GetOrAddString("T"),
            index: 0);
        metadata.AddFieldDefinition(
            FieldAttributes.Public | FieldAttributes.Static,
            metadata.GetOrAddString("TargetField"),
            metadata.GetOrAddBlob((byte[])[0x06, 0x12, 0x05]));

        metadata.AddMemberReference(
            owner,
            metadata.GetOrAddString("TargetMethod"),
            methodSignature);
        metadata.AddMemberReference(
            owner,
            metadata.GetOrAddString("TargetField"),
            metadata.GetOrAddBlob((byte[])[0x06, 0x12, 0x05]));
        metadata.AddMethodSpecification(
            method,
            metadata.GetOrAddBlob((byte[])[0x0A, 0x01, 0x12, 0x05]));

        var image = Serialize(metadata, ilStream);
        if (malformedChain == "CorruptName")
        {
            using var stream = new MemoryStream(image, writable: false);
            using var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            var rowOffset = peReader.PEHeaders.MetadataStartOffset
                + reader.GetTableMetadataOffset(TableIndex.TypeRef);

            // ResolutionScope precedes the two-byte Name string-heap index in this small fixture.
            image[rowOffset + 2] = 0xFF;
            image[rowOffset + 3] = 0x7F;
        }

        return image;
    }

    /// <summary>
    /// Builds a facade containing cyclic TypeDefs and ExportedTypes, optionally followed by a valid
    /// forwarder for <c>Synthetic.Target</c>.
    /// </summary>
    /// <param name="assemblyName">The facade assembly name.</param>
    /// <param name="targetAssemblyName">The valid forwarder's target assembly name.</param>
    /// <param name="includeValidForwarder">Whether to append a valid forwarder after the cycle.</param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildCyclicFacade(
        string assemblyName,
        string targetAssemblyName,
        bool includeValidForwarder)
    {
        var metadata = CreateMetadata(assemblyName);
        var targetReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString(targetAssemblyName),
            new Version(1, 0, 0, 0),
            default,
            default,
            default,
            default);

        metadata.AddTypeDefinition(
            TypeAttributes.NestedPublic,
            default,
            metadata.GetOrAddString("Target"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(
            TypeAttributes.NestedPublic,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("CycleOuter"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddNestedType(
            MetadataTokens.TypeDefinitionHandle(2),
            MetadataTokens.TypeDefinitionHandle(3));
        metadata.AddNestedType(
            MetadataTokens.TypeDefinitionHandle(3),
            MetadataTokens.TypeDefinitionHandle(2));

        metadata.AddExportedType(
            TypeAttributes.NestedPublic,
            default,
            metadata.GetOrAddString("CycleInner"),
            MetadataTokens.ExportedTypeHandle(2),
            typeDefinitionId: 0);
        metadata.AddExportedType(
            TypeAttributes.NestedPublic,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("CycleOuter"),
            MetadataTokens.ExportedTypeHandle(1),
            typeDefinitionId: 0);

        if (includeValidForwarder)
        {
            metadata.AddExportedType(
                TypeAttributes.Public | (TypeAttributes)0x00200000,
                metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString("Target"),
                targetReference,
                typeDefinitionId: 0);
        }

        return Serialize(metadata);
    }

    /// <summary>Builds an assembly that owns <c>Synthetic.Target</c> as a TypeDef.</summary>
    /// <param name="assemblyName">The assembly name.</param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildTargetAssembly(string assemblyName)
    {
        var metadata = CreateMetadata(assemblyName);
        metadata.AddTypeDefinition(
            TypeAttributes.Public,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("Target"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        return Serialize(metadata);
    }

    /// <summary>
    /// Builds a facade with configurable, conflicting ownership metadata for
    /// <c>Synthetic.Target</c>.
    /// </summary>
    /// <param name="assemblyName">The facade assembly name.</param>
    /// <param name="targetAssemblyName">The exported types' target assembly name.</param>
    /// <param name="includeTypeDefinition">
    /// Whether the facade also defines <c>Synthetic.Target</c> as a TypeDef.
    /// </param>
    /// <param name="exportedTypeCount">
    /// The number of matching ExportedType forwarders to emit.
    /// </param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildAmbiguousOwnershipFacade(
        string assemblyName,
        string targetAssemblyName,
        bool includeTypeDefinition,
        int exportedTypeCount)
    {
        var metadata = CreateMetadata(assemblyName);
        var targetReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString(targetAssemblyName),
            new Version(1, 0, 0, 0),
            default,
            default,
            default,
            default);

        if (includeTypeDefinition)
        {
            metadata.AddTypeDefinition(
                TypeAttributes.Public,
                metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString("Target"),
                default,
                MetadataTokens.FieldDefinitionHandle(1),
                MetadataTokens.MethodDefinitionHandle(1));
        }

        for (var index = 0; index < exportedTypeCount; index++)
        {
            metadata.AddExportedType(
                TypeAttributes.Public | (TypeAttributes)0x0020_0000,
                metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString("Target"),
                targetReference,
                typeDefinitionId: 0);
        }

        return Serialize(metadata);
    }

    /// <summary>Builds an assembly whose first TypeDef has an invalid string-heap name handle.</summary>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildInvalidTypeDefinitionNameAssembly()
    {
        var image = BuildTargetAssembly("InvalidTypeDefinitionName");
        using var stream = new MemoryStream(image, writable: false);
        using var peReader = new PEReader(stream);
        var reader = peReader.GetMetadataReader();
        var rowOffset = peReader.PEHeaders.MetadataStartOffset
            + reader.GetTableMetadataOffset(TableIndex.TypeDef);

        // Flags precede the two-byte Name string-heap index in this small fixture.
        image[rowOffset + 4] = 0xFF;
        image[rowOffset + 5] = 0x7F;
        return image;
    }

    /// <summary>Builds a metadata-bearing module with one public type, method, and field.</summary>
    /// <param name="moduleName">The module-definition name.</param>
    /// <param name="typeName">The public type name.</param>
    /// <param name="includeAssemblyDefinition">
    /// Whether to emit an Assembly row, making the image an assembly rather than a netmodule.
    /// </param>
    /// <param name="duplicateType">Whether to emit a second TypeDef with the same full name.</param>
    /// <param name="typeAttributes">The owned TypeDef's attributes.</param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildSiblingModule(
        string moduleName,
        string typeName = "ModuleOwned",
        bool includeAssemblyDefinition = false,
        bool duplicateType = false,
        TypeAttributes typeAttributes = TypeAttributes.Public)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString(moduleName),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        if (includeAssemblyDefinition)
        {
            metadata.AddAssembly(
                metadata.GetOrAddString(Path.GetFileNameWithoutExtension(moduleName)),
                new Version(1, 0, 0, 0),
                default,
                default,
                default,
                AssemblyHashAlgorithm.Sha256);
        }

        var runtimeReference = metadata.AddAssemblyReference(
            metadata.GetOrAddString("System.Runtime"),
            new Version(10, 0, 0, 0),
            default,
            default,
            default,
            default);
        metadata.AddTypeReference(
            runtimeReference,
            metadata.GetOrAddString("System"),
            metadata.GetOrAddString("String"));

        metadata.AddTypeDefinition(
            default,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddTypeDefinition(
            typeAttributes,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString(typeName),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        if (duplicateType)
        {
            metadata.AddTypeDefinition(
                typeAttributes,
                metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString(typeName),
                default,
                MetadataTokens.FieldDefinitionHandle(2),
                MetadataTokens.MethodDefinitionHandle(2));
        }
        metadata.AddFieldDefinition(
            FieldAttributes.Public,
            metadata.GetOrAddString("Value"),
            metadata.GetOrAddBlob((byte[])[0x06, 0x08]));
        metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL,
            metadata.GetOrAddString("Run"),
            metadata.GetOrAddBlob((byte[])[0x00, 0x00, 0x01]),
            bodyOffset: 0,
            parameterList: MetadataTokens.ParameterHandle(1));
        return Serialize(metadata);
    }

    /// <summary>Builds a metadata-bearing module with a public nested type.</summary>
    /// <param name="moduleName">The module-definition name.</param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildNestedSiblingModule(string moduleName)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString(moduleName),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddTypeDefinition(
            default,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        var outer = metadata.AddTypeDefinition(
            TypeAttributes.Public,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("Outer"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        var inner = metadata.AddTypeDefinition(
            TypeAttributes.NestedPublic,
            default,
            metadata.GetOrAddString("Inner"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        metadata.AddNestedType(inner, outer);
        return Serialize(metadata);
    }

    /// <summary>Builds a manifest whose exported type is implemented by an AssemblyFile row.</summary>
    /// <param name="assemblyName">The manifest assembly name.</param>
    /// <param name="moduleName">The File-row name.</param>
    /// <param name="moduleBytes">The bytes whose SHA-256 hash is stored in the File row.</param>
    /// <param name="containsMetadata">Whether the File row declares metadata content.</param>
    /// <param name="typeName">The exported type name.</param>
    /// <param name="typeDefinitionId">The advisory TypeDef row id recorded by ExportedType.</param>
    /// <param name="exportedAttributes">The ExportedType attribute flags.</param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildSiblingModuleManifest(
        string assemblyName,
        string moduleName,
        byte[] moduleBytes,
        bool containsMetadata = true,
        string typeName = "ModuleOwned",
        int typeDefinitionId = 2,
        TypeAttributes exportedAttributes = TypeAttributes.Public)
    {
        var metadata = CreateMetadata(assemblyName, AssemblyHashAlgorithm.Sha256);
        var file = metadata.AddAssemblyFile(
            metadata.GetOrAddString(moduleName),
            metadata.GetOrAddBlob(SHA256.HashData(moduleBytes)),
            containsMetadata);
        metadata.AddExportedType(
            exportedAttributes,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString(typeName),
            file,
            typeDefinitionId);
        return Serialize(metadata);
    }

    /// <summary>Builds a manifest exporting a nested type from a sibling module.</summary>
    /// <param name="assemblyName">The manifest assembly name.</param>
    /// <param name="moduleName">The File-row name.</param>
    /// <param name="moduleBytes">The bytes whose SHA-256 hash is stored in the File row.</param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildNestedSiblingModuleManifest(
        string assemblyName,
        string moduleName,
        byte[] moduleBytes)
    {
        var metadata = CreateMetadata(assemblyName, AssemblyHashAlgorithm.Sha256);
        var file = metadata.AddAssemblyFile(
            metadata.GetOrAddString(moduleName),
            metadata.GetOrAddBlob(SHA256.HashData(moduleBytes)),
            containsMetadata: true);
        var outer = metadata.AddExportedType(
            TypeAttributes.Public,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("Outer"),
            file,
            typeDefinitionId: 2);
        metadata.AddExportedType(
            TypeAttributes.NestedPublic,
            default,
            metadata.GetOrAddString("Inner"),
            outer,
            typeDefinitionId: 3);
        return Serialize(metadata);
    }

    private static MetadataBuilder CreateMetadata(
        string assemblyName,
        AssemblyHashAlgorithm hashAlgorithm = AssemblyHashAlgorithm.None)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString(assemblyName + ".dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString(assemblyName),
            new Version(1, 0, 0, 0),
            default,
            default,
            default,
            hashAlgorithm);
        metadata.AddTypeDefinition(
            default,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        return metadata;
    }

    private static byte[] Serialize(MetadataBuilder metadata, BlobBuilder? ilStream = null)
    {
        var peBuilder = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream ?? new BlobBuilder());
        var image = new BlobBuilder();
        peBuilder.Serialize(image);
        return image.ToArray();
    }
}
