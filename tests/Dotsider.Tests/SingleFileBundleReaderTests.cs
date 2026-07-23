using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Tests.Shared;
using System.IO.Compression;
using System.Text;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="SingleFileBundleReader"/> covering bundle detection,
/// manifest parsing, and entry assembly extraction.
/// </summary>
[TestClass]
public sealed class SingleFileBundleReaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>Verifies that a self-contained single-file exe is detected as a bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsBundle_SelfContainedExe_ReturnsTrue()
    {
        Assert.IsNotNull(Samples.SelfContainedConsoleExe);
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        Assert.IsGreaterThan(0, offset);
    }

    /// <summary>Verifies that a regular managed DLL is not detected as a bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsBundle_RegularDll_ReturnsFalse()
    {
        Assert.IsFalse(SingleFileBundleReader.IsBundle(Samples.RichLibraryDll, out _));
    }

    /// <summary>Verifies that a NativeAOT exe is not detected as a bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsBundle_NativeAotExe_ReturnsFalse()
    {
        Assert.IsNotNull(Samples.NativeAotConsoleExe);
        Assert.IsFalse(SingleFileBundleReader.IsBundle(Samples.NativeAotConsoleExe!, out _));
    }

    /// <summary>Verifies that the manifest has a positive file count matching its entries.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_SelfContainedExe_HasEntries()
    {
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(Samples.SelfContainedConsoleExe!, offset);
        Assert.IsGreaterThan(0, manifest.FileCount);
        Assert.HasCount(manifest.FileCount, manifest.Entries);
    }

    /// <summary>Verifies that System.Runtime.dll is included in the bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_SelfContainedExe_ContainsSystemRuntime()
    {
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(Samples.SelfContainedConsoleExe!, offset);
        Assert.Contains(e =>
            e.Type == BundleFileType.Assembly
            && Path.GetFileName(e.RelativePath).Equals("System.Runtime.dll", StringComparison.OrdinalIgnoreCase), manifest.Entries);
    }

    /// <summary>Verifies that the entry assembly DLL is included in the bundle.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_SelfContainedExe_ContainsEntryAssembly()
    {
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(Samples.SelfContainedConsoleExe!, offset);
        Assert.Contains(e =>
            e.Type == BundleFileType.Assembly
            && Path.GetFileName(e.RelativePath).Equals("SelfContainedConsole.dll", StringComparison.OrdinalIgnoreCase), manifest.Entries);
    }

    /// <summary>Verifies that extracted System.Runtime bytes form a valid PE (MZ header).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadAssembly_SystemRuntime_ReturnsPeBytes()
    {
        Assert.IsTrue(SingleFileBundleReader.IsBundle(Samples.SelfContainedConsoleExe!, out var offset));
        var manifest = SingleFileBundleReader.ReadManifest(Samples.SelfContainedConsoleExe!, offset);
        Assert.Contains(e => e.CompressedSize > 0, manifest.Entries);
        var bytes = SingleFileBundleReader.ReadAssembly(Samples.SelfContainedConsoleExe!, manifest, "System.Runtime");
        Assert.IsNotNull(bytes);
        // Verify it's a valid PE — MZ header
        Assert.IsGreaterThan(2, bytes.Length);
        Assert.AreEqual((byte)'M', bytes[0]);
        Assert.AreEqual((byte)'Z', bytes[1]);
    }

    /// <summary>Verifies that FindEntryAssembly returns the correct entry name.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FindEntryAssembly_SelfContainedExe_MatchesBasename()
    {
        var result = SingleFileBundleReader.FindEntryAssembly(Samples.SelfContainedConsoleExe!);
        Assert.IsNotNull(result);
        Assert.AreEqual("SelfContainedConsole.dll", result.Value.Name);
        Assert.IsGreaterThan(0, result.Value.Bytes.Length);
    }

    /// <summary>Verifies that the extracted entry assembly has valid metadata.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FindEntryAssembly_SelfContainedExe_HasValidMetadata()
    {
        var result = SingleFileBundleReader.FindEntryAssembly(Samples.SelfContainedConsoleExe!);
        Assert.IsNotNull(result);
        using var analyzer = new AssemblyAnalyzer(result.Value.Bytes, result.Value.Name);
        Assert.IsTrue(analyzer.HasMetadata);
        Assert.AreEqual("SelfContainedConsole", analyzer.AssemblyName);
    }

    /// <summary>
    /// Verifies that version 1 and version 6 synthetic bundles parse with their expected entry layouts.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_SyntheticV1AndV6_ParsesEntries()
    {
        var v1Path = SyntheticSingleFileBundle.Create(majorVersion: 1);
        var v6Path = SyntheticSingleFileBundle.Create(majorVersion: 6);
        var unknownTypePath = SyntheticSingleFileBundle.Create(type: BundleFileType.Unknown);
        try
        {
            var v1 = SingleFileBundleReader.ReadManifest(v1Path, SyntheticSingleFileBundle.HeaderOffset);
            var v6 = SingleFileBundleReader.ReadManifest(v6Path, SyntheticSingleFileBundle.HeaderOffset);
            var unknownType = SingleFileBundleReader.ReadManifest(unknownTypePath, SyntheticSingleFileBundle.HeaderOffset);

            Assert.AreEqual(1u, v1.MajorVersion);
            Assert.AreEqual(6u, v6.MajorVersion);
            Assert.HasCount(1, v1.Entries);
            Assert.HasCount(1, v6.Entries);
            Assert.AreEqual(0L, v1.Entries[0].CompressedSize);
            Assert.AreEqual(0L, v6.Entries[0].CompressedSize);
            Assert.AreEqual(BundleFileType.Unknown, unknownType.Entries[0].Type);
        }
        finally
        {
            DeleteFile(v1Path);
            DeleteFile(v6Path);
            DeleteFile(unknownTypePath);
        }
    }

    /// <summary>
    /// Verifies that a signature beginning at the final legal span offset is found.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsBundle_SignatureAtFinalLegalOffset_ReturnsTrue()
    {
        var path = SyntheticSingleFileBundle.Create();
        try
        {
            var bytes = File.ReadAllBytes(path);
            Assert.IsTrue(SingleFileBundleReader.IsBundle(bytes, out var headerOffset));
            Assert.AreEqual(SyntheticSingleFileBundle.HeaderOffset, headerOffset);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    /// <summary>
    /// Verifies that invalid entry counts are rejected before an entries array is allocated.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_InvalidFileCount_ThrowsInvalidDataException()
    {
        foreach (var fileCount in new[] { -1, 0, int.MaxValue })
        {
            var path = SyntheticSingleFileBundle.Create(fileCount: fileCount);
            try
            {
                AssertInvalidManifest(path);
                if (fileCount == 0)
                    Assert.IsNull(SingleFileBundleReader.FindEntryAssembly(path));
            }
            finally
            {
                DeleteFile(path);
            }
        }
    }

    /// <summary>
    /// Verifies that malformed header, string, path, and entry-kind encodings fail closed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_MalformedHeaderAndStrings_ThrowsInvalidDataException()
    {
        using var truncated = new MemoryStream([0x06, 0x00, 0x00, 0x00], writable: false);
        Assert.ThrowsExactly<InvalidDataException>(() => SingleFileBundleReader.ReadManifest(truncated));

        var invalidUtf8Path = SyntheticSingleFileBundle.Create();
        var malformedLengthPath = SyntheticSingleFileBundle.Create();
        var nonCanonicalLengthPath = SyntheticSingleFileBundle.Create();
        var oversizedStringPath = SyntheticSingleFileBundle.Create();
        var emptyRelativePath = SyntheticSingleFileBundle.Create(relativePath: string.Empty);
        var unknownTypePath = SyntheticSingleFileBundle.Create(type: (BundleFileType)255);
        try
        {
            WriteBytes(invalidUtf8Path, SyntheticSingleFileBundle.HeaderOffset + 13, [0xff]);
            WriteBytes(malformedLengthPath, SyntheticSingleFileBundle.HeaderOffset + 12,
                [0x80, 0x80, 0x80, 0x80, 0x80]);
            WriteBytes(nonCanonicalLengthPath, SyntheticSingleFileBundle.HeaderOffset + 12,
                [0x8a, 0x00]);
            WriteBytes(oversizedStringPath, SyntheticSingleFileBundle.HeaderOffset + 12,
                [0x81, 0x80, 0x02]);

            AssertInvalidManifest(invalidUtf8Path);
            AssertInvalidManifest(malformedLengthPath);
            AssertInvalidManifest(nonCanonicalLengthPath);
            AssertInvalidManifest(oversizedStringPath);
            AssertInvalidManifest(emptyRelativePath);
            AssertInvalidManifest(unknownTypePath);
        }
        finally
        {
            DeleteFile(invalidUtf8Path);
            DeleteFile(malformedLengthPath);
            DeleteFile(nonCanonicalLengthPath);
            DeleteFile(oversizedStringPath);
            DeleteFile(emptyRelativePath);
            DeleteFile(unknownTypePath);
        }
    }

    /// <summary>
    /// Verifies that the strict UTF-8 string limit accepts the maximum valid byte length.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_MaximumLengthRelativePath_Parses()
    {
        var relativePath = new string('a', (32 * 1024) - ".dll".Length) + ".dll";
        var path = SyntheticSingleFileBundle.Create(relativePath: relativePath);
        try
        {
            var manifest = SingleFileBundleReader.ReadManifest(path, SyntheticSingleFileBundle.HeaderOffset);
            Assert.AreEqual(relativePath, manifest.Entries[0].RelativePath);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    /// <summary>
    /// Verifies that v2 locations and entry ranges cannot point beyond the manifest data boundary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_InvalidLocationsAndRanges_ThrowsInvalidDataException()
    {
        var invalidV2LocationPath = SyntheticSingleFileBundle.Create();
        var crossManifestBoundaryPath = SyntheticSingleFileBundle.Create(offset: 127, size: 2);
        var overflowPath = SyntheticSingleFileBundle.Create(offset: long.MaxValue, size: 1);
        var negativeSizePath = SyntheticSingleFileBundle.Create(size: -1);
        var invalidCompressedRangePath = SyntheticSingleFileBundle.Create(offset: 127, size: 1, compressedSize: 2);
        try
        {
            var depsJsonOffset = SyntheticSingleFileBundle.HeaderOffset + (sizeof(uint) * 2) + sizeof(int) + 1 + "TestBundle".Length;
            WriteInt64(invalidV2LocationPath, depsJsonOffset, -1);

            AssertInvalidManifest(invalidV2LocationPath);
            AssertInvalidManifest(crossManifestBoundaryPath);
            AssertInvalidManifest(overflowPath);
            AssertInvalidManifest(negativeSizePath);
            AssertInvalidManifest(invalidCompressedRangePath);
        }
        finally
        {
            DeleteFile(invalidV2LocationPath);
            DeleteFile(crossManifestBoundaryPath);
            DeleteFile(overflowPath);
            DeleteFile(negativeSizePath);
            DeleteFile(invalidCompressedRangePath);
        }
    }

    /// <summary>
    /// Verifies that a readable but non-seekable manifest stream is rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_NonSeekableStream_ThrowsInvalidDataException()
    {
        var path = SyntheticSingleFileBundle.Create();
        try
        {
            var compressed = SyntheticSingleFileBundle.Deflate(File.ReadAllBytes(path));
            using var compressedStream = new MemoryStream(compressed, writable: false);
            using var nonSeekableStream = new DeflateStream(compressedStream, CompressionMode.Decompress);

            Assert.ThrowsExactly<InvalidDataException>(() => SingleFileBundleReader.ReadManifest(nonSeekableStream));
        }
        finally
        {
            DeleteFile(path);
        }
    }

    /// <summary>
    /// Verifies that compressed entries are decompressed through the bounded source stream.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadEntry_CompressedEntry_ReturnsExactLogicalBytes()
    {
        var logicalBytes = Encoding.UTF8.GetBytes("compressed bundle entry");
        var compressedBytes = SyntheticSingleFileBundle.Deflate(logicalBytes);
        var path = SyntheticSingleFileBundle.Create(
            size: logicalBytes.Length,
            compressedSize: compressedBytes.Length,
            payload: compressedBytes);
        try
        {
            var manifest = SingleFileBundleReader.ReadManifest(path, SyntheticSingleFileBundle.HeaderOffset);
            var bytes = SingleFileBundleReader.ReadEntry(path, manifest, "Test.dll");

            Assert.IsNotNull(bytes);
            Assert.AreSequenceEqual(logicalBytes, bytes);
        }
        finally
        {
            DeleteFile(path);
        }
    }

    /// <summary>
    /// Verifies that compressed output longer than the declared logical length is rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadEntry_CompressedEntryWithTrailingOutput_ReturnsNull()
    {
        var compressedBytes = SyntheticSingleFileBundle.Deflate(new byte[4096]);
        var path = SyntheticSingleFileBundle.Create(
            size: 1,
            compressedSize: compressedBytes.Length,
            payload: compressedBytes);
        try
        {
            var manifest = SingleFileBundleReader.ReadManifest(path, SyntheticSingleFileBundle.HeaderOffset);
            Assert.IsNull(SingleFileBundleReader.ReadEntry(path, manifest, "Test.dll"));
        }
        finally
        {
            DeleteFile(path);
        }
    }

    /// <summary>
    /// Verifies that a truncated compressed stream cannot produce a partial entry.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadEntry_TruncatedCompressedEntry_ReturnsNull()
    {
        var compressedBytes = SyntheticSingleFileBundle.Deflate(Encoding.UTF8.GetBytes("truncated bundle entry"));
        var truncatedBytes = compressedBytes[..^5];
        var path = SyntheticSingleFileBundle.Create(
            size: "truncated bundle entry".Length,
            compressedSize: truncatedBytes.Length,
            payload: truncatedBytes);
        try
        {
            var manifest = SingleFileBundleReader.ReadManifest(path, SyntheticSingleFileBundle.HeaderOffset);
            Assert.IsNull(SingleFileBundleReader.ReadEntry(path, manifest, "Test.dll"));
        }
        finally
        {
            DeleteFile(path);
        }
    }

    /// <summary>
    /// Verifies that oversized and externally constructed entry descriptors are not materialized.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadEntry_UnsafeEntryDescriptors_ReturnNull()
    {
        var path = SyntheticSingleFileBundle.Create();
        try
        {
            var oversizedLogicalEntry = new BundleManifest(
                6,
                0,
                1,
                "TestBundle",
                [new BundleEntry(32, (512L * 1024 * 1024) + 1, 1, BundleFileType.Assembly, "Test.dll")]);
            var outOfRangeRawEntry = new BundleManifest(
                6,
                0,
                1,
                "TestBundle",
                [new BundleEntry(32, long.MaxValue, 0, BundleFileType.Assembly, "Test.dll")]);
            var oversizedStoredEntry = new BundleManifest(
                6,
                0,
                1,
                "TestBundle",
                [new BundleEntry(32, 1, (512L * 1024 * 1024) + 1, BundleFileType.Assembly, "Test.dll")]);

            Assert.IsNull(SingleFileBundleReader.ReadEntry(path, oversizedLogicalEntry, "Test.dll"));
            Assert.IsNull(SingleFileBundleReader.ReadEntry(path, outOfRangeRawEntry, "Test.dll"));
            Assert.IsNull(SingleFileBundleReader.ReadEntry(path, oversizedStoredEntry, "Test.dll"));
            Assert.IsNull(SingleFileBundleReader.ReadAssembly(path, oversizedLogicalEntry, "Test"));
        }
        finally
        {
            DeleteFile(path);
        }
    }

    /// <summary>
    /// Verifies that bounded mutations either parse safely or report only invalid manifest data.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadManifest_BoundedMutations_DoNotEscapeInvalidDataException()
    {
        var path = SyntheticSingleFileBundle.Create();
        try
        {
            var source = File.ReadAllBytes(path);
            var random = new Random(217);
            for (var i = 0; i < 128; i++)
            {
                var mutated = source.ToArray();
                var position = random.Next(SyntheticSingleFileBundle.HeaderOffset, mutated.Length);
                mutated[position] ^= (byte)(1 << random.Next(8));

                using var stream = new MemoryStream(mutated, writable: false)
                {
                    Position = SyntheticSingleFileBundle.HeaderOffset
                };
                try
                {
                    _ = SingleFileBundleReader.ReadManifest(stream);
                }
                catch (InvalidDataException)
                {
                }
            }
        }
        finally
        {
            DeleteFile(path);
        }
    }

    private static void AssertInvalidManifest(string path)
    {
        Assert.ThrowsExactly<InvalidDataException>(
            () => SingleFileBundleReader.ReadManifest(path, SyntheticSingleFileBundle.HeaderOffset));
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static void WriteBytes(string path, long position, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        stream.Position = position;
        stream.Write(bytes);
    }

    private static void WriteInt64(string path, long position, long value)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        stream.Position = position;
        stream.Write(BitConverter.GetBytes(value));
    }
}
