using Dotsider.Core.Analysis.NativePdb;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>Verifies bounded native PDB identity probing over attacker-controlled MSF fields.</summary>
[TestClass]
public sealed class NativePdbProbeBoundsTests
{
    /// <summary>Verifies a synthetic valid info stream retains its GUID and age.</summary>
    [TestMethod]
    public void TryReadPdbId_ValidSyntheticPdb_ReturnsIdentity()
    {
        var expectedGuid = Guid.NewGuid();
        const int expectedAge = 17;
        var path = WriteTemporaryPdb(
            SyntheticImageBuilders.BuildMsf(512, PdbInfoStream(expectedGuid, expectedAge)));

        try
        {
            var valid = NativePdbReader.TryReadPdbId(path, out var guid, out var age);

            Assert.IsTrue(valid);
            Assert.AreEqual(expectedGuid, guid);
            Assert.AreEqual(expectedAge, age);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies impossible directory declarations fail with default identity outputs.</summary>
    /// <param name="malformation">The superblock range rule to violate.</param>
    [TestMethod]
    [DataRow("BlockMapOutsideFile")]
    [DataRow("DirectoryBlockOutsideFile")]
    [DataRow("DirectoryRoundingOverflow")]
    [DataRow("InfoStreamBlockOutsideFile")]
    [DataRow("InfoStreamTooShort")]
    [DataRow("StreamZeroBlockListOutsideDirectory")]
    public void TryReadPdbId_ImpossibleDirectoryDeclaration_ReturnsFalseWithDefaults(
        string malformation)
    {
        const int blockSize = 512;
        var image = SyntheticImageBuilders.BuildMsf(
            blockSize,
            PdbInfoStream(Guid.NewGuid(), 17));
        switch (malformation)
        {
            case "BlockMapOutsideFile":
                BinaryPrimitives.WriteInt32LittleEndian(
                    image.AsSpan(52),
                    image.Length / blockSize + 1);
                break;
            case "DirectoryBlockOutsideFile":
                BinaryPrimitives.WriteUInt32LittleEndian(
                    image.AsSpan(BlockMapOffset(image)),
                    uint.MaxValue);
                break;
            case "DirectoryRoundingOverflow":
                BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(44), int.MaxValue);
                break;
            case "InfoStreamBlockOutsideFile":
                BinaryPrimitives.WriteUInt32LittleEndian(
                    image.AsSpan(DirectoryOffset(image) + 3 * sizeof(uint)),
                    uint.MaxValue);
                break;
            case "InfoStreamTooShort":
                BinaryPrimitives.WriteUInt32LittleEndian(
                    image.AsSpan(DirectoryOffset(image) + 2 * sizeof(uint)),
                    27);
                break;
            case "StreamZeroBlockListOutsideDirectory":
                BinaryPrimitives.WriteUInt32LittleEndian(
                    image.AsSpan(DirectoryOffset(image) + sizeof(uint)),
                    int.MaxValue);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(malformation));
        }

        var path = WriteTemporaryPdb(image);
        try
        {
            var valid = NativePdbReader.TryReadPdbId(path, out var guid, out var age);

            Assert.IsFalse(valid);
            Assert.AreEqual(Guid.Empty, guid);
            Assert.AreEqual(0, age);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a directory declaration beyond one map block is rejected before proportional
    /// allocation.
    /// </summary>
    [TestMethod]
    public void TryReadPdbId_MapCapacityExceeded_DoesNotScaleAllocationWithDeclaration()
    {
        const int blockSize = 8192;
        var warmupPath = WriteTemporaryPdb(
            SyntheticImageBuilders.BuildMsf(
                blockSize,
                PdbInfoStream(Guid.NewGuid(), 1)));
        var image = SyntheticImageBuilders.BuildMsf(
            blockSize,
            PdbInfoStream(Guid.NewGuid(), 2));
        var mapCapacity = blockSize / sizeof(int);
        var declaredBlockCount = mapCapacity + 2;
        Array.Resize(ref image, declaredBlockCount * blockSize);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(40), declaredBlockCount);
        BinaryPrimitives.WriteInt32LittleEndian(
            image.AsSpan(44),
            checked((mapCapacity + 1) * blockSize));
        var path = WriteTemporaryPdb(image);

        try
        {
            _ = NativePdbReader.TryReadPdbId(warmupPath, out _, out _);
            var before = GC.GetAllocatedBytesForCurrentThread();

            var valid = NativePdbReader.TryReadPdbId(path, out var guid, out var age);

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsFalse(valid);
            Assert.AreEqual(Guid.Empty, guid);
            Assert.AreEqual(0, age);
            Assert.IsLessThan(
                2_000_000L,
                allocated,
                $"The impossible directory declaration allocated {allocated:N0} bytes.");
        }
        finally
        {
            File.Delete(path);
            File.Delete(warmupPath);
        }
    }

    private static byte[] PdbInfoStream(Guid guid, int age)
    {
        var stream = new byte[28];
        BinaryPrimitives.WriteInt32LittleEndian(stream, 20000404);
        BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(8), age);
        guid.TryWriteBytes(stream.AsSpan(12));
        return stream;
    }

    private static int BlockMapOffset(byte[] image)
    {
        var blockSize = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(32));
        var blockMap = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(52));
        return blockMap * blockSize;
    }

    private static int DirectoryOffset(byte[] image)
    {
        var blockSize = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(32));
        var directoryBlock = BinaryPrimitives.ReadInt32LittleEndian(
            image.AsSpan(BlockMapOffset(image)));
        return directoryBlock * blockSize;
    }

    private static string WriteTemporaryPdb(byte[] image)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dotsider-pdb-probe-{Guid.NewGuid():N}.pdb");
        File.WriteAllBytes(path, image);
        return path;
    }
}
