using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Builds minimal in-memory managed assemblies shared by metadata-focused tests.
/// </summary>
internal static class SyntheticMetadataBuilder
{
    /// <summary>
    /// Builds an assembly containing a module row and the requested public type definitions.
    /// </summary>
    /// <param name="assemblyName">The assembly and module base name.</param>
    /// <param name="typeNames">The simple names of public types in the Synthetic namespace.</param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildAssembly(string assemblyName, params string[] typeNames)
    {
        var metadata = new MetadataBuilder();
        metadata.AddAssembly(
            metadata.GetOrAddString(assemblyName),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        metadata.AddModule(
            0,
            metadata.GetOrAddString(assemblyName + ".dll"),
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

        foreach (var typeName in typeNames)
        {
            metadata.AddTypeDefinition(
                TypeAttributes.Public,
                metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString(typeName),
                baseType: default,
                fieldList: MetadataTokens.FieldDefinitionHandle(1),
                methodList: MetadataTokens.MethodDefinitionHandle(1));
        }

        var pe = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream: new BlobBuilder());
        var image = new BlobBuilder();
        pe.Serialize(image);
        return image.ToArray();
    }

    /// <summary>
    /// Builds an assembly containing one MethodDef and one method MemberRef for ReadyToRun token tests.
    /// </summary>
    /// <param name="assemblyName">The assembly and module base name.</param>
    /// <param name="typeNames">
    /// The simple names of public types in the Synthetic namespace; a default owner is added when empty.
    /// </param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildAssemblyWithMethodTokens(string assemblyName, params string[] typeNames)
    {
        var metadata = new MetadataBuilder();
        metadata.AddAssembly(
            metadata.GetOrAddString(assemblyName),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        metadata.AddModule(
            0,
            metadata.GetOrAddString(assemblyName + ".dll"),
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
        TypeDefinitionHandle owner = default;
        string[] effectiveTypeNames = typeNames.Length == 0 ? ["Owner"] : typeNames;
        foreach (var typeName in effectiveTypeNames)
        {
            var type = metadata.AddTypeDefinition(
                TypeAttributes.Public,
                metadata.GetOrAddString("Synthetic"),
                metadata.GetOrAddString(typeName),
                baseType: default,
                fieldList: MetadataTokens.FieldDefinitionHandle(1),
                methodList: MetadataTokens.MethodDefinitionHandle(1));
            if (owner.IsNil)
            {
                owner = type;
            }
        }
        var methodSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x00, 0x01 });
        metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL,
            metadata.GetOrAddString("Method"),
            methodSignature,
            bodyOffset: 0,
            parameterList: MetadataTokens.ParameterHandle(1));
        metadata.AddMemberReference(
            owner,
            metadata.GetOrAddString("ReferencedMethod"),
            methodSignature);

        var pe = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream: new BlobBuilder());
        var image = new BlobBuilder();
        pe.Serialize(image);
        return image.ToArray();
    }

    /// <summary>
    /// Builds an assembly containing custom-modified VAR/MVAR TypeSpecs and MemberRefs whose parent
    /// is each TypeSpec.
    /// </summary>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] BuildCustomModifiedGenericParameterNavigationAssembly()
    {
        var metadata = new MetadataBuilder();
        metadata.AddAssembly(
            metadata.GetOrAddString("GenericParameterNavigation"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        metadata.AddModule(
            0,
            metadata.GetOrAddString("GenericParameterNavigation.dll"),
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
            metadata.GetOrAddString("GenericOwner`1"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));
        var methodSignature = metadata.GetOrAddBlob(new byte[] { 0x10, 0x01, 0x00, 0x01 });
        var method = metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL,
            metadata.GetOrAddString("Method"),
            methodSignature,
            bodyOffset: 0,
            parameterList: MetadataTokens.ParameterHandle(1));
        metadata.AddGenericParameter(
            method,
            GenericParameterAttributes.None,
            metadata.GetOrAddString("TMethod"),
            index: 0);
        metadata.AddGenericParameter(
            owner,
            GenericParameterAttributes.None,
            metadata.GetOrAddString("TType"),
            index: 0);

        // CMOD_OPT TypeDef row 2, followed by VAR 0.
        var typeParameter = metadata.AddTypeSpecification(
            metadata.GetOrAddBlob(new byte[] { 0x20, 0x08, 0x13, 0x00 }));
        // CMOD_REQD TypeDef row 2, followed by MVAR 0.
        var methodParameter = metadata.AddTypeSpecification(
            metadata.GetOrAddBlob(new byte[] { 0x1F, 0x08, 0x1E, 0x00 }));
        var memberSignature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x00, 0x01 });
        metadata.AddMemberReference(
            typeParameter,
            metadata.GetOrAddString("Method"),
            memberSignature);
        metadata.AddMemberReference(
            methodParameter,
            metadata.GetOrAddString("Method"),
            memberSignature);

        var pe = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream: new BlobBuilder());
        var image = new BlobBuilder();
        pe.Serialize(image);
        return image.ToArray();
    }
}
