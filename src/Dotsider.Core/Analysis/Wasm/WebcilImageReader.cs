using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Dotsider.Core.Analysis.Wasm;

/// <summary>
/// Reads Webcil managed assemblies, including Webcil payloads wrapped inside WebAssembly modules.
/// This mirrors the runtime Webcil reader closely enough for dotsider to expose metadata, IL,
/// debug-directory, and portable-PDB behavior without depending on runtime tooling.
/// </summary>
internal sealed class WebcilImageReader
{
    private const uint WebcilMagic = 0x4C496257;
    private const uint WasmMagic = 0x6D736100;
    private const uint WasmVersion = 1;
    private const int HeaderV0Size = 28;
    private const int HeaderV1Size = 32;
    private const int SectionHeaderSize = 16;
    private const int DebugDirectoryEntrySize = 28;
    private const uint EmbeddedPortablePdbSignature = 0x4244504D;

    private readonly byte[] _image;
    private readonly List<WebcilSection> _sections;
    private readonly List<WebcilDebugEntry> _debugEntries;
    private readonly WebcilHeader _header;
    private readonly byte[] _metadataBytes;

    private WebcilImageReader(
        byte[] image,
        long payloadOffset,
        WebcilHeader header,
        List<WebcilSection> sections,
        byte[] metadataBytes,
        List<WebcilDebugEntry> debugEntries)
    {
        _image = image;
        PayloadOffset = payloadOffset;
        _header = header;
        _sections = sections;
        _metadataBytes = metadataBytes;
        _debugEntries = debugEntries;
        Info = new WebcilInfo(
            header.VersionMajor,
            header.VersionMinor,
            payloadOffset > 0,
            payloadOffset,
            sections.Count,
            metadataBytes.Length,
            checked((int)header.PeDebugSize));
        ClrHeader = ReadClrHeader(header, sections, image, payloadOffset);
    }

    /// <summary>
    /// Gets display and provenance information for the Webcil image.
    /// </summary>
    public WebcilInfo Info { get; }

    /// <summary>
    /// Gets the CLR header reconstructed from the Webcil COR directory.
    /// </summary>
    public ClrHeader ClrHeader { get; }

    /// <summary>
    /// Attempts to read a Webcil image from raw bytes.
    /// </summary>
    /// <param name="bytes">The candidate file bytes.</param>
    /// <param name="reader">The parsed Webcil reader when the bytes contain Webcil.</param>
    /// <returns>True when a bare or Wasm-wrapped Webcil payload was found and parsed.</returns>
    public static bool TryRead(ReadOnlySpan<byte> bytes, out WebcilImageReader? reader)
    {
        reader = null;
        if (!TryFindPayload(bytes, out var payloadOffset))
            return false;

        try
        {
            if (!TryReadHeader(bytes, payloadOffset, out var header))
                return false;

            var sections = ReadSections(bytes, payloadOffset, header);
            var metadataBytes = ReadMetadata(bytes, payloadOffset, header, sections);
            var debugEntries = ReadDebugEntries(bytes, payloadOffset, header, sections);
            reader = new WebcilImageReader(
                bytes.ToArray(),
                payloadOffset,
                header,
                sections,
                metadataBytes,
                debugEntries);
            return true;
        }
        catch (Exception ex) when (ex is BadImageFormatException or ArgumentOutOfRangeException or OverflowException)
        {
            reader = null;
            return false;
        }
    }

    /// <summary>
    /// Creates a metadata provider over the Webcil metadata blob.
    /// </summary>
    /// <returns>A metadata provider over the Webcil ECMA-335 metadata image.</returns>
    public MetadataReaderProvider CreateMetadataReaderProvider() =>
        MetadataReaderProvider.FromMetadataImage(ImmutableArray.Create(_metadataBytes));

    /// <summary>
    /// Reads a managed method body by RVA from the Webcil section table.
    /// </summary>
    /// <param name="rva">The method body's RVA.</param>
    /// <returns>The method body block, or null when the RVA does not map to Webcil bytes.</returns>
    public unsafe MethodBodyBlock? GetMethodBody(int rva)
    {
        if (rva == 0 || !TryTranslateRva((uint)rva, out var offset, out var available) || available <= 0)
            return null;

        fixed (byte* ptr = &_image[(int)offset])
        {
            var blob = new BlobReader(ptr, checked((int)available));
            return MethodBodyBlock.Create(blob);
        }
    }

    /// <summary>
    /// Converts Webcil sections to dotsider's generic section table rows.
    /// </summary>
    /// <returns>Generic section rows with Webcil-adjusted raw data offsets.</returns>
    public IReadOnlyList<SectionInfo> ReadSections() =>
        [.. _sections.Select((s, i) => new SectionInfo(
            Name: $"webcil-section-{i}",
            VirtualAddress: checked((int)s.VirtualAddress),
            VirtualSize: checked((int)s.VirtualSize),
            RawDataOffset: checked((int)(PayloadOffset + s.PointerToRawData)),
            RawDataSize: checked((int)s.SizeOfRawData),
            Characteristics: 0))];

    /// <summary>
    /// Converts Webcil debug directory entries to dotsider display rows.
    /// </summary>
    /// <returns>Debug directory rows with formatted Webcil payload details.</returns>
    public IReadOnlyList<DebugDirectoryInfo> ReadDebugDirectory() =>
        [.. _debugEntries.Select(e => new DebugDirectoryInfo(
            Type: e.Type,
            Stamp: e.Stamp,
            MajorVersion: e.MajorVersion,
            MinorVersion: e.MinorVersion,
            DataSize: e.DataSize,
            AddressOfRawData: e.DataRva,
            PointerToRawData: checked((int)(PayloadOffset + e.DataPointer)),
            Payload: FormatPayload(e)))];

    /// <summary>
    /// Opens an embedded portable PDB from a Webcil debug directory entry.
    /// </summary>
    /// <param name="entry">The embedded portable PDB debug directory entry.</param>
    /// <returns>A metadata provider over the decompressed portable PDB image.</returns>
    public MetadataReaderProvider ReadEmbeddedPortablePdb(WebcilDebugEntry entry)
    {
        var payload = ReadEntryPayload(entry);
        if (payload.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(payload) != EmbeddedPortablePdbSignature)
            throw new BadImageFormatException("Unexpected embedded portable PDB signature.");

        var decompressedSize = BinaryPrimitives.ReadInt32LittleEndian(payload[4..]);
        using var compressed = new MemoryStream(payload[8..].ToArray(), writable: false);
        using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
        var decompressed = new byte[decompressedSize];
        deflate.ReadExactly(decompressed);
        return MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(decompressed));
    }

    /// <summary>
    /// Gets the first embedded portable PDB entry from the Webcil debug directory.
    /// </summary>
    /// <returns>The embedded portable PDB entry, or null when absent.</returns>
    public WebcilDebugEntry? EmbeddedPortablePdbEntry() =>
        FindDebugEntry(DebugDirectoryEntryType.EmbeddedPortablePdb);

    /// <summary>
    /// Gets the first CodeView entry from the Webcil debug directory.
    /// </summary>
    /// <returns>The CodeView debug entry, or null when absent.</returns>
    public WebcilDebugEntry? CodeViewEntry() =>
        FindDebugEntry(DebugDirectoryEntryType.CodeView);

    /// <summary>
    /// Decodes CodeView portable-PDB identity data from a Webcil debug entry.
    /// </summary>
    /// <param name="entry">The CodeView debug directory entry.</param>
    /// <returns>The portable PDB identity and build-time path.</returns>
    public WebcilCodeViewData ReadCodeView(WebcilDebugEntry entry)
    {
        var payload = ReadEntryPayload(entry);
        if (payload.Length < 24
            || payload[0] != (byte)'R'
            || payload[1] != (byte)'S'
            || payload[2] != (byte)'D'
            || payload[3] != (byte)'S')
        {
            throw new BadImageFormatException("Unexpected CodeView payload signature.");
        }

        var guid = new Guid(payload[4..20]);
        var age = BinaryPrimitives.ReadInt32LittleEndian(payload[20..]);
        var path = ReadUtf8NullTerminated(payload[24..]);
        return new WebcilCodeViewData(guid, age, path);
    }

    /// <summary>
    /// Reads a 32-bit value at the requested RVA.
    /// </summary>
    /// <param name="rva">The base RVA to translate through the Webcil section table.</param>
    /// <param name="relativeOffset">The byte offset from <paramref name="rva"/>.</param>
    /// <param name="value">The decoded little-endian 32-bit value.</param>
    /// <returns>True when the requested bytes mapped to the Webcil image.</returns>
    public bool TryReadInt32AtRva(int rva, int relativeOffset, out int value)
    {
        value = 0;
        if (!TryTranslateRva((uint)rva, out var offset, out var available)
            || relativeOffset < 0
            || relativeOffset + 4 > available)
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(_image.AsSpan((int)offset + relativeOffset, 4));
        return true;
    }

    private long PayloadOffset { get; }

    private static bool TryFindPayload(ReadOnlySpan<byte> bytes, out long payloadOffset)
    {
        payloadOffset = 0;
        if (bytes.Length < 4)
            return false;

        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) == WebcilMagic)
            return true;

        if (bytes.Length < 8
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != WasmMagic
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != WasmVersion)
        {
            return false;
        }

        var pos = 8;
        while (pos < bytes.Length)
        {
            var sectionId = ReadByte(bytes, ref pos);
            var sectionSize = checked((int)ReadUleb(bytes, ref pos));
            var sectionEnd = checked(pos + sectionSize);
            if (sectionEnd > bytes.Length)
                return false;

            if (sectionId != 11)
            {
                pos = sectionEnd;
                continue;
            }

            var segmentCount = checked((int)ReadUleb(bytes, ref pos));
            for (var i = 0; i < segmentCount && pos < sectionEnd; i++)
            {
                var mode = ReadByte(bytes, ref pos);
                if (mode == 0)
                    SkipConstExpr(bytes, ref pos, sectionEnd);
                else if (mode == 2)
                {
                    _ = ReadUleb(bytes, ref pos);
                    SkipConstExpr(bytes, ref pos, sectionEnd);
                }
                else if (mode != 1)
                {
                    return false;
                }

                var size = checked((int)ReadUleb(bytes, ref pos));
                if (pos + size > sectionEnd)
                    return false;

                var segment = bytes[pos..(pos + size)];
                if (segment.Length >= 4
                    && BinaryPrimitives.ReadUInt32LittleEndian(segment) == WebcilMagic
                    && TryReadHeader(bytes, pos, out _))
                {
                    payloadOffset = pos;
                    return true;
                }

                pos += size;
            }

            return false;
        }

        return false;
    }

    private static bool TryReadHeader(ReadOnlySpan<byte> bytes, long offset, out WebcilHeader header)
    {
        header = default;
        if (offset < 0 || offset + HeaderV0Size > bytes.Length)
            return false;

        var span = bytes[(int)offset..];
        header = new WebcilHeader(
            Id: BinaryPrimitives.ReadUInt32LittleEndian(span),
            VersionMajor: BinaryPrimitives.ReadUInt16LittleEndian(span[4..]),
            VersionMinor: BinaryPrimitives.ReadUInt16LittleEndian(span[6..]),
            CoffSections: BinaryPrimitives.ReadUInt16LittleEndian(span[8..]),
            PeCliHeaderRva: BinaryPrimitives.ReadUInt32LittleEndian(span[12..]),
            PeCliHeaderSize: BinaryPrimitives.ReadUInt32LittleEndian(span[16..]),
            PeDebugRva: BinaryPrimitives.ReadUInt32LittleEndian(span[20..]),
            PeDebugSize: BinaryPrimitives.ReadUInt32LittleEndian(span[24..]),
            TableBase: uint.MaxValue);

        if (header.Id != WebcilMagic
            || header.VersionMajor is not (0 or 1)
            || header.VersionMinor != 0)
        {
            return false;
        }

        if (header.VersionMajor >= 1)
        {
            if (offset + HeaderV1Size > bytes.Length)
                return false;
            header = header with { TableBase = BinaryPrimitives.ReadUInt32LittleEndian(span[HeaderV0Size..]) };
        }

        return true;
    }

    private static List<WebcilSection> ReadSections(ReadOnlySpan<byte> bytes, long payloadOffset, WebcilHeader header)
    {
        var sectionOffset = checked(payloadOffset + (header.VersionMajor >= 1 ? HeaderV1Size : HeaderV0Size));
        var result = new List<WebcilSection>(header.CoffSections);
        for (var i = 0; i < header.CoffSections; i++)
        {
            var offset = checked(sectionOffset + i * SectionHeaderSize);
            if (offset + SectionHeaderSize > bytes.Length)
                throw new BadImageFormatException("The Webcil section table is truncated.");

            var span = bytes[(int)offset..];
            result.Add(new WebcilSection(
                VirtualSize: BinaryPrimitives.ReadUInt32LittleEndian(span),
                VirtualAddress: BinaryPrimitives.ReadUInt32LittleEndian(span[4..]),
                SizeOfRawData: BinaryPrimitives.ReadUInt32LittleEndian(span[8..]),
                PointerToRawData: BinaryPrimitives.ReadUInt32LittleEndian(span[12..])));
        }

        return result;
    }

    private static byte[] ReadMetadata(
        ReadOnlySpan<byte> bytes,
        long payloadOffset,
        WebcilHeader header,
        IReadOnlyList<WebcilSection> sections)
    {
        var clr = ReadClrHeader(header, sections, bytes, payloadOffset);
        if (!TryTranslateRva(sections, payloadOffset, (uint)clr.MetadataRva, out var offset, out var available)
            || clr.MetadataSize <= 0
            || clr.MetadataSize > available)
        {
            throw new BadImageFormatException("The Webcil metadata directory does not map to file bytes.");
        }

        return bytes.Slice((int)offset, clr.MetadataSize).ToArray();
    }

    private static List<WebcilDebugEntry> ReadDebugEntries(
        ReadOnlySpan<byte> bytes,
        long payloadOffset,
        WebcilHeader header,
        IReadOnlyList<WebcilSection> sections)
    {
        if (header.PeDebugRva == 0 || header.PeDebugSize == 0)
            return [];

        if (!TryTranslateRva(sections, payloadOffset, header.PeDebugRva, out var offset, out var available)
            || header.PeDebugSize > available
            || header.PeDebugSize % DebugDirectoryEntrySize != 0)
        {
            return [];
        }

        var count = checked((int)header.PeDebugSize / DebugDirectoryEntrySize);
        var result = new List<WebcilDebugEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var span = bytes.Slice((int)offset + i * DebugDirectoryEntrySize, DebugDirectoryEntrySize);
            var characteristics = BinaryPrimitives.ReadUInt32LittleEndian(span);
            if (characteristics != 0)
                continue;

            result.Add(new WebcilDebugEntry(
                Stamp: BinaryPrimitives.ReadUInt32LittleEndian(span[4..]),
                MajorVersion: BinaryPrimitives.ReadUInt16LittleEndian(span[8..]),
                MinorVersion: BinaryPrimitives.ReadUInt16LittleEndian(span[10..]),
                Type: (DebugDirectoryEntryType)BinaryPrimitives.ReadInt32LittleEndian(span[12..]),
                DataSize: BinaryPrimitives.ReadInt32LittleEndian(span[16..]),
                DataRva: BinaryPrimitives.ReadInt32LittleEndian(span[20..]),
                DataPointer: BinaryPrimitives.ReadInt32LittleEndian(span[24..])));
        }

        return result;
    }

    private static ClrHeader ReadClrHeader(
        WebcilHeader header,
        IReadOnlyList<WebcilSection> sections,
        ReadOnlySpan<byte> bytes,
        long payloadOffset)
    {
        if (!TryTranslateRva(sections, payloadOffset, header.PeCliHeaderRva, out var offset, out var available)
            || available < 72)
        {
            throw new BadImageFormatException("The Webcil CLR header does not map to file bytes.");
        }

        var span = bytes[(int)offset..];
        return new ClrHeader(
            MajorRuntimeVersion: BinaryPrimitives.ReadUInt16LittleEndian(span[4..]),
            MinorRuntimeVersion: BinaryPrimitives.ReadUInt16LittleEndian(span[6..]),
            MetadataRva: BinaryPrimitives.ReadInt32LittleEndian(span[8..]),
            MetadataSize: BinaryPrimitives.ReadInt32LittleEndian(span[12..]),
            Flags: (CorFlags)BinaryPrimitives.ReadUInt32LittleEndian(span[16..]),
            EntryPointToken: BinaryPrimitives.ReadInt32LittleEndian(span[20..]),
            ResourcesRva: BinaryPrimitives.ReadInt32LittleEndian(span[24..]),
            ResourcesSize: BinaryPrimitives.ReadInt32LittleEndian(span[28..]),
            StrongNameSignatureRva: BinaryPrimitives.ReadInt32LittleEndian(span[32..]),
            StrongNameSignatureSize: BinaryPrimitives.ReadInt32LittleEndian(span[36..]),
            ManagedNativeHeader: new DirectoryEntry(
                BinaryPrimitives.ReadInt32LittleEndian(span[64..]),
                BinaryPrimitives.ReadInt32LittleEndian(span[68..])));
    }

    private string FormatPayload(WebcilDebugEntry entry)
    {
        try
        {
            return entry.Type switch
            {
                DebugDirectoryEntryType.CodeView => FormatCodeViewPayload(entry),
                DebugDirectoryEntryType.Reproducible => "present",
                DebugDirectoryEntryType.PdbChecksum => FormatPdbChecksumPayload(entry),
                DebugDirectoryEntryType.EmbeddedPortablePdb => FormatEmbeddedPortablePdbPayload(entry),
                _ => ""
            };
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or ArgumentOutOfRangeException)
        {
            return $"unreadable: {ex.Message}";
        }
    }

    private string FormatCodeViewPayload(WebcilDebugEntry entry)
    {
        var data = ReadCodeView(entry);
        return $"Portable PDB; PDB GUID: {data.Guid}; age: {data.Age}; path: {data.Path}";
    }

    private string FormatPdbChecksumPayload(WebcilDebugEntry entry)
    {
        var payload = ReadEntryPayload(entry);
        var nul = payload.IndexOf((byte)0);
        if (nul < 0)
            return "";

        var algorithm = System.Text.Encoding.UTF8.GetString(payload[..nul]);
        var checksum = payload[(nul + 1)..];
        return $"Algorithm: {algorithm}; checksum: {Convert.ToHexString(checksum)}";
    }

    private string FormatEmbeddedPortablePdbPayload(WebcilDebugEntry entry)
    {
        var payload = ReadEntryPayload(entry);
        return payload.Length >= 8
            ? $"present; uncompressed size: {BinaryPrimitives.ReadInt32LittleEndian(payload[4..])} bytes"
            : "present";
    }

    private ReadOnlySpan<byte> ReadEntryPayload(WebcilDebugEntry entry)
    {
        var offset = checked(PayloadOffset + entry.DataPointer);
        if (entry.DataSize < 0 || offset < 0 || offset + entry.DataSize > _image.Length)
            throw new BadImageFormatException("The Webcil debug entry payload is out of range.");
        return _image.AsSpan((int)offset, entry.DataSize);
    }

    private bool TryTranslateRva(uint rva, out long offset, out long available) =>
        TryTranslateRva(_sections, PayloadOffset, rva, out offset, out available);

    private WebcilDebugEntry? FindDebugEntry(DebugDirectoryEntryType type)
    {
        foreach (var entry in _debugEntries)
            if (entry.Type == type)
                return entry;

        return null;
    }

    private static bool TryTranslateRva(
        IReadOnlyList<WebcilSection> sections,
        long payloadOffset,
        uint rva,
        out long offset,
        out long available)
    {
        offset = 0;
        available = 0;
        foreach (var section in sections)
        {
            if (rva < section.VirtualAddress || rva >= section.VirtualAddress + section.VirtualSize)
                continue;

            var delta = rva - section.VirtualAddress;
            if (delta >= section.SizeOfRawData)
                return false;

            offset = checked(payloadOffset + section.PointerToRawData + delta);
            available = section.SizeOfRawData - delta;
            return true;
        }

        return false;
    }

    private static byte ReadByte(ReadOnlySpan<byte> bytes, ref int pos)
    {
        if ((uint)pos >= (uint)bytes.Length)
            throw new BadImageFormatException("Unexpected end of WebAssembly data.");
        return bytes[pos++];
    }

    private static uint ReadUleb(ReadOnlySpan<byte> bytes, ref int pos)
    {
        uint result = 0;
        var shift = 0;
        while (true)
        {
            var b = ReadByte(bytes, ref pos);
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return result;
            shift += 7;
            if (shift >= 35)
                throw new BadImageFormatException("WebAssembly ULEB128 value is too large.");
        }
    }

    private static void SkipConstExpr(ReadOnlySpan<byte> bytes, ref int pos, int end)
    {
        while (pos < end)
        {
            var opcode = ReadByte(bytes, ref pos);
            switch (opcode)
            {
                case 0x0B:
                    return;
                case 0x41:
                case 0x42:
                    _ = ReadUleb(bytes, ref pos);
                    break;
                case 0x23:
                    _ = ReadUleb(bytes, ref pos);
                    break;
                default:
                    break;
            }
        }
    }

    private static string ReadUtf8NullTerminated(ReadOnlySpan<byte> bytes)
    {
        var nul = bytes.IndexOf((byte)0);
        return System.Text.Encoding.UTF8.GetString(nul >= 0 ? bytes[..nul] : bytes);
    }

    private readonly record struct WebcilHeader(
        uint Id,
        int VersionMajor,
        int VersionMinor,
        int CoffSections,
        uint PeCliHeaderRva,
        uint PeCliHeaderSize,
        uint PeDebugRva,
        uint PeDebugSize,
        uint TableBase);

    private readonly record struct WebcilSection(
        uint VirtualSize,
        uint VirtualAddress,
        uint SizeOfRawData,
        uint PointerToRawData);

}
