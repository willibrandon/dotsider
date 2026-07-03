namespace Dotsider.Core.Analysis;

/// <summary>
/// The standard CRC-32 (IEEE 802.3, reflected polynomial 0xEDB88320) — the checksum
/// <c>.gnu_debuglink</c> stores over the entire sidecar file.
/// </summary>
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            table[i] = value;
        }

        return table;
    }

    /// <summary>Computes the CRC-32 of <paramref name="bytes"/>.</summary>
    /// <param name="bytes">The bytes to checksum.</param>
    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFF_FFFF;
        foreach (var b in bytes)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }
}
