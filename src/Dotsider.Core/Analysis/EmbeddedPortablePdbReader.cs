using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads an embedded portable PDB with bounded, exact-length decompression.
/// </summary>
internal static class EmbeddedPortablePdbReader
{
    private const uint EmbeddedPortablePdbSignature = 0x4244504D;
    private const ushort EmbeddedPortablePdbVersion = 0x0100;
    private const ushort MinimumPortablePdbVersion = 0x0100;

    internal static MetadataReaderProvider Read(
        byte[] image,
        int payloadOffset,
        int payloadSize,
        DebugDirectoryEntryType entryType,
        ushort portablePdbVersion,
        ushort embeddedPdbVersion)
    {
        if (entryType != DebugDirectoryEntryType.EmbeddedPortablePdb)
            throw new ArgumentException("The debug entry is not an embedded portable PDB.", nameof(entryType));

        if (!TryReadHeader(
                image,
                payloadOffset,
                payloadSize,
                entryType,
                portablePdbVersion,
                embeddedPdbVersion,
                out int declaredSize,
                out string? error))
        {
            throw new BadImageFormatException(error);
        }

        int compressedLength = payloadSize - (2 * sizeof(int));
        if (!BoundedDeflateDecoder.TryDecode(
                image,
                payloadOffset + (2 * sizeof(int)),
                compressedLength,
                declaredSize,
                EmbeddedDebugDataLimits.MaxEmbeddedPortablePdbBytes
                    + EmbeddedDebugDataLimits.MaxCompressedOverheadBytes,
                EmbeddedDebugDataLimits.MaxEmbeddedPortablePdbBytes,
                out byte[] decompressed))
        {
            throw new BadImageFormatException(
                "The embedded portable PDB is malformed or its decompressed size does not match its header.");
        }

        return MetadataReaderProvider.FromPortablePdbImage(
            ImmutableCollectionsMarshal.AsImmutableArray(decompressed));
    }

    internal static bool TryReadHeader(
        byte[] image,
        int payloadOffset,
        int payloadSize,
        DebugDirectoryEntryType entryType,
        ushort portablePdbVersion,
        ushort embeddedPdbVersion,
        out int declaredSize,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(image);

        declaredSize = 0;
        error = null;
        if (entryType != DebugDirectoryEntryType.EmbeddedPortablePdb)
        {
            error = "The debug entry is not an embedded portable PDB.";
            return false;
        }

        if (portablePdbVersion < MinimumPortablePdbVersion
            || embeddedPdbVersion != EmbeddedPortablePdbVersion)
        {
            error = "The embedded portable PDB version is unsupported.";
            return false;
        }

        if (payloadOffset < 0
            || payloadSize < 0
            || payloadOffset > image.Length
            || payloadSize > image.Length - payloadOffset)
        {
            error = "The embedded portable PDB payload is out of range.";
            return false;
        }

        if (payloadSize < 2 * sizeof(int))
        {
            error = "The embedded portable PDB payload is truncated.";
            return false;
        }

        ReadOnlySpan<byte> payload = image.AsSpan(payloadOffset, payloadSize);
        if (BinaryPrimitives.ReadUInt32LittleEndian(payload) != EmbeddedPortablePdbSignature)
        {
            error = "The embedded portable PDB signature is invalid.";
            return false;
        }

        declaredSize = BinaryPrimitives.ReadInt32LittleEndian(payload[sizeof(int)..]);
        if (declaredSize <= 0)
        {
            error = "The embedded portable PDB decompressed size is invalid.";
            return false;
        }

        if (declaredSize > EmbeddedDebugDataLimits.MaxEmbeddedPortablePdbBytes)
        {
            error = "The embedded portable PDB exceeds the 256 MiB decompression limit.";
            return false;
        }

        int compressedLength = payloadSize - (2 * sizeof(int));
        if (compressedLength > EmbeddedDebugDataLimits.MaxEmbeddedPortablePdbBytes
            + EmbeddedDebugDataLimits.MaxCompressedOverheadBytes)
        {
            error = "The embedded portable PDB compressed payload exceeds the supported limit.";
            return false;
        }

        return true;
    }
}
