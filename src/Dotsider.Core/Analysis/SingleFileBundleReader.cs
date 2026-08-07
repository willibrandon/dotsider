using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads .NET single-file bundles — detects the bundle signature, parses the
/// manifest header, and extracts individual entries.
/// </summary>
public static class SingleFileBundleReader
{
    private const long MaxManifestBytes = 16L * 1024 * 1024;
    private const int MaxEntryCount = 100_000;
    private const int MaxStringBytes = 32 * 1024;
    private const long MaxMaterializedEntryBytes = 512L * 1024 * 1024;
    private const int BundleLocatorLength = sizeof(long) + 32;
    private const int BundleScanBufferSize = 64 * 1024;
    private const int MinimumV1EntryBytes = (sizeof(long) * 2) + sizeof(byte) + sizeof(byte);
    private const int MinimumV6EntryBytes = (sizeof(long) * 3) + sizeof(byte) + sizeof(byte);

    // SHA-256 for ".net core bundle"
    private static readonly byte[] BundleSignature =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38,
        0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18,
        0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae
    ];

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length < BundleLocatorLength)
                return false;

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[BundleScanBufferSize + BundleLocatorLength - 1];
            var bufferedBytes = 0;
            while (true)
            {
                var bytesRead = stream.Read(buffer, bufferedBytes, buffer.Length - bufferedBytes);
                if (bytesRead == 0)
                    return false;

                var availableBytes = bufferedBytes + bytesRead;
                var data = buffer.AsSpan(0, availableBytes);
                if (TryFindBundleLocator(data, fileInfo.Length, out headerOffset))
                    return true;

                bufferedBytes = Math.Min(BundleLocatorLength - 1, availableBytes);
                if (bufferedBytes > 0)
                {
                    Buffer.BlockCopy(
                        buffer,
                        availableBytes - bufferedBytes,
                        buffer,
                        0,
                        bufferedBytes);
                }
            }
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
        if (data.Length < BundleLocatorLength)
            return false;

        return TryFindBundleLocator(data, data.Length, out headerOffset);
    }

    /// <summary>
    /// Reads the bundle manifest from the file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Path to the bundle file.</param>
    /// <param name="headerOffset">
    /// The byte offset of the bundle header, as returned by <see cref="IsBundle(string, out long)"/>.
    /// </param>
    /// <returns>The parsed bundle manifest.</returns>
    /// <exception cref="InvalidDataException">
    /// The header offset is invalid, the manifest is malformed, or the bundle version is unsupported.
    /// </exception>
    public static BundleManifest ReadManifest(string filePath, long headerOffset)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (headerOffset < 0 || headerOffset >= stream.Length)
            throw CreateMalformedManifestException();

        stream.Position = headerOffset;
        return ReadManifest(stream);
    }

    /// <summary>
    /// Reads the bundle manifest from a readable, seekable stream positioned at the header.
    /// </summary>
    /// <param name="stream">A readable, seekable stream positioned at the bundle header offset.</param>
    /// <returns>The parsed bundle manifest.</returns>
    /// <exception cref="InvalidDataException">
    /// The stream is unsuitable, the manifest is malformed, or the bundle version is unsupported.
    /// </exception>
    public static BundleManifest ReadManifest(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead || !stream.CanSeek)
            throw CreateMalformedManifestException();

        try
        {
            var manifestStart = stream.Position;
            var remainingFileBytes = stream.Length - manifestStart;
            if (manifestStart < 0 || remainingFileBytes < 0)
                throw CreateMalformedManifestException();

            var manifestEnd = manifestStart + Math.Min(remainingFileBytes, MaxManifestBytes);
            using var reader = new BinaryReader(stream, StrictUtf8, leaveOpen: true);

            var majorVersion = ReadUInt32(reader, stream, manifestEnd);
            var minorVersion = ReadUInt32(reader, stream, manifestEnd);

            // Versions 3, 4, 5 were skipped to align with .NET versioning.
            if (majorVersion is < 1 or > 6)
                throw new InvalidDataException("The single-file bundle manifest version is unsupported.");

            var fileCount = ReadInt32(reader, stream, manifestEnd);
            if (fileCount is <= 0 or > MaxEntryCount)
                throw CreateMalformedManifestException();

            var bundleId = ReadManifestString(reader, stream, manifestEnd);

            if (majorVersion >= 2)
            {
                var depsJsonOffset = ReadInt64(reader, stream, manifestEnd);
                var depsJsonSize = ReadInt64(reader, stream, manifestEnd);
                var runtimeConfigJsonOffset = ReadInt64(reader, stream, manifestEnd);
                var runtimeConfigJsonSize = ReadInt64(reader, stream, manifestEnd);
                _ = ReadUInt64(reader, stream, manifestEnd);

                ValidateOptionalDataRange(depsJsonOffset, depsJsonSize, manifestStart);
                ValidateOptionalDataRange(runtimeConfigJsonOffset, runtimeConfigJsonSize, manifestStart);
            }

            var minimumEntryBytes = majorVersion >= 6 ? MinimumV6EntryBytes : MinimumV1EntryBytes;
            if (fileCount > GetRemainingManifestBytes(stream, manifestEnd) / minimumEntryBytes)
                throw CreateMalformedManifestException();

            var entries = new BundleEntry[fileCount];
            for (var i = 0; i < entries.Length; i++)
                entries[i] = ReadManifestEntry(reader, stream, manifestEnd, manifestStart, majorVersion);

            return new BundleManifest(majorVersion, minorVersion, fileCount, bundleId, entries);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or DecoderFallbackException
            or EndOfStreamException
            or FormatException
            or IOException
            or NotSupportedException
            or OverflowException)
        {
            throw CreateMalformedManifestException(exception);
        }
    }

    /// <summary>
    /// Reads a specific entry's raw bytes from the bundle.
    /// </summary>
    /// <param name="filePath">Path to the bundle file.</param>
    /// <param name="manifest">The bundle manifest.</param>
    /// <param name="entryRelativePath">The <see cref="BundleEntry.RelativePath"/> to read.</param>
    /// <returns>The entry's bytes, or <c>null</c> if the entry was not found, is unsafe, or cannot be read.</returns>
    public static byte[]? ReadEntry(string filePath, BundleManifest manifest, string entryRelativePath)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(entryRelativePath);

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
    /// <returns>The assembly bytes, or <c>null</c> if the entry is not found, is unsafe, or cannot be read.</returns>
    public static byte[]? ReadAssembly(string filePath, BundleManifest manifest, string assemblyName)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(assemblyName);

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
    /// The entry assembly bytes and name, or <c>null</c> if the file is not a bundle,
    /// the manifest is invalid, or no entry assembly could be identified.
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

        // Fallback: match by BundleId.
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

        // Verify the extracted bytes are a valid PE with metadata.
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

    private static InvalidDataException CreateMalformedManifestException(Exception? innerException = null)
    {
        return new InvalidDataException("The single-file bundle manifest is malformed.", innerException);
    }

    private static bool TryFindBundleLocator(
        ReadOnlySpan<byte> data,
        long fileLength,
        out long headerOffset)
    {
        headerOffset = 0;
        if (data.Length < BundleLocatorLength)
            return false;

        var finalLocatorStart = data.Length - BundleLocatorLength;
        for (var locatorStart = 0; locatorStart <= finalLocatorStart; locatorStart++)
        {
            var signatureStart = locatorStart + sizeof(long);
            if (data[signatureStart] != BundleSignature[0]
                || !data.Slice(signatureStart, BundleSignature.Length).SequenceEqual(BundleSignature))
            {
                continue;
            }

            var candidateHeaderOffset = BinaryPrimitives.ReadInt64LittleEndian(data[locatorStart..]);
            if (candidateHeaderOffset > 0 && candidateHeaderOffset < fileLength)
            {
                headerOffset = candidateHeaderOffset;
                return true;
            }
        }

        return false;
    }

    private static long GetRemainingManifestBytes(Stream stream, long manifestEnd)
    {
        if (stream.Position < 0 || stream.Position > manifestEnd)
            throw CreateMalformedManifestException();

        return manifestEnd - stream.Position;
    }

    private static BundleEntry ReadManifestEntry(
        BinaryReader reader,
        Stream stream,
        long manifestEnd,
        long manifestStart,
        uint majorVersion)
    {
        var offset = ReadInt64(reader, stream, manifestEnd);
        var size = ReadInt64(reader, stream, manifestEnd);
        var compressedSize = majorVersion >= 6 ? ReadInt64(reader, stream, manifestEnd) : 0;
        var type = (BundleFileType)ReadByte(reader, stream, manifestEnd);
        var relativePath = ReadManifestString(reader, stream, manifestEnd);

        ValidateEntry(offset, size, compressedSize, type, relativePath, manifestStart);
        return new BundleEntry(offset, size, compressedSize, type, relativePath);
    }

    private static string ReadManifestString(BinaryReader reader, Stream stream, long manifestEnd)
    {
        var byteLength = Read7BitEncodedInt(reader, stream, manifestEnd);
        if (byteLength is <= 0 or > MaxStringBytes)
            throw CreateMalformedManifestException();

        EnsureAvailable(stream, manifestEnd, byteLength);
        var bytes = reader.ReadBytes(byteLength);
        if (bytes.Length != byteLength)
            throw CreateMalformedManifestException();

        var value = StrictUtf8.GetString(bytes);
        if (value.Length == 0 || value.Contains('\0'))
            throw CreateMalformedManifestException();

        return value;
    }

    private static int Read7BitEncodedInt(BinaryReader reader, Stream stream, long manifestEnd)
    {
        uint value = 0;

        for (var shift = 0; shift < 35; shift += 7)
        {
            var current = ReadByte(reader, stream, manifestEnd);
            if (shift == 28 && (current & 0xf0) != 0)
                throw CreateMalformedManifestException();

            value |= (uint)(current & 0x7f) << shift;
            if ((current & 0x80) == 0)
            {
                if (value > int.MaxValue || (shift > 0 && value < (1u << shift)))
                    throw CreateMalformedManifestException();

                return (int)value;
            }
        }

        throw CreateMalformedManifestException();
    }

    private static int ReadInt32(BinaryReader reader, Stream stream, long manifestEnd)
    {
        EnsureAvailable(stream, manifestEnd, sizeof(int));
        return reader.ReadInt32();
    }

    private static long ReadInt64(BinaryReader reader, Stream stream, long manifestEnd)
    {
        EnsureAvailable(stream, manifestEnd, sizeof(long));
        return reader.ReadInt64();
    }

    private static uint ReadUInt32(BinaryReader reader, Stream stream, long manifestEnd)
    {
        EnsureAvailable(stream, manifestEnd, sizeof(uint));
        return reader.ReadUInt32();
    }

    private static ulong ReadUInt64(BinaryReader reader, Stream stream, long manifestEnd)
    {
        EnsureAvailable(stream, manifestEnd, sizeof(ulong));
        return reader.ReadUInt64();
    }

    private static byte ReadByte(BinaryReader reader, Stream stream, long manifestEnd)
    {
        EnsureAvailable(stream, manifestEnd, sizeof(byte));
        return reader.ReadByte();
    }

    private static void EnsureAvailable(Stream stream, long manifestEnd, int requiredBytes)
    {
        if (requiredBytes < 0 || GetRemainingManifestBytes(stream, manifestEnd) < requiredBytes)
            throw CreateMalformedManifestException();
    }

    private static void ValidateEntry(
        long offset,
        long size,
        long compressedSize,
        BundleFileType type,
        string relativePath,
        long dataBoundary)
    {
        if (offset <= 0
            || size < 0
            || compressedSize < 0
            || type > BundleFileType.Symbols
            || string.IsNullOrEmpty(relativePath)
            || relativePath.Contains('\0'))
        {
            throw CreateMalformedManifestException();
        }

        var storedSize = compressedSize > 0 ? compressedSize : size;
        ValidateDataRange(offset, storedSize, dataBoundary);
    }

    private static void ValidateOptionalDataRange(long offset, long size, long dataBoundary)
    {
        if (offset == 0)
        {
            if (size == 0)
                return;

            throw CreateMalformedManifestException();
        }

        ValidateDataRange(offset, size, dataBoundary);
    }

    private static void ValidateDataRange(long offset, long size, long dataBoundary)
    {
        if (offset < 0
            || size < 0
            || dataBoundary < 0
            || offset > dataBoundary
            || size > dataBoundary - offset)
        {
            throw CreateMalformedManifestException();
        }
    }

    private static byte[]? ReadEntryBytes(string filePath, BundleEntry entry)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            ValidateEntry(
                entry.Offset,
                entry.Size,
                entry.CompressedSize,
                entry.Type,
                entry.RelativePath,
                stream.Length);

            var storedSize = entry.CompressedSize > 0 ? entry.CompressedSize : entry.Size;
            if (entry.Size > MaxMaterializedEntryBytes || storedSize > MaxMaterializedEntryBytes)
                return null;

            stream.Position = entry.Offset;
            if (entry.CompressedSize > 0)
                return ReadCompressedEntry(stream, entry);

            var bytes = new byte[checked((int)entry.Size)];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or IOException
            or OverflowException)
        {
            return null;
        }
    }

    private static byte[]? ReadCompressedEntry(Stream stream, BundleEntry entry)
    {
        using var compressedStream = new BoundedReadStream(stream, entry.CompressedSize, leaveOpen: true);
        using var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress, leaveOpen: true);
        var bytes = new byte[checked((int)entry.Size)];

        deflate.ReadExactly(bytes);
        if (deflate.ReadByte() != -1)
            return null;

        return bytes;
    }
}
