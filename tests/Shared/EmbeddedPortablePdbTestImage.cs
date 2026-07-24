using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

/// <summary>
/// Locates and mutates the embedded portable PDB payload in a compiler-produced PE image.
/// </summary>
internal static class EmbeddedPortablePdbTestImage
{
    private const int DebugDirectoryEntrySize = 28;
    private static readonly Guid EmbeddedSourceKind =
        new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");

    internal static byte[] ExtractPayload(string assemblyPath)
    {
        byte[] image = File.ReadAllBytes(assemblyPath);
        DebugDirectoryEntry entry = FindEntry(image);
        return image.AsSpan(entry.DataPointer, entry.DataSize).ToArray();
    }

    internal static byte[] ExtractPortablePdb(string assemblyPath)
    {
        byte[] image = File.ReadAllBytes(assemblyPath);
        return DecompressPortablePdb(image, FindEntry(image));
    }

    internal static int ReadDeclaredSize(string assemblyPath)
    {
        byte[] image = File.ReadAllBytes(assemblyPath);
        DebugDirectoryEntry entry = FindEntry(image);
        return BinaryPrimitives.ReadInt32LittleEndian(
            image.AsSpan(entry.DataPointer + sizeof(int), sizeof(int)));
    }

    internal static int ReadEmbeddedSourceDeclaredSize(
        string assemblyPath,
        string documentFileName)
    {
        byte[] image = File.ReadAllBytes(assemblyPath);
        byte[] portablePdb = DecompressPortablePdb(image, FindEntry(image));
        int sourceSizeOffset = FindEmbeddedSourceSizeOffset(portablePdb, documentFileName);
        return BinaryPrimitives.ReadInt32LittleEndian(
            portablePdb.AsSpan(sourceSizeOffset, sizeof(int)));
    }

    internal static byte[] WithDeclaredSize(string assemblyPath, int declaredSize)
    {
        byte[] image = File.ReadAllBytes(assemblyPath);
        DebugDirectoryEntry entry = FindEntry(image);
        BinaryPrimitives.WriteInt32LittleEndian(
            image.AsSpan(entry.DataPointer + sizeof(int), sizeof(int)),
            declaredSize);
        return image;
    }

    internal static byte[] WithEmbeddedSourceDeclaredSize(
        string assemblyPath,
        string documentFileName,
        int declaredSize)
    {
        byte[] image = File.ReadAllBytes(assemblyPath);
        DebugDirectoryEntry[] entries;
        PEHeaders headers;
        using (MemoryStream stream = new(image, writable: false))
        using (PEReader reader = new(stream))
        {
            entries = [.. reader.ReadDebugDirectory()];
            headers = reader.PEHeaders;
        }

        int entryIndex = Array.FindIndex(
            entries,
            static entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
        Assert.IsGreaterThanOrEqualTo(0, entryIndex);
        DebugDirectoryEntry entry = entries[entryIndex];
        byte[] portablePdb = DecompressPortablePdb(image, entry);
        int sourceSizeOffset = FindEmbeddedSourceSizeOffset(portablePdb, documentFileName);
        BinaryPrimitives.WriteInt32LittleEndian(
            portablePdb.AsSpan(sourceSizeOffset, sizeof(int)),
            declaredSize);
        byte[] compressed = Compress(portablePdb);

        int payloadEnd = FindPayloadEnd(image, headers, entries, entry);
        int compressedOffset = checked(entry.DataPointer + (2 * sizeof(int)));
        Assert.IsLessThanOrEqualTo(payloadEnd - compressedOffset, compressed.Length);
        compressed.CopyTo(image.AsSpan(compressedOffset));
        image.AsSpan(compressedOffset + compressed.Length, payloadEnd - compressedOffset - compressed.Length)
            .Clear();

        int debugDirectoryOffset = RvaToFileOffset(
            headers,
            headers.PEHeader!.DebugTableDirectory.RelativeVirtualAddress);
        BinaryPrimitives.WriteInt32LittleEndian(
            image.AsSpan(
                debugDirectoryOffset + (entryIndex * DebugDirectoryEntrySize) + 16,
                sizeof(int)),
            checked((2 * sizeof(int)) + compressed.Length));
        return image;
    }

    private static DebugDirectoryEntry FindEntry(byte[] image)
    {
        using MemoryStream stream = new(image, writable: false);
        using PEReader reader = new(stream);
        return Assert.ContainsSingle(
            static entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb,
            reader.ReadDebugDirectory());
    }

    private static byte[] Compress(byte[] content)
    {
        using MemoryStream output = new();
        using (DeflateStream deflate = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(content);
        return output.ToArray();
    }

    private static byte[] DecompressPortablePdb(byte[] image, DebugDirectoryEntry entry)
    {
        using MemoryStream input = new(
            image,
            entry.DataPointer + (2 * sizeof(int)),
            entry.DataSize - (2 * sizeof(int)),
            writable: false,
            publiclyVisible: false);
        using DeflateStream deflate = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        deflate.CopyTo(output);
        return output.ToArray();
    }

    private static int FindEmbeddedSourceSizeOffset(byte[] portablePdb, string documentFileName)
    {
        BlobHandle valueHandle = default;
        using (MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbImage(
            ImmutableArray.Create(portablePdb)))
        {
            MetadataReader reader = provider.GetMetadataReader();
            DocumentHandle documentHandle = Assert.ContainsSingle(
                handle =>
                {
                    string path = reader.GetString(reader.GetDocument(handle).Name)
                        .Replace('\\', '/');
                    return string.Equals(
                        path[(path.LastIndexOf('/') + 1)..],
                        documentFileName,
                        StringComparison.Ordinal);
                },
                reader.Documents);
            CustomDebugInformationHandle customInformationHandle = Assert.ContainsSingle(
                handle => reader.GetGuid(reader.GetCustomDebugInformation(handle).Kind)
                    == EmbeddedSourceKind,
                reader.GetCustomDebugInformation(documentHandle));
            valueHandle = reader.GetCustomDebugInformation(customInformationHandle).Value;
        }

        int blobStreamOffset = FindBlobStreamOffset(portablePdb);
        int entryOffset = checked(blobStreamOffset + MetadataTokens.GetHeapOffset(valueHandle));
        int prefixLength = GetCompressedIntegerLength(portablePdb[entryOffset]);
        return checked(entryOffset + prefixLength);
    }

    private static int FindBlobStreamOffset(byte[] metadata)
    {
        int versionLength = BinaryPrimitives.ReadInt32LittleEndian(metadata.AsSpan(12));
        int position = checked(16 + versionLength);
        position = (position + 3) & ~3;
        position += sizeof(ushort);
        int streamCount = BinaryPrimitives.ReadUInt16LittleEndian(metadata.AsSpan(position));
        position += sizeof(ushort);

        for (int index = 0; index < streamCount; index++)
        {
            int streamOffset = BinaryPrimitives.ReadInt32LittleEndian(metadata.AsSpan(position));
            position += 2 * sizeof(int);
            int nameStart = position;
            while (metadata[position] != 0)
                position++;
            string name = Encoding.ASCII.GetString(metadata, nameStart, position - nameStart);
            position = (position + 4) & ~3;
            if (name == "#Blob")
                return streamOffset;
        }

        Assert.Fail("The portable PDB has no #Blob stream.");
        return 0;
    }

    private static int FindPayloadEnd(
        byte[] image,
        PEHeaders headers,
        IReadOnlyList<DebugDirectoryEntry> entries,
        DebugDirectoryEntry target)
    {
        int sectionEnd = headers.SectionHeaders
            .Where(section => target.DataPointer >= section.PointerToRawData
                && target.DataPointer - section.PointerToRawData < section.SizeOfRawData)
            .Select(section => checked(section.PointerToRawData + section.SizeOfRawData))
            .Single();
        int nextPayload = entries
            .Where(entry => entry.DataPointer > target.DataPointer)
            .Select(static entry => entry.DataPointer)
            .DefaultIfEmpty(sectionEnd)
            .Min();
        return Math.Min(image.Length, Math.Min(sectionEnd, nextPayload));
    }

    private static int GetCompressedIntegerLength(byte firstByte)
    {
        if ((firstByte & 0x80) == 0)
            return 1;
        if ((firstByte & 0xC0) == 0x80)
            return 2;
        if ((firstByte & 0xE0) == 0xC0)
            return 4;

        Assert.Fail("The portable PDB blob length prefix is invalid.");
        return 0;
    }

    private static int RvaToFileOffset(PEHeaders headers, int rva)
    {
        SectionHeader section = headers.SectionHeaders.Single(candidate =>
            rva >= candidate.VirtualAddress
            && rva - candidate.VirtualAddress < candidate.SizeOfRawData);
        return checked(section.PointerToRawData + rva - section.VirtualAddress);
    }
}
