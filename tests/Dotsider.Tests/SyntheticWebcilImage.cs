using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Dotsider.Tests;

/// <summary>
/// Describes a mutable synthetic Webcil image and the offsets needed by boundary tests.
/// </summary>
internal sealed class SyntheticWebcilImage
{
    internal SyntheticWebcilImage(
        byte[] bytes,
        int clrHeaderOffset,
        int methodRva,
        int methodRvaOffset,
        int payloadLength,
        int payloadOffset,
        int sectionCount)
    {
        Bytes = bytes;
        ClrHeaderOffset = clrHeaderOffset;
        MethodRva = methodRva;
        MethodRvaOffset = methodRvaOffset;
        PayloadLength = payloadLength;
        PayloadOffset = payloadOffset;
        SectionCount = sectionCount;
    }

    internal byte[] Bytes { get; }

    internal int ClrHeaderOffset { get; }

    internal int MethodRva { get; }

    internal int MethodRvaOffset { get; }

    internal int PayloadLength { get; }

    internal int PayloadOffset { get; }

    internal int SectionCount { get; }

    internal uint GetSectionPointer(int index) => ReadSectionValue(index, 12);

    internal int GetSectionDataOffset(int index) =>
        checked(PayloadOffset + (int)GetSectionPointer(index));

    internal uint GetSectionVirtualAddress(int index) => ReadSectionValue(index, 4);

    internal uint GetSectionVirtualSize(int index) => ReadSectionValue(index, 0);

    internal uint GetClrMetadataRva() => ReadClrHeaderUInt32(8);

    internal uint GetClrMetadataSize() => ReadClrHeaderUInt32(12);

    internal void SetSectionCount(ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(Bytes.AsSpan(PayloadOffset + 8, sizeof(ushort)), value);

    internal void SetClrExportAddressTableJumpsSize(uint value) =>
        WriteClrHeaderUInt32(60, value);

    internal void SetClrHeaderSize(uint value) =>
        WriteClrHeaderUInt32(0, value);

    internal void SetClrFlags(CorFlags value) =>
        WriteClrHeaderUInt32(16, (uint)value);

    internal void SetClrMajorRuntimeVersion(ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(
            Bytes.AsSpan(PayloadOffset + ClrHeaderOffset + 4, sizeof(ushort)),
            value);

    internal void SetClrManagedNativeHeaderRva(uint value) =>
        WriteClrHeaderUInt32(64, value);

    internal void SetClrManagedNativeHeaderSize(uint value) =>
        WriteClrHeaderUInt32(68, value);

    internal void SetClrMetadataRva(uint value) =>
        WriteClrHeaderUInt32(8, value);

    internal void SetClrMetadataSize(uint value) =>
        WriteClrHeaderUInt32(12, value);

    internal void SetClrResourcesRva(uint value) =>
        WriteClrHeaderUInt32(24, value);

    internal void SetClrResourcesSize(uint value) =>
        WriteClrHeaderUInt32(28, value);

    internal void SetClrStrongNameSignatureRva(uint value) =>
        WriteClrHeaderUInt32(32, value);

    internal void SetClrStrongNameSignatureSize(uint value) =>
        WriteClrHeaderUInt32(36, value);

    internal void SetClrVTableFixupsSize(uint value) =>
        WriteClrHeaderUInt32(52, value);

    internal void SetMethodRva(int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(Bytes.AsSpan(PayloadOffset + MethodRvaOffset, sizeof(int)), value);

    internal void SetPeCliHeaderRva(uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(Bytes.AsSpan(PayloadOffset + 12, sizeof(uint)), value);

    internal void SetPeCliHeaderSize(uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(Bytes.AsSpan(PayloadOffset + 16, sizeof(uint)), value);

    internal void SetPeDebugRva(uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(Bytes.AsSpan(PayloadOffset + 20, sizeof(uint)), value);

    internal void SetPeDebugSize(uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(Bytes.AsSpan(PayloadOffset + 24, sizeof(uint)), value);

    internal void SetSectionPointer(int index, uint value) => WriteSectionValue(index, 12, value);

    internal void SetSectionRawSize(int index, uint value) => WriteSectionValue(index, 8, value);

    internal void SetSectionVirtualAddress(int index, uint value) => WriteSectionValue(index, 4, value);

    internal void SetSectionVirtualSize(int index, uint value) => WriteSectionValue(index, 0, value);

    internal void SetVersionMajor(ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(Bytes.AsSpan(PayloadOffset + 4, sizeof(ushort)), value);

    internal void SetVersionMinor(ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(Bytes.AsSpan(PayloadOffset + 6, sizeof(ushort)), value);

    private int GetSectionOffset(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, SectionCount);
        int headerSize = BinaryPrimitives.ReadUInt16LittleEndian(
            Bytes.AsSpan(PayloadOffset + 4, sizeof(ushort))) >= 1
            ? 32
            : 28;
        return PayloadOffset + headerSize + index * 16;
    }

    private uint ReadSectionValue(int index, int fieldOffset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan(GetSectionOffset(index) + fieldOffset, sizeof(uint)));

    private uint ReadClrHeaderUInt32(int fieldOffset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(
            Bytes.AsSpan(PayloadOffset + ClrHeaderOffset + fieldOffset, sizeof(uint)));

    private void WriteClrHeaderUInt32(int fieldOffset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(
            Bytes.AsSpan(PayloadOffset + ClrHeaderOffset + fieldOffset, sizeof(uint)),
            value);

    private void WriteSectionValue(int index, int fieldOffset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(
            Bytes.AsSpan(GetSectionOffset(index) + fieldOffset, sizeof(uint)),
            value);
}
