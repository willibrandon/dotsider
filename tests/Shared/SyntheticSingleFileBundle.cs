using Dotsider.Core.Analysis.Models;
using System.IO.Compression;
using System.Text;

namespace Dotsider.Tests.Shared;

/// <summary>
/// Creates minimal single-file bundle fixtures for parser and containment tests.
/// </summary>
internal static class SyntheticSingleFileBundle
{
    private const int PayloadOffset = 32;

    /// <summary>
    /// Gets the fixed offset at which generated manifests begin.
    /// </summary>
    internal const int HeaderOffset = 128;

    private static readonly byte[] s_bundleSignature =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
        0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
        0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae
    ];

    /// <summary>
    /// Creates a minimal bundle with one configurable manifest entry.
    /// </summary>
    /// <param name="majorVersion">The bundle major version to encode.</param>
    /// <param name="fileCount">The manifest file count to encode.</param>
    /// <param name="bundleId">The bundle identifier to encode.</param>
    /// <param name="offset">The entry offset to encode.</param>
    /// <param name="size">The logical entry size to encode.</param>
    /// <param name="compressedSize">The compressed entry size to encode.</param>
    /// <param name="type">The entry type to encode.</param>
    /// <param name="relativePath">The entry relative path to encode.</param>
    /// <param name="payload">The payload to place before the manifest.</param>
    /// <returns>The path of the generated bundle file.</returns>
    internal static string Create(
        uint majorVersion = 6,
        int fileCount = 1,
        string bundleId = "TestBundle",
        long offset = 32,
        long size = 4,
        long compressedSize = 0,
        BundleFileType type = BundleFileType.Assembly,
        string relativePath = "Test.dll",
        byte[]? payload = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotsider-single-file-{Guid.NewGuid():N}.bundle");
        payload ??= [0x4d, 0x5a, 0x90, 0x00];

        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        stream.SetLength(HeaderOffset);
        stream.Position = PayloadOffset;
        stream.Write(payload);
        stream.Position = HeaderOffset;

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(majorVersion);
        writer.Write(0u);
        writer.Write(fileCount);
        writer.Write(bundleId);

        if (majorVersion >= 2)
        {
            writer.Write(0L);
            writer.Write(0L);
            writer.Write(0L);
            writer.Write(0L);
            writer.Write(0UL);
        }

        if (fileCount > 0)
        {
            writer.Write(offset);
            writer.Write(size);
            if (majorVersion >= 6)
                writer.Write(compressedSize);
            writer.Write((byte)type);
            writer.Write(relativePath);
        }

        writer.Write((long)HeaderOffset);
        writer.Write(s_bundleSignature);
        return path;
    }

    /// <summary>
    /// Compresses <paramref name="bytes"/> using the Deflate format used by compressed bundle entries.
    /// </summary>
    /// <param name="bytes">The logical entry bytes to compress.</param>
    /// <returns>The compressed bytes.</returns>
    internal static byte[] Deflate(byte[] bytes)
    {
        using var stream = new MemoryStream();
        using (var deflate = new DeflateStream(stream, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(bytes);

        return stream.ToArray();
    }
}
