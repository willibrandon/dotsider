using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;

using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads .NET single-file bundles — detects the bundle signature, parses the
/// manifest header, and extracts individual entries.
/// </summary>
public static class SingleFileBundleReader
{
    // SHA-256 for ".net core bundle"
    private static readonly byte[] BundleSignature =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
        0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
        0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae
    ];

    /// <summary>
    /// Checks whether the file at <paramref name="filePath"/> is a .NET single-file bundle.
    /// </summary>
    /// <param name="filePath">Path to the file to inspect.</param>
    /// <param name="headerOffset">
    /// When this method returns <c>true</c>, contains the byte offset of the bundle header.
    /// </param>
    /// <returns><c>true</c> if the file contains the bundle signature; otherwise <c>false</c>.</returns>
    public static bool IsBundle(string filePath, out long headerOffset)
    {
        headerOffset = 0;
        try
        {
            var data = File.ReadAllBytes(filePath);
            return IsBundle(data, out headerOffset);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the raw bytes contain the .NET single-file bundle signature.
    /// </summary>
    /// <param name="data">The raw file bytes.</param>
    /// <param name="headerOffset">
    /// When this method returns <c>true</c>, contains the byte offset of the bundle header.
    /// </param>
    /// <returns><c>true</c> if the signature is found; otherwise <c>false</c>.</returns>
    public static bool IsBundle(ReadOnlySpan<byte> data, out long headerOffset)
    {
        headerOffset = 0;
        if (data.Length < BundleSignature.Length + sizeof(long))
            return false;

        var signature = new ReadOnlySpan<byte>(BundleSignature);
        var end = data.Length - signature.Length;

        for (var i = 0; i < end; i++)
        {
            if (data[i] == 0x8b && data.Slice(i, signature.Length).SequenceEqual(signature))
            {
                if (i < sizeof(long))
                    continue;

                headerOffset = Unsafe.ReadUnaligned<long>(
                    ref Unsafe.AsRef(in data[i - sizeof(long)]));

                if (headerOffset > 0 && headerOffset < data.Length)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the bundle manifest from the file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Path to the bundle file.</param>
    /// <param name="headerOffset">
    /// The byte offset of the bundle header, as returned by <see cref="IsBundle(string, out long)"/>.
    /// </param>
    /// <returns>The parsed bundle manifest.</returns>
    /// <exception cref="InvalidDataException">The bundle version is unsupported.</exception>
    public static BundleManifest ReadManifest(string filePath, long headerOffset)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(headerOffset, SeekOrigin.Begin);
        return ReadManifest(stream);
    }

    /// <summary>
    /// Reads the bundle manifest from a stream positioned at the header.
    /// </summary>
    /// <param name="stream">A readable stream positioned at the bundle header offset.</param>
    /// <returns>The parsed bundle manifest.</returns>
    /// <exception cref="InvalidDataException">The bundle version is unsupported.</exception>
    public static BundleManifest ReadManifest(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var majorVersion = reader.ReadUInt32();
        var minorVersion = reader.ReadUInt32();

        // Versions 3, 4, 5 were skipped to align with .NET versioning
        if (majorVersion is < 1 or > 6)
            throw new InvalidDataException(
                $"Unsupported bundle version: {majorVersion}.{minorVersion}");

        var fileCount = reader.ReadInt32();
        var bundleId = reader.ReadString();

        // v2+ adds deps.json/runtimeconfig offsets and flags
        if (majorVersion >= 2)
        {
            reader.ReadInt64(); // DepsJsonOffset
            reader.ReadInt64(); // DepsJsonSize
            reader.ReadInt64(); // RuntimeConfigJsonOffset
            reader.ReadInt64(); // RuntimeConfigJsonSize
            reader.ReadUInt64(); // Flags
        }

        var entries = new BundleEntry[fileCount];
        for (var i = 0; i < fileCount; i++)
            entries[i] = ReadEntry(reader, majorVersion);

        return new BundleManifest(majorVersion, minorVersion, fileCount, bundleId, entries);
    }

    /// <summary>
    /// Reads a specific entry's raw bytes from the bundle.
    /// </summary>
    /// <param name="filePath">Path to the bundle file.</param>
    /// <param name="manifest">The bundle manifest.</param>
    /// <param name="entryRelativePath">The <see cref="BundleEntry.RelativePath"/> to read.</param>
    /// <returns>The entry's bytes, or <c>null</c> if the entry was not found.</returns>
    public static byte[]? ReadEntry(string filePath, BundleManifest manifest, string entryRelativePath)
    {
        var entry = manifest.Entries.FirstOrDefault(e =>
            string.Equals(e.RelativePath, entryRelativePath, StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : ReadEntryBytes(filePath, entry);
    }

    /// <summary>
    /// Finds and reads an assembly entry by assembly name (without extension).
    /// </summary>
    /// <param name="filePath">Path to the bundle file.</param>
    /// <param name="manifest">The bundle manifest.</param>
    /// <param name="assemblyName">Assembly name without extension (e.g. "System.Runtime").</param>
    /// <returns>The assembly bytes, or <c>null</c> if not found in the bundle.</returns>
    public static byte[]? ReadAssembly(string filePath, BundleManifest manifest, string assemblyName)
    {
        var dllName = assemblyName + ".dll";
        var entry = manifest.Entries.FirstOrDefault(e =>
            e.Type == BundleFileType.Assembly
            && string.Equals(Path.GetFileName(e.RelativePath), dllName,
                StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : ReadEntryBytes(filePath, entry);
    }

    /// <summary>
    /// Detects a single-file bundle and extracts the entry assembly (the app's own managed code).
    /// Uses dotted-name-safe basename matching: for <c>.exe</c> files, strips the extension;
    /// for extensionless files, appends <c>.dll</c> to the full filename.
    /// </summary>
    /// <param name="bundlePath">Path to the potential bundle file.</param>
    /// <returns>
    /// The entry assembly bytes and name, or <c>null</c> if the file is not a bundle
    /// or no entry assembly could be identified.
    /// </returns>
    public static (byte[] Bytes, string Name)? FindEntryAssembly(string bundlePath)
    {
        if (!IsBundle(bundlePath, out var headerOffset))
            return null;

        BundleManifest manifest;
        try
        {
            manifest = ReadManifest(bundlePath, headerOffset);
        }
        catch (InvalidDataException)
        {
            return null;
        }

        // Dotted-name-safe basename matching (mirrors ApphostDetector.cs:40-48).
        // For .exe: Foo.exe → Foo.dll
        // For extensionless (Linux/macOS): Dotsider.Website → Dotsider.Website.dll
        var fileName = Path.GetFileName(bundlePath);
        var entryDllName = bundlePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(fileName) + ".dll"
            : fileName + ".dll";

        var entry = manifest.Entries.FirstOrDefault(e =>
            e.Type == BundleFileType.Assembly
            && string.Equals(Path.GetFileName(e.RelativePath), entryDllName,
                StringComparison.OrdinalIgnoreCase));

        // Fallback: match by BundleId
        if (entry is null)
        {
            var idDllName = manifest.BundleId + ".dll";
            entry = manifest.Entries.FirstOrDefault(e =>
                e.Type == BundleFileType.Assembly
                && string.Equals(Path.GetFileName(e.RelativePath), idDllName,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (entry is null)
            return null;

        var bytes = ReadEntryBytes(bundlePath, entry);
        if (bytes is null)
            return null;

        // Verify the extracted bytes are a valid PE with metadata
        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var pe = new PEReader(ms);
            if (!pe.HasMetadata)
                return null;
            _ = pe.GetMetadataReader();
        }
        catch
        {
            return null;
        }

        return (bytes, Path.GetFileName(entry.RelativePath));
    }

    private static BundleEntry ReadEntry(BinaryReader reader, uint majorVersion)
    {
        var offset = reader.ReadInt64();
        var size = reader.ReadInt64();
        var compressedSize = majorVersion >= 6 ? reader.ReadInt64() : 0;
        var type = (BundleFileType)reader.ReadByte();
        var relativePath = reader.ReadString();
        return new BundleEntry(offset, size, compressedSize, type, relativePath);
    }

    private static byte[]? ReadEntryBytes(string filePath, BundleEntry entry)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (entry.CompressedSize > 0)
            {
                // v6+ compressed entry — DeflateStream
                stream.Seek(entry.Offset, SeekOrigin.Begin);
                var compressedBytes = new byte[entry.CompressedSize];
                stream.ReadExactly(compressedBytes);

                using var compressedStream = new MemoryStream(compressedBytes, writable: false);
                using var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress);
                var decompressed = new MemoryStream((int)entry.Size);
                deflate.CopyTo(decompressed);

                if (decompressed.Length != entry.Size)
                    throw new InvalidDataException(
                        $"Corrupted single-file entry '{entry.RelativePath}'. "
                        + $"Declared size {entry.Size} != actual {decompressed.Length}.");

                return decompressed.ToArray();
            }

            // Uncompressed entry — direct read
            stream.Seek(entry.Offset, SeekOrigin.Begin);
            var bytes = new byte[entry.Size];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
