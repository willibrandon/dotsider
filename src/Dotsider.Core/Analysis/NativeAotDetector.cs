using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Detects Native AOT compiled .NET binaries by locating and validating the embedded
/// ReadyToRun header. Every Native AOT image carries this header (signature "RTR\0")
/// so the runtime can find its module sections, but the signature bytes also occur as
/// code immediates, so each candidate is validated against the field ranges the ILC
/// toolchain actually emits before it is accepted.
/// </summary>
public static partial class NativeAotDetector
{
    /// <summary>Fixed size of the ReadyToRun header before the section entries begin.</summary>
    private const int HeaderSize = 16;

    /// <summary>
    /// The runtime pack embeds its version string near this message — the two live in
    /// the same runtime object file, so linkers keep them close, but which side the
    /// version lands on differs: immediately before on MSVC-linked PEs, a few hundred
    /// bytes after on ELF images.
    /// </summary>
    private static ReadOnlySpan<byte> VersionAnchor =>
        "Process is terminating due to StackOverflowException"u8;

    /// <summary>Bytes to search on each side of <see cref="VersionAnchor"/> for the version string.</summary>
    private const int VersionWindowSize = 1024;

    /// <summary>
    /// Probes the given binary image for a validated ReadyToRun header.
    /// </summary>
    /// <param name="bytes">The raw bytes of the binary file.</param>
    /// <returns>
    /// The extracted header facts when the image is a PE, ELF, or Mach-O binary
    /// containing a validated ReadyToRun header; otherwise null. Callers decide
    /// what the result means in combination with metadata presence — a managed
    /// R2R assembly also embeds this header, so probe only metadata-less files.
    /// </returns>
    public static NativeAotInfo? Detect(ReadOnlySpan<byte> bytes)
    {
        if (!HasExecutableMagic(bytes)) return null;

        ReadOnlySpan<byte> signature = [(byte)'R', (byte)'T', (byte)'R', 0];
        var searchStart = 0;
        while (searchStart < bytes.Length)
        {
            var index = bytes[searchStart..].IndexOf(signature);
            if (index < 0) return null;

            var candidate = searchStart + index;
            if (ParseHeader(bytes, candidate) is { } info)
                return info with { RuntimeVersion = DetectRuntimeVersion(bytes) };

            searchStart = candidate + 1;
        }

        return null;
    }

    /// <summary>
    /// Returns true if the bytes start with a PE, ELF, or Mach-O magic number.
    /// </summary>
    private static bool HasExecutableMagic(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4) return false;

        // PE: MZ
        if (bytes[0] == (byte)'M' && bytes[1] == (byte)'Z') return true;

        // ELF: \x7fELF
        if (bytes[0] == 0x7F && bytes[1] == (byte)'E' && bytes[2] == (byte)'L' && bytes[3] == (byte)'F')
            return true;

        // Mach-O: four known magic values (big/little endian, 32/64-bit)
        var magic = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        return magic is 0xFEEDFACE or 0xFEEDFACF or 0xCEFAEDFE or 0xCFFAEDFE;
    }

    /// <summary>
    /// Validates a candidate header at <paramref name="offset"/> and extracts its fields.
    /// Field ranges are deliberately tight: a real code-immediate collision observed in
    /// ILC output passes the signature check but fails these.
    /// </summary>
    private static NativeAotInfo? ParseHeader(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset + HeaderSize + sizeof(int) > bytes.Length) return null;

        var majorVersion = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 4)..]);
        if (majorVersion is < 1 or > 100) return null;

        var minorVersion = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 6)..]);
        if (minorVersion > 100) return null;

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 8)..]);

        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 12)..]);
        if (sectionCount is 0 or > 1000) return null;

        var entrySize = bytes[offset + 14];
        if (entrySize is < 8 or > 64) return null;

        var entryType = bytes[offset + 15];
        if (entryType > 8) return null;

        if (offset + HeaderSize + (long)sectionCount * entrySize > bytes.Length) return null;

        // Native AOT module section ids live in the 200..999 range (StringTable = 200,
        // GCStaticRegion = 201, readonly blob regions = 300..399) and entries are sorted,
        // so the first id is always in range. Managed ReadyToRun images use ids around
        // 100 with a different entry layout, so they fall out here and at the
        // entry-size check above.
        var firstSectionId = BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + HeaderSize)..]);
        if (firstSectionId is < 200 or > 999) return null;

        return new NativeAotInfo(
            offset, majorVersion, minorVersion, flags, sectionCount, entrySize,
            RuntimeVersion: null);
    }

    /// <summary>
    /// Heuristically recovers the runtime version from the string the runtime pack
    /// embeds near its stack-overflow fatal message, taking the version-shaped match
    /// closest to the anchor. Returns null when the layout does not match — never
    /// guesses.
    /// </summary>
    private static string? DetectRuntimeVersion(ReadOnlySpan<byte> bytes)
    {
        var anchorIndex = bytes.IndexOf(VersionAnchor);
        if (anchorIndex < 0) return null;

        var windowStart = Math.Max(0, anchorIndex - VersionWindowSize);
        var windowEnd = Math.Min(bytes.Length, anchorIndex + VersionAnchor.Length + VersionWindowSize);
        var window = Encoding.Latin1.GetString(bytes[windowStart..windowEnd]);

        var anchorOffset = anchorIndex - windowStart;
        Match? best = null;
        var bestDistance = int.MaxValue;
        foreach (Match match in VersionRegex().Matches(window))
        {
            var distance = Math.Abs(match.Index - anchorOffset);
            if (distance < bestDistance)
            {
                best = match;
                bestDistance = distance;
            }
        }

        return best?.Value;
    }

    /// <summary>Matches a three-part runtime version with an optional prerelease suffix.</summary>
    [GeneratedRegex(@"\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?")]
    private static partial Regex VersionRegex();
}
