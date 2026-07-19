using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Builds a minimal in-memory managed assembly with one method containing caller-supplied IL bytes.
/// </summary>
internal static class SyntheticIlAssembly
{
    /// <summary>
    /// Creates an assembly with one public static method containing the supplied IL bytes.
    /// </summary>
    /// <param name="il">The exact IL bytes to write into the method body.</param>
    /// <returns>The serialized managed PE image.</returns>
    internal static byte[] Create(ReadOnlySpan<byte> il)
    {
        var metadata = new MetadataBuilder();
        metadata.AddAssembly(
            metadata.GetOrAddString("SyntheticIl"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        metadata.AddModule(
            0,
            metadata.GetOrAddString("SyntheticIl.dll"),
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
        metadata.AddTypeDefinition(
            TypeAttributes.Public,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("Owner"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));

        var ilStream = new BlobBuilder();
        var code = new BlobBuilder();
        code.WriteBytes(il.ToArray());
        var instructions = new InstructionEncoder(code);
        int bodyOffset = new MethodBodyStreamEncoder(ilStream).AddMethodBody(instructions, maxStack: 8);
        metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL,
            metadata.GetOrAddString("Method"),
            metadata.GetOrAddBlob(new byte[] { 0x00, 0x00, 0x01 }),
            bodyOffset,
            parameterList: MetadataTokens.ParameterHandle(1));

        var peBuilder = new ManagedPEBuilder(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream);
        var blob = new BlobBuilder();
        peBuilder.Serialize(blob);

        return blob.ToArray();
    }
}
