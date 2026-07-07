using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Rehydrates a Native AOT binary's dehydrated data section (ReadyToRun section 207) to
/// reconstruct the frozen object region, which on ELF and zero-fill Mach-O layouts the
/// runtime rebuilds at startup rather than storing on disk. The command encoding is ported
/// clean-room from the MIT runtime source (<c>DehydratedDataCommand</c> /
/// <c>StartupCodeHelpers.RehydrateData</c>). Only the copied literal data is reproduced —
/// pointer commands merely advance the cursor — which is all the frozen string scan needs;
/// their targets use platform-specific encodings that carry no string content. Malformed
/// data yields null.
/// </summary>
internal static class DehydratedDataReader
{
    private const int CommandMask = 0x07;
    private const int CommandPayloadShift = 3;
    private const int MaxShortPayload = 28;

    private const int Copy = 0x00;
    private const int ZeroFill = 0x01;
    private const int RelPtr32Reloc = 0x02;
    private const int PtrReloc = 0x03;
    private const int InlineRelPtr32Reloc = 0x04;
    private const int InlinePtrReloc = 0x05;

    /// <summary>
    /// Reconstructs the frozen object region's literal data from the dehydrated data section.
    /// </summary>
    /// <param name="image">The raw image bytes.</param>
    /// <param name="dehydrated">The dehydrated data section (207), which must be file-backed.</param>
    /// <param name="frozenRegion">The frozen object region section (206) being reconstructed.</param>
    /// <param name="addressSpace">The image's address map, for its pointer size.</param>
    /// <returns>The rebuilt region bytes, or null when the data cannot be rehydrated.</returns>
    internal static byte[]? Rehydrate(
        ReadOnlySpan<byte> image,
        RtrSection dehydrated,
        RtrSection frozenRegion,
        NativeAddressSpace addressSpace)
    {
        if (ReadyToRunReader.FileRange(dehydrated) is not { } range) return null;
        var (sectionOffset, sectionLength) = range;
        if (sectionOffset + sectionLength > image.Length || sectionLength < 8) return null;
        if (frozenRegion.Size is <= 0 or > (1 << 28)) return null;

        var src = image.Slice(sectionOffset, sectionLength);
        var pointerSize = addressSpace.PointerSize;

        // The command stream reconstructs memory starting at this destination base; the
        // frozen region begins there or a little past it.
        var destBaseVa = dehydrated.VirtualAddress + (ulong)BinaryPrimitives.ReadInt32LittleEndian(src);
        if (frozenRegion.VirtualAddress < destBaseVa) return null;
        var destLength = (long)(frozenRegion.VirtualAddress - destBaseVa) + frozenRegion.Size;
        if (destLength is <= 0 or > (1 << 28)) return null;

        var dest = new byte[destLength];
        var destPos = 0;
        var p = 8; // past the destination rel32 and the length field
        try
        {
            while (destPos < dest.Length && p < src.Length)
            {
                var payload = DecodeCommand(src, ref p, out var command);
                switch (command)
                {
                    case Copy:
                        var copy = Math.Min(payload, dest.Length - destPos);
                        if (p + payload > src.Length) return dest;
                        src.Slice(p, copy).CopyTo(dest.AsSpan(destPos));
                        p += payload;
                        destPos += payload;
                        break;

                    case ZeroFill:
                        destPos += payload; // dest is already zero-initialized
                        break;

                    case PtrReloc:
                        destPos += pointerSize;
                        break;

                    case RelPtr32Reloc:
                        destPos += 4;
                        break;

                    case InlinePtrReloc:
                        p += payload * 4;
                        destPos += payload * pointerSize;
                        break;

                    case InlineRelPtr32Reloc:
                        p += payload * 4;
                        destPos += payload * 4;
                        break;

                    default:
                        return dest; // unknown command — stop with what was rebuilt
                }
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Return the partial reconstruction.
        }

        return dest;
    }

    /// <summary>
    /// Decodes a command byte and its payload. The payload is the high 5 bits, extended by
    /// up to three little-endian bytes when it exceeds the short-form maximum.
    /// </summary>
    private static int DecodeCommand(ReadOnlySpan<byte> src, ref int p, out int command)
    {
        var b = src[p++];
        command = b & CommandMask;
        var payload = b >> CommandPayloadShift;
        var extraBytes = payload - MaxShortPayload;
        if (extraBytes <= 0) return payload;

        payload = src[p++];
        if (extraBytes > 1) payload += src[p++] << 8;
        if (extraBytes > 2) payload += src[p++] << 16;
        return payload + MaxShortPayload;
    }
}
