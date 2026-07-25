using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Tests;

/// <summary>
/// Builds deterministic format-2.2 mstat images using the runtime writer's metadata, IL, and
/// serialized-name layout.
/// </summary>
internal static class SyntheticMstat22Builder
{
    private static readonly Guid ModuleVersionId =
        new("2558c921-3775-4287-97f5-9de6b3904ed6");

    /// <summary>
    /// Creates an mstat image with caller-selected declared and physically encoded target
    /// counts, optionally corrupting the final row.
    /// </summary>
    /// <param name="declaredCounts">The target count declared by each original method.</param>
    /// <param name="encodedTargetCounts">
    /// The number of target pairs physically encoded for each original method.
    /// </param>
    /// <param name="fault">The corruption to apply to the final row.</param>
    /// <returns>The serialized format-2.2 mstat image.</returns>
    internal static byte[] Create(
        IReadOnlyList<int> declaredCounts,
        IReadOnlyList<int> encodedTargetCounts,
        SyntheticMstat22Fault fault = SyntheticMstat22Fault.None)
    {
        if (declaredCounts.Count != encodedTargetCounts.Count)
        {
            throw new ArgumentException(
                "Declared and encoded target count collections must have the same length.");
        }

        var metadata = CreateMetadata();
        var owner = AddFixtureReferences(metadata, out var originals, out var targets);
        var names = new BlobBuilder();
        var targetNameOffsets = AddTargetNames(names);
        var methodNameOffset = AddName(names, "Method entry node");
        var typeNameOffset = AddName(names, "Type entry node");

        var methodStream = new BlobBuilder();
        WriteToken(methodStream, originals[0]);
        WriteInt32(methodStream, 17);
        WriteInt32(methodStream, 2);
        WriteInt32(methodStream, 1);
        WriteInt32(methodStream, methodNameOffset);

        var typeStream = new BlobBuilder();
        WriteToken(typeStream, owner);
        WriteInt32(typeStream, 24);
        WriteInt32(typeStream, typeNameOffset);

        var deduplicatedMethodStream = new BlobBuilder();
        for (var groupIndex = 0; groupIndex < declaredCounts.Count; groupIndex++)
        {
            WriteToken(deduplicatedMethodStream, originals[groupIndex]);
            var isFinalGroup = groupIndex == declaredCounts.Count - 1;
            if (isFinalGroup && fault == SyntheticMstat22Fault.TruncatedCount)
            {
                deduplicatedMethodStream.WriteByte((byte)ILOpCode.Ldc_i4);
                deduplicatedMethodStream.WriteByte(0);
                deduplicatedMethodStream.WriteByte(0);
                break;
            }

            WriteInt32(deduplicatedMethodStream, declaredCounts[groupIndex]);
            for (var targetIndex = 0;
                targetIndex < encodedTargetCounts[groupIndex];
                targetIndex++)
            {
                var isFaultedTarget = isFinalGroup
                    && targetIndex == encodedTargetCounts[groupIndex] - 1;
                if (isFaultedTarget && fault == SyntheticMstat22Fault.MalformedTargetToken)
                {
                    deduplicatedMethodStream.WriteBytes(new byte[] { 0, 0, 0, 0, 0, 0 });
                    break;
                }

                WriteToken(deduplicatedMethodStream, targets[targetIndex]);
                if (isFaultedTarget
                    && fault == SyntheticMstat22Fault.TruncatedTargetNameOffset)
                {
                    break;
                }

                if (isFaultedTarget
                    && fault == SyntheticMstat22Fault.MalformedTargetNameOffset)
                {
                    deduplicatedMethodStream.WriteByte((byte)ILOpCode.Nop);
                    break;
                }

                var targetNameOffset = isFaultedTarget
                    && fault == SyntheticMstat22Fault.OutOfRangeTargetNameOffset
                        ? int.MaxValue
                        : targetNameOffsets[targetIndex];
                WriteInt32(deduplicatedMethodStream, targetNameOffset);
            }
        }

        var ilStream = new BlobBuilder();
        var methodBodies = new MethodBodyStreamEncoder(ilStream);
        AddGlobalMethod(metadata, methodBodies, "Methods", methodStream);
        AddGlobalMethod(metadata, methodBodies, "Types", typeStream);
        AddGlobalMethod(metadata, methodBodies, "Blobs", new BlobBuilder());
        AddGlobalMethod(metadata, methodBodies, "RvaFields", new BlobBuilder());
        AddGlobalMethod(metadata, methodBodies, "FrozenObjects", new BlobBuilder());
        AddGlobalMethod(metadata, methodBodies, "ManifestResources", new BlobBuilder());
        AddGlobalMethod(metadata, methodBodies, "DeduplicatedMethods", deduplicatedMethodStream);

        var pe = new SyntheticMstat22PeBuilder(metadata, ilStream, names);
        var image = new BlobBuilder();
        pe.Serialize(image);
        return image.ToArray();
    }

    private static MetadataBuilder CreateMetadata()
    {
        var metadata = new MetadataBuilder();
        var assemblyName = metadata.GetOrAddString("SyntheticMstat22");
        metadata.AddModule(
            0,
            metadata.GetOrAddString("SyntheticMstat22.mstat"),
            metadata.GetOrAddGuid(ModuleVersionId),
            default,
            default);
        metadata.AddAssembly(
            assemblyName,
            new Version(2, 2),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(
            TypeAttributes.NotPublic,
            default,
            metadata.GetOrAddString("<Module>"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));
        return metadata;
    }

    private static TypeReferenceHandle AddFixtureReferences(
        MetadataBuilder metadata,
        out MemberReferenceHandle[] originals,
        out MemberReferenceHandle[] targets)
    {
        var fixtureAssembly = metadata.AddAssemblyReference(
            metadata.GetOrAddString("FixtureAssembly"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            default);
        var owner = metadata.AddTypeReference(
            fixtureAssembly,
            metadata.GetOrAddString("Fixture"),
            metadata.GetOrAddString("Worker"));
        var signature = metadata.GetOrAddBlob(new byte[] { 0x00, 0x00, 0x01 });

        originals = new MemberReferenceHandle[4];
        targets = new MemberReferenceHandle[4];
        for (var i = 0; i < originals.Length; i++)
        {
            originals[i] = metadata.AddMemberReference(
                owner,
                metadata.GetOrAddString($"Original{i + 1}"),
                signature);
            targets[i] = metadata.AddMemberReference(
                owner,
                metadata.GetOrAddString($"Folded{i + 1}"),
                signature);
        }

        return owner;
    }

    private static int[] AddTargetNames(BlobBuilder names) =>
    [
        AddName(names, "Folded target 1"),
        AddName(names, "Folded target 2"),
        AddName(names, "Folded target 3"),
        AddName(names, "Folded target 4"),
    ];

    private static int AddName(BlobBuilder names, string value)
    {
        var offset = names.Count;
        names.WriteSerializedString(value);
        return offset;
    }

    private static void AddGlobalMethod(
        MetadataBuilder metadata,
        MethodBodyStreamEncoder methodBodies,
        string name,
        BlobBuilder code)
    {
        var bodyOffset = methodBodies.AddMethodBody(
            new InstructionEncoder(code),
            maxStack: 0);
        metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL,
            metadata.GetOrAddString(name),
            metadata.GetOrAddBlob(new byte[] { 0x00, 0x00, 0x01 }),
            bodyOffset,
            parameterList: default);
    }

    private static void WriteInt32(BlobBuilder stream, int value) =>
        new InstructionEncoder(stream).LoadConstantI4(value);

    private static void WriteToken(BlobBuilder stream, EntityHandle handle)
    {
        stream.WriteByte((byte)ILOpCode.Ldtoken);
        stream.WriteInt32(MetadataTokens.GetToken(handle));
    }
}
