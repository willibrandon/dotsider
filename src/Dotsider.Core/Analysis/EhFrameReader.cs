using System.Buffers.Binary;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Recovers function boundaries from an ELF image's <c>.eh_frame</c> section — the symbol-free
/// fallback. Each FDE names its function's start (<c>initial_location</c>) and byte length
/// (<c>address_range</c>), encoded per its CIE's augmentation-declared pointer encoding;
/// absolute and PC-relative applications of the fixed, LEB128, and signed/unsigned data formats
/// are decoded, and entries with encodings beyond that (indirect, text/data-relative) are
/// skipped rather than misread. Malformed data yields the boundaries parsed before the damage.
/// </summary>
internal static class EhFrameReader
{
    private const int MaxEntries = 262_144;

    // DW_EH_PE_* pointer-encoding format (low nibble) and application (high nibble).
    private const byte PeOmit = 0xFF;
    private const byte PeFormatMask = 0x0F;
    private const byte PeApplicationMask = 0x70;
    private const byte PeIndirect = 0x80;
    private const byte PePcrel = 0x10;

    /// <summary>
    /// Walks <c>.eh_frame</c> into nameless boundary symbols, mapping each to its containing
    /// section for file offsets.
    /// </summary>
    /// <param name="imageBytes">The raw image bytes.</param>
    public static IReadOnlyList<RawNativeSymbol> ReadBoundaries(ReadOnlySpan<byte> imageBytes)
    {
        var result = new List<RawNativeSymbol>();
        var sections = ElfImageReader.ReadSections(imageBytes);
        if (!ElfImageReader.TryGetSection(imageBytes, ".eh_frame", out var ehFrame)) return result;
        if (!NativeImageRange.TryGet(
            imageBytes.Length,
            ehFrame.FileOffset,
            ehFrame.Size,
            out var sectionOffset,
            out var sectionSize))
            return result;

        var data = imageBytes.Slice(sectionOffset, sectionSize);
        var cieEncodings = new Dictionary<long, byte>();

        try
        {
            var position = 0;
            for (var entries = 0; entries < MaxEntries && position <= data.Length - 4; entries++)
            {
                var entryStart = position;
                ulong length = BinaryPrimitives.ReadUInt32LittleEndian(data[position..]);
                position += 4;
                if (length == 0) break; // terminator
                if (length == 0xFFFF_FFFF)
                {
                    if (position > data.Length - sizeof(ulong)) break;
                    length = BinaryPrimitives.ReadUInt64LittleEndian(data[position..]);
                    position += 8;
                }

                if (length < 4
                    || !NativeImageRange.TryGet(
                        data.Length,
                        (ulong)position,
                        length,
                        out _,
                        out var entryLength))
                    break;

                var next = position + entryLength;

                var idPosition = position;
                var id = BinaryPrimitives.ReadUInt32LittleEndian(data[position..]);
                position += 4;

                if (id == 0)
                {
                    cieEncodings[entryStart] = ReadCieEncoding(data, position, (int)next);
                }
                else if (cieEncodings.TryGetValue(idPosition - id, out var encoding)
                    && TryReadFdeBoundary(data, position, encoding, ehFrame.Address, out var va, out var size)
                    && va != 0 && size > 0)
                {
                    result.Add(Boundary(va, size, sections));
                }

                position = next;
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Keep the boundaries parsed before the damage.
        }

        return result;
    }

    /// <summary>Parses a CIE far enough to learn its FDE pointer encoding (the 'R' augmentation).</summary>
    private static byte ReadCieEncoding(ReadOnlySpan<byte> data, int position, int end)
    {
        const byte absptr = 0x00;
        var version = data[position++];
        if (version is not (1 or 3 or 4)) return absptr;

        var augmentationStart = position;
        while (position < end && data[position] != 0) position++;
        var augmentation = data[augmentationStart..position];
        position++; // NUL

        if (version == 4) position += 2; // address_size, segment_selector_size
        SkipUleb(data, ref position); // code_alignment_factor
        SkipUleb(data, ref position); // data_alignment_factor (SLEB shares the wire shape)
        if (version == 1) position++; // return_address_register
        else SkipUleb(data, ref position);

        if (augmentation.Length == 0 || augmentation[0] != (byte)'z') return absptr;
        SkipUleb(data, ref position); // augmentation data length

        foreach (var ch in augmentation[1..])
        {
            switch ((char)ch)
            {
                case 'L':
                    position++; // LSDA encoding
                    break;
                case 'P':
                    {
                        var personalityEncoding = data[position++];
                        DecodePointer(data, ref position, personalityEncoding, fieldVa: 0); // skip the pointer
                        break;
                    }

                case 'R':
                    return data[position];
                case 'S' or 'B' or 'G':
                    break; // no data
                default:
                    return absptr; // unknown augmentation: stop guessing, 'z' covers the skip
            }
        }

        return absptr;
    }

    private static bool TryReadFdeBoundary(
        ReadOnlySpan<byte> data, int position, byte encoding, ulong sectionAddress,
        out ulong va, out ulong size)
    {
        va = 0;
        size = 0;
        if (encoding == PeOmit || (encoding & PeIndirect) != 0) return false;
        var application = encoding & PeApplicationMask;
        if (application is not (0 or PePcrel)) return false; // text/data/func-relative: no base here

        if (!NativeImageRange.TryAdd(sectionAddress, (ulong)position, out var fieldVa))
            return false;
        var initialLocation = DecodePointer(data, ref position, encoding, fieldVa);
        if (initialLocation is not { } location) return false;

        // The range is a length: same format, no application.
        var range = DecodePointer(data, ref position, (byte)(encoding & PeFormatMask), fieldVa: 0);
        if (range is not { } r
            || r > long.MaxValue
            || !NativeImageRange.TryAdd(location, r, out _))
            return false;

        va = location;
        size = r;
        return true;
    }

    /// <summary>Decodes one encoded pointer, applying PC-relative adjustment when asked for.</summary>
    private static ulong? DecodePointer(ReadOnlySpan<byte> data, ref int position, byte encoding, ulong fieldVa)
    {
        if (encoding == PeOmit) return null;

        ulong value;
        switch (encoding & PeFormatMask)
        {
            case 0x00: // absptr (64-bit images)
                value = BinaryPrimitives.ReadUInt64LittleEndian(data[position..]);
                position += 8;
                break;
            case 0x01: // uleb128
                value = ReadUleb(data, ref position);
                break;
            case 0x02: // udata2
                value = BinaryPrimitives.ReadUInt16LittleEndian(data[position..]);
                position += 2;
                break;
            case 0x03: // udata4
                value = BinaryPrimitives.ReadUInt32LittleEndian(data[position..]);
                position += 4;
                break;
            case 0x04: // udata8
                value = BinaryPrimitives.ReadUInt64LittleEndian(data[position..]);
                position += 8;
                break;
            case 0x09: // sleb128
                value = (ulong)ReadSleb(data, ref position);
                break;
            case 0x0A: // sdata2
                value = (ulong)(long)BinaryPrimitives.ReadInt16LittleEndian(data[position..]);
                position += 2;
                break;
            case 0x0B: // sdata4
                value = (ulong)(long)BinaryPrimitives.ReadInt32LittleEndian(data[position..]);
                position += 4;
                break;
            case 0x0C: // sdata8
                value = (ulong)BinaryPrimitives.ReadInt64LittleEndian(data[position..]);
                position += 8;
                break;
            default:
                return null;
        }

        if ((encoding & PeApplicationMask) != PePcrel) return value;

        var format = encoding & PeFormatMask;
        if (format is not (0x09 or 0x0A or 0x0B or 0x0C))
            return NativeImageRange.TryAdd(fieldVa, value, out var unsignedAddress)
                ? unsignedAddress
                : null;

        var signedValue = unchecked((long)value);
        if (signedValue >= 0)
            return NativeImageRange.TryAdd(fieldVa, (ulong)signedValue, out var positiveAddress)
                ? positiveAddress
                : null;

        var magnitude = (ulong)(-(signedValue + 1)) + 1;
        return magnitude <= fieldVa ? fieldVa - magnitude : null;
    }

    private static ulong ReadUleb(ReadOnlySpan<byte> data, ref int position)
    {
        ulong result = 0;
        var shift = 0;
        while (true)
        {
            var b = data[position++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
            if (shift > 63) throw new ArgumentOutOfRangeException(nameof(position), "LEB128 too long");
        }
    }

    private static long ReadSleb(ReadOnlySpan<byte> data, ref int position)
    {
        long result = 0;
        var shift = 0;
        byte b;
        do
        {
            b = data[position++];
            result |= (long)(b & 0x7F) << shift;
            shift += 7;
            if (shift > 70) throw new ArgumentOutOfRangeException(nameof(position), "LEB128 too long");
        }
        while ((b & 0x80) != 0);

        if (shift < 64 && (b & 0x40) != 0)
            result |= -1L << shift;
        return result;
    }

    private static void SkipUleb(ReadOnlySpan<byte> data, ref int position)
    {
        while ((data[position++] & 0x80) != 0)
        {
        }
    }

    private static RawNativeSymbol Boundary(
        ulong va, ulong size, IReadOnlyList<ElfSection> sections)
    {
        var mapped = ElfImageReader.TryMapAddress(sections, va, out var name, out var offset);
        string? sectionName = mapped ? name : null;
        long? fileOffset = mapped ? offset : null;

        return new RawNativeSymbol(
            Name: $"sub_{va:x}",
            VirtualAddress: va,
            Rva: null,
            FileOffset: fileOffset,
            Section: sectionName,
            Size: (long)size,
            IsData: false,
            IsBoundary: true,
            SourceFile: null,
            Line: null);
    }
}
