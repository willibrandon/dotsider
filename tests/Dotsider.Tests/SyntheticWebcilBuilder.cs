using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Produces deterministic Webcil images from real ECMA-335 metadata and IL.
/// </summary>
internal static class SyntheticWebcilBuilder
{
    private const int DebugDirectoryEntrySize = 28;
    private const int SectionHeaderSize = 16;
    private const uint WebcilMagic = 0x4C496257;

    internal static SyntheticWebcilImage Create(
        int version = 1,
        bool wrapped = false,
        int sectionCount = 1,
        int wrapperSuffixLength = 0,
        int additionalSectionSize = 4)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(version, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sectionCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(wrapperSuffixLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(additionalSectionSize, 1);

        byte[] peBytes = CreatePortableExecutable();
        using PEReader peReader = new(new MemoryStream(peBytes, writable: false));
        PEHeaders headers = peReader.PEHeaders;
        PEHeader peHeader = headers.PEHeader
            ?? throw new InvalidOperationException("The synthetic PE has no optional header.");
        int corHeaderRva = peHeader.CorHeaderTableDirectory.RelativeVirtualAddress;
        SectionHeader sourceSection = headers.SectionHeaders.Single(section =>
            corHeaderRva >= section.VirtualAddress
            && corHeaderRva - section.VirtualAddress < section.VirtualSize);
        MetadataReader metadataReader = peReader.GetMetadataReader();
        int methodRva = metadataReader.GetMethodDefinition(
            MetadataTokens.MethodDefinitionHandle(1)).RelativeVirtualAddress;
        int metadataRva = headers.CorHeader?.MetadataDirectory.RelativeVirtualAddress
            ?? throw new InvalidOperationException("The synthetic PE has no CLR header.");

        int headerSize = version == 0 ? 28 : 32;
        int rawStart = Align(headerSize + sectionCount * SectionHeaderSize, 16);
        int sourceRawSize = sourceSection.SizeOfRawData;
        int payloadLength = checked(
            rawStart + sourceRawSize + (sectionCount - 1) * additionalSectionSize);
        byte[] payload = new byte[payloadLength];

        BinaryPrimitives.WriteUInt32LittleEndian(payload, WebcilMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), checked((ushort)version));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), checked((ushort)sectionCount));
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(12), checked((uint)corHeaderRva));
        BinaryPrimitives.WriteUInt32LittleEndian(
            payload.AsSpan(16),
            checked((uint)peHeader.CorHeaderTableDirectory.Size));
        BinaryPrimitives.WriteUInt32LittleEndian(
            payload.AsSpan(20),
            checked((uint)peHeader.DebugTableDirectory.RelativeVirtualAddress));
        BinaryPrimitives.WriteUInt32LittleEndian(
            payload.AsSpan(24),
            checked((uint)peHeader.DebugTableDirectory.Size));
        if (version == 1)
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(28), uint.MaxValue);

        WriteSection(
            payload,
            headerSize,
            checked((uint)sourceSection.VirtualSize),
            checked((uint)sourceSection.VirtualAddress),
            checked((uint)sourceRawSize),
            checked((uint)rawStart));
        peBytes.AsSpan(sourceSection.PointerToRawData, sourceRawSize).CopyTo(payload.AsSpan(rawStart));
        int methodRvaOffset = checked(
            rawStart
            + metadataRva
            - sourceSection.VirtualAddress
            + metadataReader.GetTableMetadataOffset(TableIndex.MethodDef));
        int clrHeaderOffset = checked(rawStart + corHeaderRva - sourceSection.VirtualAddress);

        int nextRaw = rawStart + sourceRawSize;
        uint nextVirtual = checked((uint)(sourceSection.VirtualAddress + sourceSection.VirtualSize));
        for (int index = 1; index < sectionCount; index++)
        {
            WriteSection(
                payload,
                headerSize + index * SectionHeaderSize,
                virtualSize: checked((uint)additionalSectionSize),
                virtualAddress: nextVirtual,
                rawSize: checked((uint)additionalSectionSize),
                rawPointer: checked((uint)nextRaw));
            payload[nextRaw] = 0x02;
            nextRaw += additionalSectionSize;
            nextVirtual += checked((uint)additionalSectionSize);
        }

        if (!wrapped)
            return new SyntheticWebcilImage(
                payload,
                clrHeaderOffset,
                methodRva,
                methodRvaOffset,
                payload.Length,
                payloadOffset: 0,
                sectionCount);

        byte[] wrappedBytes = Wrap(payload, wrapperSuffixLength, out int payloadOffset);
        return new SyntheticWebcilImage(
            wrappedBytes,
            clrHeaderOffset,
            methodRva,
            methodRvaOffset,
            payload.Length,
            payloadOffset,
            sectionCount);
    }

    internal static SyntheticWebcilImage CreateWithDebugDirectory(
        int version = 1,
        bool wrapped = false)
    {
        SyntheticWebcilImage image = Create(
            version,
            wrapped,
            sectionCount: 2,
            additionalSectionSize: 64);
        uint directoryRva = image.GetSectionVirtualAddress(1);
        uint directoryPointer = image.GetSectionPointer(1);
        uint dataRva = checked(directoryRva + DebugDirectoryEntrySize);
        uint dataPointer = checked(directoryPointer + DebugDirectoryEntrySize);
        image.SetPeDebugRva(directoryRva);
        image.SetPeDebugSize(DebugDirectoryEntrySize);

        Span<byte> entry = image.Bytes.AsSpan(
            image.GetSectionDataOffset(1),
            DebugDirectoryEntrySize);
        entry.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], 0x12345678);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[8..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(entry[10..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(entry[12..], (int)DebugDirectoryEntryType.Reproducible);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[16..], 4);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[20..], dataRva);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[24..], dataPointer);
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.Bytes.AsSpan(checked(image.PayloadOffset + (int)dataPointer)),
            0xA5A5A5A5);
        return image;
    }

    internal static SyntheticWebcilImage CreateWithOversizedWasmDataSection(int version = 1)
    {
        SyntheticWebcilImage bare = Create(version);
        byte[] wrappedBytes = Wrap(
            bare.Bytes,
            suffixLength: 0,
            out int payloadOffset,
            declaredSizeAdjustment: 1);
        return new SyntheticWebcilImage(
            wrappedBytes,
            bare.ClrHeaderOffset,
            bare.MethodRva,
            bare.MethodRvaOffset,
            bare.PayloadLength,
            payloadOffset,
            bare.SectionCount);
    }

    internal static byte[] CreateWithTruncatedSectionTable(int version, bool wrapped)
    {
        SyntheticWebcilImage image = Create(version);
        int headerSize = version == 0 ? 28 : 32;
        byte[] truncatedPayload = image.Bytes.AsSpan(0, headerSize + SectionHeaderSize - 1).ToArray();
        return wrapped
            ? Wrap(truncatedPayload, suffixLength: 0, out _)
            : truncatedPayload;
    }

    private static int Align(int value, int alignment) =>
        checked((value + alignment - 1) & ~(alignment - 1));

    private static byte[] CreatePortableExecutable()
    {
        MetadataBuilder metadata = new();
        metadata.AddAssembly(
            metadata.GetOrAddString("SyntheticWebcil"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        metadata.AddModule(
            0,
            metadata.GetOrAddString("SyntheticWebcil.dll"),
            metadata.GetOrAddGuid(new Guid("497F4CE9-AB71-4A8B-9E5B-3753807F4A56")),
            default,
            default);

        metadata.AddTypeDefinition(
            TypeAttributes.NotPublic,
            default,
            metadata.GetOrAddString("<Module>"),
            baseType: default,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));

        AssemblyName coreLibraryName = typeof(object).Assembly.GetName();
        byte[]? publicKeyToken = coreLibraryName.GetPublicKeyToken();
        BlobHandle publicKeyTokenHandle = publicKeyToken is { Length: > 0 }
            ? metadata.GetOrAddBlob(publicKeyToken)
            : default;
        AssemblyReferenceHandle coreLibrary = metadata.AddAssemblyReference(
            metadata.GetOrAddString(coreLibraryName.Name!),
            coreLibraryName.Version ?? new Version(0, 0, 0, 0),
            default,
            publicKeyTokenHandle,
            0,
            default);
        TypeReferenceHandle objectType = metadata.AddTypeReference(
            coreLibrary,
            metadata.GetOrAddString("System"),
            metadata.GetOrAddString("Object"));

        metadata.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            metadata.GetOrAddString("Synthetic"),
            metadata.GetOrAddString("Calculator"),
            baseType: objectType,
            fieldList: MetadataTokens.FieldDefinitionHandle(1),
            methodList: MetadataTokens.MethodDefinitionHandle(1));

        BlobBuilder ilStream = new();
        BlobBuilder code = new();
        InstructionEncoder encoder = new(code);
        encoder.LoadConstantI4(42);
        encoder.OpCode(ILOpCode.Ret);
        int bodyOffset = new MethodBodyStreamEncoder(ilStream).AddMethodBody(encoder, maxStack: 1);
        byte[] methodSignature = [0x00, 0x00, 0x08];
        metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            metadata.GetOrAddString("Answer"),
            metadata.GetOrAddBlob(methodSignature),
            bodyOffset,
            MetadataTokens.ParameterHandle(1));

        BlobBuilder managedResources = new();
        managedResources.WriteInt32(4);
        byte[] resourceBytes = [0x10, 0x20, 0x30, 0x40];
        managedResources.WriteBytes(resourceBytes);
        metadata.AddManifestResource(
            ManifestResourceAttributes.Public,
            metadata.GetOrAddString("SyntheticResource"),
            implementation: default,
            offset: 0);

        ManagedPEBuilder peBuilder = new(
            new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll),
            new MetadataRootBuilder(metadata),
            ilStream,
            managedResources: managedResources);
        BlobBuilder image = new();
        peBuilder.Serialize(image);
        return image.ToArray();
    }

    private static void WriteSection(
        Span<byte> payload,
        int offset,
        uint virtualSize,
        uint virtualAddress,
        uint rawSize,
        uint rawPointer)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(payload[offset..], virtualSize);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[(offset + 4)..], virtualAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[(offset + 8)..], rawSize);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[(offset + 12)..], rawPointer);
    }

    private static byte[] Wrap(
        byte[] payload,
        int suffixLength,
        out int payloadOffset,
        int declaredSizeAdjustment = 0)
    {
        using MemoryStream stream = new();
        stream.Write([0x00, 0x61, 0x73, 0x6D, 0x01, 0x00, 0x00, 0x00]);
        stream.WriteByte(11);

        using MemoryStream dataSection = new();
        WriteUleb(dataSection, 1);
        dataSection.WriteByte(1);
        WriteUleb(dataSection, checked((uint)payload.Length));
        int dataHeaderLength = checked((int)dataSection.Length);
        dataSection.Write(payload);
        WriteUleb(stream, checked((uint)(dataSection.Length + declaredSizeAdjustment)));
        payloadOffset = checked((int)stream.Length + dataHeaderLength);
        dataSection.Position = 0;
        dataSection.CopyTo(stream);

        if (suffixLength > 0)
        {
            stream.WriteByte(0);
            WriteUleb(stream, checked((uint)suffixLength + 1));
            WriteUleb(stream, 0);
            stream.Write(new byte[suffixLength]);
        }

        return stream.ToArray();
    }

    private static void WriteUleb(Stream stream, uint value)
    {
        do
        {
            byte next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
                next |= 0x80;
            stream.WriteByte(next);
        }
        while (value != 0);
    }
}
