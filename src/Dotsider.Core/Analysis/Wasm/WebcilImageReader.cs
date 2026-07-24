using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Collections.Immutable;
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
    private const int ClrHeaderSize = 72;
    private const int DebugDirectoryEntrySize = 28;
    private const int HeaderV0Size = 28;
    private const int HeaderV1Size = 32;
    private const int MaxSections = 16;
    private const int SectionHeaderSize = 16;
    private const uint WasmMagic = 0x6D736100;
    private const uint WasmVersion = 1;
    private const uint WebcilMagic = 0x4C496257;

    private readonly List<WebcilDebugEntry> _debugEntries;
    private readonly byte[] _image;
    private readonly byte[] _metadataBytes;
    private readonly List<WebcilSection> _sections;

    private WebcilImageReader(
        byte[] image,
        int payloadOffset,
        WebcilHeader header,
        List<WebcilSection> sections,
        ClrHeader clrHeader,
        byte[] metadataBytes,
        List<WebcilDebugEntry> debugEntries)
    {
        _debugEntries = debugEntries;
        _image = image;
        _metadataBytes = metadataBytes;
        _sections = sections;
        PayloadOffset = payloadOffset;
        ClrHeader = clrHeader;
        Info = new WebcilInfo(
            header.VersionMajor,
            header.VersionMinor,
            payloadOffset > 0,
            payloadOffset,
            sections.Count,
            metadataBytes.Length,
            header.PeDebugRva == 0 || header.PeDebugSize == 0
                ? 0
                : checked((int)header.PeDebugSize));
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
    /// Opens a bare or WebAssembly-wrapped Webcil image.
    /// </summary>
    /// <param name="bytes">The candidate file bytes.</param>
    /// <returns>The parsed reader, or <see langword="null"/> when no Webcil payload exists.</returns>
    /// <exception cref="BadImageFormatException">
    /// A Webcil payload was recognized, but its headers, sections, or managed directories are malformed.
    /// </exception>
    public static WebcilImageReader? Open(ReadOnlySpan<byte> bytes)
    {
        if (!TryFindPayload(bytes, out int payloadOffset, out int payloadLength))
            return null;

        ReadOnlySpan<byte> payload = bytes.Slice(payloadOffset, payloadLength);
        WebcilHeader header = ReadHeader(payload);
        List<WebcilSection> sections = ReadSectionTable(payload, header);
        ClrHeader clrHeader = ReadClrHeader(header, sections, payload);
        ValidateClrDirectories(clrHeader, sections, payload.Length);
        byte[] metadataBytes = ReadMetadata(payload, clrHeader, sections);
        List<WebcilDebugEntry> debugEntries = ReadDebugEntries(payload, header, sections);
        byte[] image = GC.AllocateUninitializedArray<byte>(payloadLength, pinned: true);
        payload.CopyTo(image);

        return new WebcilImageReader(
            image,
            payloadOffset,
            header,
            sections,
            clrHeader,
            metadataBytes,
            debugEntries);
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
        if (rva <= 0 || !TryTranslateRva((uint)rva, out int offset, out int available) || available == 0)
            return null;

        fixed (byte* image = _image)
        {
            BlobReader blob = new(image + offset, available);
            return MethodBodyBlock.Create(blob);
        }
    }

    /// <summary>
    /// Converts Webcil sections to dotsider's generic section table rows.
    /// </summary>
    /// <returns>Generic section rows with Webcil-adjusted raw data offsets.</returns>
    public IReadOnlyList<SectionInfo> ReadSections() =>
        [.. _sections.Select((section, index) => new SectionInfo(
            Name: $"webcil-section-{index}",
            VirtualAddress: unchecked((int)section.VirtualAddress),
            VirtualSize: unchecked((int)section.VirtualSize),
            RawDataOffset: checked(PayloadOffset + (int)section.PointerToRawData),
            RawDataSize: (int)section.SizeOfRawData,
            Characteristics: 0))];

    /// <summary>
    /// Converts Webcil debug directory entries to dotsider display rows.
    /// </summary>
    /// <returns>Debug directory rows with formatted Webcil payload details.</returns>
    public IReadOnlyList<DebugDirectoryInfo> ReadDebugDirectory() =>
        [.. _debugEntries.Select(entry => new DebugDirectoryInfo(
            Type: entry.Type,
            Stamp: entry.Stamp,
            MajorVersion: entry.MajorVersion,
            MinorVersion: entry.MinorVersion,
            DataSize: entry.DataSize,
            AddressOfRawData: entry.DataRva,
            PointerToRawData: checked(PayloadOffset + entry.DataPointer),
            Payload: FormatPayload(entry)))];

    /// <summary>
    /// Opens an embedded portable PDB from a Webcil debug directory entry.
    /// </summary>
    /// <param name="entry">The embedded portable PDB debug directory entry.</param>
    /// <returns>A metadata provider over the decompressed portable PDB image.</returns>
    public MetadataReaderProvider ReadEmbeddedPortablePdb(WebcilDebugEntry entry)
    {
        return EmbeddedPortablePdbReader.Read(
            _image,
            entry.DataPointer,
            entry.DataSize,
            entry.Type,
            entry.MajorVersion,
            entry.MinorVersion);
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
        ReadOnlySpan<byte> payload = ReadEntryPayload(entry);
        if (payload.Length < 24
            || payload[0] != (byte)'R'
            || payload[1] != (byte)'S'
            || payload[2] != (byte)'D'
            || payload[3] != (byte)'S')
        {
            throw new BadImageFormatException("Unexpected CodeView payload signature.");
        }

        Guid guid = new(payload[4..20]);
        int age = BinaryPrimitives.ReadInt32LittleEndian(payload[20..]);
        string path = ReadUtf8NullTerminated(payload[24..]);
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
        if (rva == 0
            || relativeOffset < 0
            || !TryTranslateRva((uint)rva, out int offset, out int available)
            || available < sizeof(int)
            || relativeOffset > available - sizeof(int))
        {
            return false;
        }

        value = BinaryPrimitives.ReadInt32LittleEndian(_image.AsSpan(offset + relativeOffset, sizeof(int)));
        return true;
    }

    private int PayloadOffset { get; }

    private static WebcilHeader ReadHeader(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderV0Size)
            throw new BadImageFormatException("The Webcil header is truncated.");

        uint id = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        ushort versionMajor = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..]);
        ushort versionMinor = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]);
        ushort coffSections = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]);

        if (id != WebcilMagic)
            throw new BadImageFormatException("The Webcil signature is invalid.");
        if (versionMajor is not (0 or 1) || versionMinor != 0)
            throw new BadImageFormatException("The Webcil version is unsupported.");
        if (coffSections is 0 or > MaxSections)
            throw new BadImageFormatException("The Webcil section count is invalid.");

        int headerSize = versionMajor == 0 ? HeaderV0Size : HeaderV1Size;
        int sectionTableSize = coffSections * SectionHeaderSize;
        if (payload.Length < headerSize || sectionTableSize > payload.Length - headerSize)
            throw new BadImageFormatException("The Webcil section table is truncated.");

        return new WebcilHeader(
            Id: id,
            VersionMajor: versionMajor,
            VersionMinor: versionMinor,
            CoffSections: coffSections,
            PeCliHeaderRva: BinaryPrimitives.ReadUInt32LittleEndian(payload[12..]),
            PeCliHeaderSize: BinaryPrimitives.ReadUInt32LittleEndian(payload[16..]),
            PeDebugRva: BinaryPrimitives.ReadUInt32LittleEndian(payload[20..]),
            PeDebugSize: BinaryPrimitives.ReadUInt32LittleEndian(payload[24..]),
            TableBase: versionMajor == 0
                ? uint.MaxValue
                : BinaryPrimitives.ReadUInt32LittleEndian(payload[HeaderV0Size..]));
    }

    private static List<WebcilSection> ReadSectionTable(ReadOnlySpan<byte> payload, WebcilHeader header)
    {
        int sectionOffset = header.VersionMajor == 0 ? HeaderV0Size : HeaderV1Size;
        List<WebcilSection> sections = new(header.CoffSections);
        for (int index = 0; index < header.CoffSections; index++)
        {
            int offset = sectionOffset + index * SectionHeaderSize;
            ReadOnlySpan<byte> span = payload.Slice(offset, SectionHeaderSize);
            WebcilSection section = new(
                VirtualSize: BinaryPrimitives.ReadUInt32LittleEndian(span),
                VirtualAddress: BinaryPrimitives.ReadUInt32LittleEndian(span[4..]),
                SizeOfRawData: BinaryPrimitives.ReadUInt32LittleEndian(span[8..]),
                PointerToRawData: BinaryPrimitives.ReadUInt32LittleEndian(span[12..]));

            ValidateSection(section, payload.Length);
            foreach (WebcilSection previous in sections)
            {
                uint sectionEnd = section.VirtualAddress + section.VirtualSize;
                uint previousEnd = previous.VirtualAddress + previous.VirtualSize;
                if (sectionEnd > previous.VirtualAddress && previousEnd > section.VirtualAddress)
                    throw new BadImageFormatException("Webcil sections overlap in virtual address space.");
            }

            sections.Add(section);
        }

        return sections;
    }

    private static void ValidateSection(WebcilSection section, int payloadLength)
    {
        if (section.PointerToRawData > (uint)payloadLength
            || section.SizeOfRawData > (uint)payloadLength - section.PointerToRawData)
        {
            throw new BadImageFormatException("A Webcil section extends past the end of its payload.");
        }

        if (section.VirtualSize > uint.MaxValue - section.VirtualAddress)
            throw new BadImageFormatException("A Webcil section's virtual address range overflows.");
    }

    private static ClrHeader ReadClrHeader(
        WebcilHeader header,
        IReadOnlyList<WebcilSection> sections,
        ReadOnlySpan<byte> payload)
    {
        if (header.PeCliHeaderRva == 0
            || header.PeCliHeaderSize < ClrHeaderSize
            || !TryTranslateRva(
                sections,
                payload.Length,
                header.PeCliHeaderRva,
                out int offset,
                out int available)
            || available < ClrHeaderSize)
        {
            throw new BadImageFormatException("The Webcil CLR header does not map to file bytes.");
        }

        ReadOnlySpan<byte> span = payload.Slice(offset, ClrHeaderSize);
        uint clrHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(span);
        if (clrHeaderSize < ClrHeaderSize)
            throw new BadImageFormatException("The Webcil CLR header is truncated.");

        ushort majorRuntimeVersion = BinaryPrimitives.ReadUInt16LittleEndian(span[4..]);
        CorFlags flags = (CorFlags)BinaryPrimitives.ReadUInt32LittleEndian(span[16..]);
        if (majorRuntimeVersion is <= 1 or > 2)
            throw new BadImageFormatException("The Webcil CLR runtime version is unsupported.");
        if ((flags & CorFlags.NativeEntryPoint) != 0)
            throw new BadImageFormatException("Webcil does not support native entry points.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(span[52..]) != 0)
            throw new BadImageFormatException("Webcil does not support CLR vtable fixups.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(span[60..]) != 0)
            throw new BadImageFormatException("Webcil does not support export address table jumps.");

        return new ClrHeader(
            MajorRuntimeVersion: majorRuntimeVersion,
            MinorRuntimeVersion: BinaryPrimitives.ReadUInt16LittleEndian(span[6..]),
            MetadataRva: BinaryPrimitives.ReadInt32LittleEndian(span[8..]),
            MetadataSize: BinaryPrimitives.ReadInt32LittleEndian(span[12..]),
            Flags: flags,
            EntryPointToken: BinaryPrimitives.ReadInt32LittleEndian(span[20..]),
            ResourcesRva: BinaryPrimitives.ReadInt32LittleEndian(span[24..]),
            ResourcesSize: BinaryPrimitives.ReadInt32LittleEndian(span[28..]),
            StrongNameSignatureRva: BinaryPrimitives.ReadInt32LittleEndian(span[32..]),
            StrongNameSignatureSize: BinaryPrimitives.ReadInt32LittleEndian(span[36..]),
            ManagedNativeHeader: new DirectoryEntry(
                BinaryPrimitives.ReadInt32LittleEndian(span[64..]),
                BinaryPrimitives.ReadInt32LittleEndian(span[68..])));
    }

    private static void ValidateClrDirectories(
        ClrHeader clrHeader,
        IReadOnlyList<WebcilSection> sections,
        int payloadLength)
    {
        ValidateDirectory(
            sections,
            payloadLength,
            clrHeader.MetadataRva,
            clrHeader.MetadataSize,
            required: true,
            "metadata");
        ValidateDirectory(
            sections,
            payloadLength,
            clrHeader.ResourcesRva,
            clrHeader.ResourcesSize,
            required: false,
            "resources");
        ValidateDirectory(
            sections,
            payloadLength,
            clrHeader.StrongNameSignatureRva,
            clrHeader.StrongNameSignatureSize,
            required: false,
            "strong-name signature");
        ValidateDirectory(
            sections,
            payloadLength,
            clrHeader.ManagedNativeHeader.RelativeVirtualAddress,
            clrHeader.ManagedNativeHeader.Size,
            required: false,
            "managed native header");

        if ((clrHeader.Flags & CorFlags.StrongNameSigned) != 0
            && clrHeader.StrongNameSignatureRva == 0)
        {
            throw new BadImageFormatException(
                "The Webcil CLR header marks the image as strong-name signed without a signature.");
        }
    }

    private static void ValidateDirectory(
        IReadOnlyList<WebcilSection> sections,
        int payloadLength,
        int rva,
        int size,
        bool required,
        string name)
    {
        if (rva == 0)
        {
            if (required)
                throw new BadImageFormatException($"The Webcil {name} directory is invalid.");
            return;
        }

        if (size < 0
            || !TryTranslateRva(sections, payloadLength, (uint)rva, out _, out int available)
            || size > available)
        {
            throw new BadImageFormatException($"The Webcil {name} directory does not map to file bytes.");
        }
    }

    private static byte[] ReadMetadata(
        ReadOnlySpan<byte> payload,
        ClrHeader clrHeader,
        IReadOnlyList<WebcilSection> sections)
    {
        if (!TryTranslateRva(
                sections,
                payload.Length,
                (uint)clrHeader.MetadataRva,
                out int offset,
                out int available)
            || clrHeader.MetadataSize <= 0
            || clrHeader.MetadataSize > available)
        {
            throw new BadImageFormatException("The Webcil metadata directory does not map to file bytes.");
        }

        return payload.Slice(offset, clrHeader.MetadataSize).ToArray();
    }

    private static List<WebcilDebugEntry> ReadDebugEntries(
        ReadOnlySpan<byte> payload,
        WebcilHeader header,
        IReadOnlyList<WebcilSection> sections)
    {
        if (header.PeDebugRva == 0 || header.PeDebugSize == 0)
            return [];

        if (header.PeDebugSize % DebugDirectoryEntrySize != 0
            || !TryTranslateRva(
                sections,
                payload.Length,
                header.PeDebugRva,
                out int offset,
                out int available)
            || header.PeDebugSize > (uint)available)
        {
            throw new BadImageFormatException("The Webcil debug directory does not map to file bytes.");
        }

        int debugSize = (int)header.PeDebugSize;
        int count = debugSize / DebugDirectoryEntrySize;
        List<WebcilDebugEntry> result = new(count);
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> span = payload.Slice(
                offset + index * DebugDirectoryEntrySize,
                DebugDirectoryEntrySize);
            if (BinaryPrimitives.ReadUInt32LittleEndian(span) != 0)
                throw new BadImageFormatException("Webcil debug-directory characteristics must be zero.");

            uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(span[16..]);
            uint dataRva = BinaryPrimitives.ReadUInt32LittleEndian(span[20..]);
            uint dataPointer = BinaryPrimitives.ReadUInt32LittleEndian(span[24..]);
            ValidatePayloadRange(
                dataPointer,
                dataSize,
                payload.Length,
                "The Webcil debug payload is out of range.");

            result.Add(new WebcilDebugEntry(
                Stamp: BinaryPrimitives.ReadUInt32LittleEndian(span[4..]),
                MajorVersion: BinaryPrimitives.ReadUInt16LittleEndian(span[8..]),
                MinorVersion: BinaryPrimitives.ReadUInt16LittleEndian(span[10..]),
                Type: (DebugDirectoryEntryType)BinaryPrimitives.ReadInt32LittleEndian(span[12..]),
                DataSize: (int)dataSize,
                DataRva: unchecked((int)dataRva),
                DataPointer: (int)dataPointer));
        }

        return result;
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
        WebcilCodeViewData data = ReadCodeView(entry);
        return $"Portable PDB; PDB GUID: {data.Guid}; age: {data.Age}; path: {data.Path}";
    }

    private string FormatPdbChecksumPayload(WebcilDebugEntry entry)
    {
        ReadOnlySpan<byte> payload = ReadEntryPayload(entry);
        int nul = payload.IndexOf((byte)0);
        if (nul < 0)
            return "";

        string algorithm = System.Text.Encoding.UTF8.GetString(payload[..nul]);
        ReadOnlySpan<byte> checksum = payload[(nul + 1)..];
        return $"Algorithm: {algorithm}; checksum: {Convert.ToHexString(checksum)}";
    }

    private string FormatEmbeddedPortablePdbPayload(WebcilDebugEntry entry)
    {
        return EmbeddedPortablePdbReader.TryReadHeader(
            _image,
            entry.DataPointer,
            entry.DataSize,
            entry.Type,
            entry.MajorVersion,
            entry.MinorVersion,
            out int declaredSize,
            out string? error)
            ? $"present; uncompressed size: {declaredSize} bytes"
            : $"unreadable: {error}";
    }

    private ReadOnlySpan<byte> ReadEntryPayload(WebcilDebugEntry entry)
    {
        ValidatePayloadRange(
            entry.DataPointer,
            entry.DataSize,
            _image.Length,
            "The Webcil debug entry payload is out of range.");
        return _image.AsSpan(entry.DataPointer, entry.DataSize);
    }

    private bool TryTranslateRva(uint rva, out int offset, out int available) =>
        TryTranslateRva(_sections, _image.Length, rva, out offset, out available);

    private WebcilDebugEntry? FindDebugEntry(DebugDirectoryEntryType type)
    {
        foreach (WebcilDebugEntry entry in _debugEntries)
            if (entry.Type == type)
                return entry;

        return null;
    }

    private static bool TryTranslateRva(
        IReadOnlyList<WebcilSection> sections,
        int payloadLength,
        uint rva,
        out int offset,
        out int available)
    {
        offset = 0;
        available = 0;
        foreach (WebcilSection section in sections)
        {
            if (rva < section.VirtualAddress)
                continue;

            uint delta = rva - section.VirtualAddress;
            if (delta >= section.VirtualSize || delta >= section.SizeOfRawData)
                continue;

            uint rawOffset = section.PointerToRawData + delta;
            if (rawOffset > (uint)payloadLength)
                return false;

            uint virtualAvailable = section.VirtualSize - delta;
            uint rawAvailable = section.SizeOfRawData - delta;
            uint payloadAvailable = (uint)payloadLength - rawOffset;
            offset = (int)rawOffset;
            available = (int)Math.Min(virtualAvailable, Math.Min(rawAvailable, payloadAvailable));
            return true;
        }

        return false;
    }

    private static bool TryFindPayload(
        ReadOnlySpan<byte> bytes,
        out int payloadOffset,
        out int payloadLength)
    {
        payloadOffset = 0;
        payloadLength = 0;
        if (bytes.Length < sizeof(uint))
            return false;

        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) == WebcilMagic)
        {
            payloadLength = bytes.Length;
            return true;
        }

        if (bytes.Length < 8
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != WasmMagic
            || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != WasmVersion)
        {
            return false;
        }

        bool recognized = false;
        try
        {
            int position = 8;
            while (position < bytes.Length)
            {
                byte sectionId = ReadByte(bytes, ref position, bytes.Length);
                uint sectionSize = ReadUleb32(bytes, ref position, bytes.Length);
                bool sectionTruncated = sectionSize > (uint)(bytes.Length - position);
                int sectionEnd = sectionTruncated ? bytes.Length : position + (int)sectionSize;
                if (sectionId != 11)
                {
                    if (sectionTruncated)
                        return false;
                    position = sectionEnd;
                    continue;
                }

                uint segmentCount = ReadUleb32(bytes, ref position, sectionEnd);
                for (uint index = 0; index < segmentCount; index++)
                {
                    uint mode = ReadUleb32(bytes, ref position, sectionEnd);
                    if (mode == 0)
                    {
                        SkipConstExpr(bytes, ref position, sectionEnd);
                    }
                    else if (mode == 2)
                    {
                        _ = ReadUleb32(bytes, ref position, sectionEnd);
                        SkipConstExpr(bytes, ref position, sectionEnd);
                    }
                    else if (mode != 1)
                    {
                        return false;
                    }

                    uint size = ReadUleb32(bytes, ref position, sectionEnd);
                    recognized = size >= sizeof(uint)
                        && position <= sectionEnd - sizeof(uint)
                        && BinaryPrimitives.ReadUInt32LittleEndian(bytes[position..]) == WebcilMagic;
                    if (recognized && sectionTruncated)
                    {
                        throw new BadImageFormatException(
                            "The WebAssembly data section containing Webcil extends past the file.");
                    }
                    if (size > (uint)(sectionEnd - position))
                    {
                        if (recognized)
                            throw new BadImageFormatException(
                                "The wrapped Webcil payload extends past its data segment.");
                        return false;
                    }

                    if (recognized)
                    {
                        payloadOffset = position;
                        payloadLength = (int)size;
                        return true;
                    }

                    position += (int)size;
                }

                return false;
            }
        }
        catch (Exception ex) when (
            !recognized
            && ex is BadImageFormatException or OverflowException or ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (Exception ex) when (
            recognized
            && ex is OverflowException or ArgumentOutOfRangeException)
        {
            throw new BadImageFormatException("The wrapped Webcil payload is malformed.", ex);
        }

        return false;
    }

    private static uint ReadUleb32(ReadOnlySpan<byte> bytes, ref int position, int end)
    {
        uint value = 0;
        for (int index = 0; index < 5; index++)
        {
            byte current = ReadByte(bytes, ref position, end);
            if (index == 4 && (current & 0xF0) != 0)
                throw new BadImageFormatException("A WebAssembly ULEB128 value is too large.");

            value |= (uint)(current & 0x7F) << (index * 7);
            if ((current & 0x80) == 0)
                return value;
        }

        throw new BadImageFormatException("A WebAssembly ULEB128 value is too large.");
    }

    private static byte ReadByte(ReadOnlySpan<byte> bytes, ref int position, int end)
    {
        if ((uint)position >= (uint)end || (uint)position >= (uint)bytes.Length)
            throw new BadImageFormatException("Unexpected end of WebAssembly data.");
        return bytes[position++];
    }

    private static void SkipConstExpr(ReadOnlySpan<byte> bytes, ref int position, int end)
    {
        while (position < end)
        {
            byte opcode = ReadByte(bytes, ref position, end);
            switch (opcode)
            {
                case 0x0B:
                    return;
                case 0x41:
                    SkipLeb128(bytes, ref position, end, 5);
                    break;
                case 0x42:
                    SkipLeb128(bytes, ref position, end, 10);
                    break;
                case 0x43:
                    SkipBytes(ref position, end, sizeof(float));
                    break;
                case 0x44:
                    SkipBytes(ref position, end, sizeof(double));
                    break;
                case 0x23:
                case 0xD2:
                    _ = ReadUleb32(bytes, ref position, end);
                    break;
                case 0xD0:
                    SkipLeb128(bytes, ref position, end, 5);
                    break;
            }
        }

        throw new BadImageFormatException("A WebAssembly constant expression is unterminated.");
    }

    private static void SkipLeb128(ReadOnlySpan<byte> bytes, ref int position, int end, int maximumBytes)
    {
        for (int index = 0; index < maximumBytes; index++)
            if ((ReadByte(bytes, ref position, end) & 0x80) == 0)
                return;

        throw new BadImageFormatException("A WebAssembly LEB128 value is too large.");
    }

    private static void SkipBytes(ref int position, int end, int count)
    {
        if (position > end - count)
            throw new BadImageFormatException("Unexpected end of WebAssembly data.");
        position += count;
    }

    private static void ValidatePayloadRange(int offset, int size, int payloadLength, string message)
    {
        if (offset < 0 || size < 0 || offset > payloadLength || size > payloadLength - offset)
            throw new BadImageFormatException(message);
    }

    private static void ValidatePayloadRange(uint offset, uint size, int payloadLength, string message)
    {
        if (offset > (uint)payloadLength || size > (uint)payloadLength - offset)
            throw new BadImageFormatException(message);
    }

    private static string ReadUtf8NullTerminated(ReadOnlySpan<byte> bytes)
    {
        int nul = bytes.IndexOf((byte)0);
        return System.Text.Encoding.UTF8.GetString(nul >= 0 ? bytes[..nul] : bytes);
    }
}
