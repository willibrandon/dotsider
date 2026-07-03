using System.Buffers.Binary;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Decides whether an ELF debug sidecar belongs to a stripped image. The image's identity
/// signals — the GNU build id and the <c>.gnu_debuglink</c> CRC — are each verified when
/// present, and all present signals must pass. Only a signal-free image falls back to the loose
/// checks: same <c>e_machine</c> and a sidecar that actually carries <c>.debug_info</c>.
/// </summary>
internal static class ElfSidecarIdentity
{
    /// <summary>Checks the sidecar's identity against the image.</summary>
    /// <param name="image">The stripped image's bytes.</param>
    /// <param name="sidecar">The candidate sidecar's bytes.</param>
    public static ElfSidecarMatch Check(ReadOnlySpan<byte> image, ReadOnlySpan<byte> sidecar)
    {
        var hasBuildId = ElfImageReader.TryReadBuildId(image, out var imageId);
        var hasDebugLink = ElfImageReader.TryReadDebugLink(image, out _, out var expectedCrc);

        if (hasBuildId || hasDebugLink)
        {
            if (hasBuildId
                && (!ElfImageReader.TryReadBuildId(sidecar, out var sidecarId)
                    || !imageId.AsSpan().SequenceEqual(sidecarId)))
            {
                return ElfSidecarMatch.Mismatched;
            }

            if (hasDebugLink && Crc32.Compute(sidecar) != expectedCrc)
                return ElfSidecarMatch.Mismatched;

            return ElfSidecarMatch.Matched;
        }

        return SameMachine(image, sidecar) && ElfImageReader.TryGetSection(sidecar, ".debug_info", out _)
            ? ElfSidecarMatch.LooseMatch
            : ElfSidecarMatch.Mismatched;
    }

    private static bool SameMachine(ReadOnlySpan<byte> image, ReadOnlySpan<byte> sidecar) =>
        ElfImageReader.IsElf(image) && ElfImageReader.IsElf(sidecar)
        && BinaryPrimitives.ReadUInt16LittleEndian(image[18..])
            == BinaryPrimitives.ReadUInt16LittleEndian(sidecar[18..]);
}
