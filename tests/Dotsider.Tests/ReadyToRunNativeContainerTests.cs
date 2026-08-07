using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.ReadyToRun;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Verifies NativeFormat array and hashtable cursors remain inside their declared ReadyToRun
/// section rather than consuming otherwise-valid bytes from adjacent image data.
/// </summary>
[TestClass]
public sealed class ReadyToRunNativeContainerTests
{
    private const int SectionFileOffset = 0x400;

    /// <summary>Accepts the exact traversal budget and rejects any additional work atomically.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TraversalBudget_ExactAndOneOver_PreservesBoundary()
    {
        var budget = new ReadyToRunTraversalBudget();

        Assert.AreEqual(ReadyToRunTraversalBudget.MaximumWork, budget.Remaining);
        Assert.IsTrue(budget.TryCharge(ReadyToRunTraversalBudget.MaximumWork));
        Assert.AreEqual(0, budget.Remaining);
        Assert.IsFalse(budget.TryCharge(1));
        Assert.IsFalse(budget.TryCharge(-1));
        Assert.AreEqual(0, budget.Remaining);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            budget.Charge(1, "test traversal"));
        Assert.Contains("1,048,576", exception.Message);
    }

    /// <summary>
    /// Rejects a maximal starting offset through subtraction-based reader and slice bounds without
    /// allowing addition overflow to escape as a runtime indexing exception.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeReader_MaximumOffsetWithPositiveLength_ThrowsBadImageFormat()
    {
        byte[] bytes = [0];
        var reader = new R2RNativeReader(bytes);
        var offset = int.MaxValue;

        _ = Assert.ThrowsExactly<BadImageFormatException>(() => reader.ReadByte(ref offset));
        _ = Assert.ThrowsExactly<BadImageFormatException>(() => reader.Slice(int.MaxValue, 1));
        Assert.AreEqual(int.MaxValue, offset);
    }

    /// <summary>Accepts an array whose final element encoding ends exactly at the section end.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeArray_ElementEndingAtSectionEnd_IsAccepted()
    {
        byte[] bytes = [0x08, 0x01, 0x00, 0x00];
        var reader = new R2RNativeReader(bytes);
        var array = new R2RNativeArray(reader, 0, bytes.Length);

        Assert.AreEqual(1U, array.Count);
        Assert.IsTrue(array.TryGetAt(0, out var elementOffset));
        Assert.AreEqual(3, elementOffset);

        var sectionReader = reader.Slice(0, bytes.Length);
        var endOffset = sectionReader.DecodeUnsigned(elementOffset, out var runtimeFunctionIndex);
        Assert.AreEqual(0U, runtimeFunctionIndex);
        Assert.AreEqual(bytes.Length, endOffset);
    }

    /// <summary>Resolves an element through every supported NativeArray block-index width.</summary>
    /// <param name="entryIndexSize">The NativeFormat block-index width selector.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void NativeArray_SupportedIndexWidth_ResolvesElement(int entryIndexSize)
    {
        var bytes = BuildSingleElementNativeArray(entryIndexSize);
        var reader = new R2RNativeReader(bytes);
        var array = new R2RNativeArray(reader, 0, bytes.Length);

        Assert.AreEqual(1U, array.Count);
        Assert.IsTrue(array.TryGetAt(0, out var elementOffset));
        Assert.AreEqual(bytes.Length - 1, elementOffset);

        var endOffset = reader.DecodeUnsigned(elementOffset, out var runtimeFunctionIndex);
        Assert.AreEqual(0U, runtimeFunctionIndex);
        Assert.AreEqual(bytes.Length, endOffset);
    }

    /// <summary>Rejects an array count that cannot be represented by a metadata row id.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeArray_CountBeyondMetadataRowIdRange_IsRejected()
    {
        byte[] bytes = [0x0F, 0xFC, 0xFF, 0xFF, 0xFF];

        _ = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new R2RNativeArray(new R2RNativeReader(bytes), 0, bytes.Length));
    }

    /// <summary>
    /// Rejects an in-range count whose required block-index bytes exceed the declared section.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeArray_BlockIndexBeyondSection_IsRejected()
    {
        byte[] bytes = [0x88, 0x01, 0x01, 0x00, 0x00];

        _ = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new R2RNativeArray(new R2RNativeReader(bytes), 0, 2));
    }

    /// <summary>
    /// Rejects maximum-row-id block arithmetic with a format exception rather than an arithmetic
    /// exception when the required four-byte index table is absent.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeArray_MaximumCountWithTruncatedWideIndex_ThrowsBadImageFormat()
    {
        var bytes = EncodeNativeUnsigned((0x00FF_FFFFU << 2) | 2);

        _ = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new R2RNativeArray(new R2RNativeReader(bytes), 0, bytes.Length));
    }

    /// <summary>Rejects a tree root that targets plausible bytes beyond the declared section.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeArray_TreeRootBeyondSection_IsRejected()
    {
        byte[] bytes = [0x08, 0x02, 0xCC, 0x00, 0x00];
        var array = new R2RNativeArray(new R2RNativeReader(bytes), 0, 2);

        _ = Assert.ThrowsExactly<BadImageFormatException>(() => array.TryGetAt(0, out _));
    }

    /// <summary>Rejects a branch delta that leaves the array after a contained tree root.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeArray_BranchBeyondSection_IsRejected()
    {
        byte[] bytes = [0x48, 0x01, 0x1C, 0xCC, 0xCC, 0x00];
        var array = new R2RNativeArray(new R2RNativeReader(bytes), 0, 3);

        _ = Assert.ThrowsExactly<BadImageFormatException>(() => array.TryGetAt(8, out _));
    }

    /// <summary>Rejects a matching leaf whose element starts exactly at the section end.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeArray_ElementBeyondSection_IsRejected()
    {
        byte[] bytes = [0x08, 0x01, 0x00, 0x00];
        var array = new R2RNativeArray(new R2RNativeReader(bytes), 0, 3);

        _ = Assert.ThrowsExactly<BadImageFormatException>(() => array.TryGetAt(0, out _));
    }

    /// <summary>Accepts an empty hashtable whose final bucket boundary equals the section end.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeHashtable_EmptyBucketEndingAtSectionEnd_IsAccepted()
    {
        byte[] bytes = [0x00, 0x02, 0x02];
        var table = new R2RNativeHashtable(new R2RNativeReader(bytes), 0, bytes.Length);

        Assert.IsEmpty(table.AllEntryOffsets());
    }

    /// <summary>Enumerates one entry through every supported NativeHashtable boundary width.</summary>
    /// <param name="entryIndexSize">The NativeFormat bucket-boundary width selector.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void NativeHashtable_SupportedIndexWidth_EnumeratesEntry(int entryIndexSize)
    {
        var bytes = BuildSingleEntryNativeHashtable(entryIndexSize);
        var table = new R2RNativeHashtable(new R2RNativeReader(bytes), 0, bytes.Length);

        Assert.AreEqual(1, table.BucketCount);
        Assert.AreSequenceEqual([bytes.Length - 1], table.AllEntryOffsets());
    }

    /// <summary>Rejects the reserved NativeHashtable boundary width.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeHashtable_ReservedIndexWidth_IsRejected()
    {
        byte[] bytes = [0x03];

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new R2RNativeHashtable(new R2RNativeReader(bytes), 0, bytes.Length));
        Assert.Contains("entry index size", exception.Message);
    }

    /// <summary>Validates bucket-shift arithmetic at zero, the largest supported shift, and rejects.</summary>
    /// <param name="shift">The encoded base-two bucket-count shift.</param>
    /// <param name="expectedMessage">The expected rejection diagnostic, or null for the valid case.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(0, null)]
    [DataRow(30, "bucket index")]
    [DataRow(31, "too many buckets")]
    [DataRow(32, "too many buckets")]
    [DataRow(63, "too many buckets")]
    public void NativeHashtable_BucketShiftBoundary_UsesRepresentableCount(
        int shift,
        string? expectedMessage)
    {
        var bytes = shift == 0
            ? BuildEmptyNativeHashtable(shift, entryIndexSize: 0)
            : [checked((byte)(shift << 2)), 0, 0];

        if (expectedMessage is null)
        {
            var table = new R2RNativeHashtable(new R2RNativeReader(bytes), 0, bytes.Length);
            Assert.AreEqual(1, table.BucketCount);
            Assert.IsEmpty(table.AllEntryOffsets());
            return;
        }

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new R2RNativeHashtable(new R2RNativeReader(bytes), 0, bytes.Length));
        Assert.Contains(expectedMessage, exception.Message);
    }

    /// <summary>Rejects a bucket index table truncated at the declared section boundary.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeHashtable_TruncatedBucketIndex_IsRejected()
    {
        byte[] bytes = [0x00, 0x02, 0x02];

        _ = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new R2RNativeHashtable(new R2RNativeReader(bytes), 0, 2));
    }

    /// <summary>Rejects a bucket range whose end targets plausible bytes beyond the section.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeHashtable_BucketRangeBeyondSection_IsRejected()
    {
        byte[] bytes = [0x00, 0x02, 0x07, 0x00, 0x02, 0x00, 0x01, 0x00];

        _ = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new R2RNativeHashtable(new R2RNativeReader(bytes), 0, 3));
    }

    /// <summary>
    /// Constructs from valid first/final extents, then rejects an intermediate boundary that moves
    /// backward when enumeration reaches it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeHashtable_NonMonotonicIntermediateRange_IsRejectedDuringEnumeration()
    {
        byte[] bytes = [0x04, 0x03, 0x02, 0x03];
        var table = new R2RNativeHashtable(new R2RNativeReader(bytes), 0, bytes.Length);

        Assert.AreEqual(2, table.BucketCount);
        _ = Assert.ThrowsExactly<BadImageFormatException>(() => table.AllEntryOffsets().ToArray());
    }

    /// <summary>Rejects an entry whose signed payload delta lands exactly outside the section.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeHashtable_PayloadBeyondSection_IsRejected()
    {
        byte[] bytes = [0x00, 0x02, 0x04, 0x00, 0x02, 0x00, 0x01, 0x00];
        var table = new R2RNativeHashtable(new R2RNativeReader(bytes), 0, 5);

        _ = Assert.ThrowsExactly<BadImageFormatException>(() => table.AllEntryOffsets().ToArray());
    }

    /// <summary>Rejects an entry whose relative-offset encoding crosses its bucket boundary.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeHashtable_EntryEncodingBeyondBucket_IsRejected()
    {
        byte[] bytes = [0x00, 0x02, 0x03, 0x00, 0x02, 0x00];
        var table = new R2RNativeHashtable(new R2RNativeReader(bytes), 0, bytes.Length);

        _ = Assert.ThrowsExactly<BadImageFormatException>(() => table.AllEntryOffsets().ToArray());
    }

    /// <summary>
    /// Accepts two MethodDef NativeArrays whose cumulative index count equals the shared method-map
    /// traversal budget.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodMap_MethodDefSourcesAtCumulativeBudget_AreAccepted()
    {
        var first = BuildAbsentNativeArray(1);
        var second = BuildAbsentNativeArray(ReadyToRunTraversalBudget.MaximumWork - 1);
        var (Reader, AddressSpace, RuntimeFunctions, HotColdMap, FirstOffset, SecondOffset) = CreateMethodMapContext(first, second);
        ReadyToRunMethodMapReader.MethodMapSource[] sources =
        [
            CreateMethodMapSource("First", FirstOffset, first.Length),
            CreateMethodMapSource("Second", SecondOffset, second.Length),
        ];

        var methods = ReadyToRunMethodMapReader.Build(
            Reader,
            RuntimeFunctions,
            HotColdMap,
            imageBase: 0x140000000,
            AddressSpace,
            sources,
            globalInstance: null);

        Assert.IsEmpty(methods);
    }

    /// <summary>
    /// Rejects the second MethodDef NativeArray when cumulative index work exceeds the shared
    /// method-map traversal budget by one.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodMap_MethodDefSourcesOneOverCumulativeBudget_AreRejected()
    {
        var first = BuildAbsentNativeArray(ReadyToRunTraversalBudget.MaximumWork);
        var second = BuildAbsentNativeArray(1);
        var (Reader, AddressSpace, RuntimeFunctions, HotColdMap, FirstOffset, SecondOffset) = CreateMethodMapContext(first, second);
        ReadyToRunMethodMapReader.MethodMapSource[] sources =
        [
            CreateMethodMapSource("First", FirstOffset, first.Length),
            CreateMethodMapSource("Second", SecondOffset, second.Length),
        ];

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunMethodMapReader.Build(
                Reader,
                RuntimeFunctions,
                HotColdMap,
                imageBase: 0x140000000,
                AddressSpace,
                sources,
                globalInstance: null));

        Assert.Contains("MethodDefEntryPoints", exception.Message);
        Assert.Contains("1,048,576", exception.Message);
    }

    /// <summary>
    /// Shares the method-map budget between MethodDef indices and instance buckets at the exact
    /// boundary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodMap_MethodDefIndicesAndInstanceBucketsAtBudget_AreAccepted()
    {
        var methodDefinitions = BuildAbsentNativeArray(ReadyToRunTraversalBudget.MaximumWork - 1);
        var instances = BuildEmptyNativeHashtable(shift: 0, entryIndexSize: 0);
        var (Reader, AddressSpace, RuntimeFunctions, HotColdMap, FirstOffset, SecondOffset) = CreateMethodMapContext(methodDefinitions, instances);
        ReadyToRunMethodMapReader.MethodMapSource[] sources =
        [
            CreateMethodMapSource("Methods", FirstOffset, methodDefinitions.Length),
        ];
        var globalInstance = CreateGlobalInstanceSource(
            SecondOffset,
            instances.Length);

        var methods = ReadyToRunMethodMapReader.Build(
            Reader,
            RuntimeFunctions,
            HotColdMap,
            imageBase: 0x140000000,
            AddressSpace,
            sources,
            globalInstance);

        Assert.IsEmpty(methods);
    }

    /// <summary>
    /// Charges an instance entry after its bucket and rejects that entry as the one unit beyond the
    /// method-map traversal budget.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodMap_InstanceEntryOneOverSharedBudget_IsRejected()
    {
        var methodDefinitions = BuildAbsentNativeArray(ReadyToRunTraversalBudget.MaximumWork - 1);
        var instances = BuildSingleEntryNativeHashtable(entryIndexSize: 0);
        var (Reader, AddressSpace, RuntimeFunctions, HotColdMap, FirstOffset, SecondOffset) = CreateMethodMapContext(methodDefinitions, instances);
        ReadyToRunMethodMapReader.MethodMapSource[] sources =
        [
            CreateMethodMapSource("Methods", FirstOffset, methodDefinitions.Length),
        ];
        var globalInstance = CreateGlobalInstanceSource(
            SecondOffset,
            instances.Length);

        var exception = Assert.ThrowsExactly<BadImageFormatException>(() =>
            ReadyToRunMethodMapReader.Build(
                Reader,
                RuntimeFunctions,
                HotColdMap,
                imageBase: 0x140000000,
                AddressSpace,
                sources,
                globalInstance));

        Assert.Contains("InstanceMethodEntryPoints entries", exception.Message);
        Assert.Contains("1,048,576", exception.Message);
    }

    private static byte[] BuildAbsentNativeArray(int count)
    {
        var blockCount = count / 16 + (count % 16 == 0 ? 0 : 1);
        var entryIndexSize = blockCount <= byte.MaxValue
            ? 0
            : blockCount * sizeof(ushort) <= ushort.MaxValue
                ? 1
                : 2;
        var stride = 1 << entryIndexSize;
        var rootOffset = checked(blockCount * stride);
        var header = EncodeNativeUnsigned(checked((uint)(count << 2) | (uint)entryIndexSize));
        var bytes = new byte[checked(header.Length + rootOffset + 1)];
        header.CopyTo(bytes, 0);

        for (var block = 0; block < blockCount; block++)
        {
            WriteIndex(
                bytes,
                header.Length + block * stride,
                entryIndexSize,
                checked((uint)rootOffset));
        }

        bytes[^1] = 0x80;
        return bytes;
    }

    private static byte[] BuildEmptyNativeHashtable(int shift, int entryIndexSize)
    {
        var bucketCount = checked(1 << shift);
        var stride = 1 << entryIndexSize;
        var boundaryBytes = checked((bucketCount + 1) * stride);
        var bytes = new byte[checked(1 + boundaryBytes)];
        bytes[0] = checked((byte)((shift << 2) | entryIndexSize));
        for (var boundary = 0; boundary <= bucketCount; boundary++)
        {
            WriteIndex(
                bytes,
                1 + boundary * stride,
                entryIndexSize,
                checked((uint)boundaryBytes));
        }

        return bytes;
    }

    private static byte[] BuildSingleElementNativeArray(int entryIndexSize)
    {
        var stride = 1 << entryIndexSize;
        var header = EncodeNativeUnsigned(checked((uint)(1 << 2) | (uint)entryIndexSize));
        var bytes = new byte[checked(header.Length + stride + 2)];
        header.CopyTo(bytes, 0);
        WriteIndex(bytes, header.Length, entryIndexSize, checked((uint)stride));
        bytes[header.Length + stride] = 0;
        bytes[^1] = 0;
        return bytes;
    }

    private static byte[] BuildSingleEntryNativeHashtable(int entryIndexSize)
    {
        var stride = 1 << entryIndexSize;
        var bucketDataOffset = 2 * stride;
        var payloadDataOffset = bucketDataOffset + 2;
        var bytes = new byte[checked(1 + payloadDataOffset + 1)];
        bytes[0] = checked((byte)entryIndexSize);
        WriteIndex(bytes, 1, entryIndexSize, checked((uint)bucketDataOffset));
        WriteIndex(bytes, 1 + stride, entryIndexSize, checked((uint)payloadDataOffset));
        bytes[1 + bucketDataOffset] = 0;
        bytes[1 + bucketDataOffset + 1] = 0x02;
        bytes[^1] = 0;
        return bytes;
    }

    private static ReadyToRunMethodMapReader.GlobalInstanceSource CreateGlobalInstanceSource(
        int offset,
        int size) =>
        new(offset, size, Metadata: null, "Instances", Guid.Empty, MethodDefs: []);

    private static ReadyToRunMethodMapReader.MethodMapSource CreateMethodMapSource(
        string name,
        int offset,
        int size) =>
        new(name, Guid.Empty, offset, size, MethodDefs: [], Metadata: null);

    private static (
        R2RNativeReader Reader,
        NativeAddressSpace AddressSpace,
        ReadyToRunRuntimeFunctionTable RuntimeFunctions,
        ReadyToRunHotColdMap HotColdMap,
        int FirstOffset,
        int SecondOffset)
        CreateMethodMapContext(byte[] first, byte[] second)
    {
        var section = new byte[checked(first.Length + second.Length)];
        first.CopyTo(section, 0);
        second.CopyTo(section, first.Length);
        var image = SyntheticImageBuilders.BuildPe(0x8664, section, 0, 0);
        var reader = new R2RNativeReader(image);
        var addressSpace = NativeAddressSpace.Create(image);
        Assert.IsNotNull(addressSpace);

        var runtimeFunctionsValid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            sectionSize: 0,
            NativeArchitecture.X64,
            imageBase: 0x140000000,
            addressSpace,
            out var runtimeFunctions,
            out var runtimeDiagnostic);
        Assert.IsTrue(runtimeFunctionsValid, runtimeDiagnostic);
        Assert.IsNotNull(runtimeFunctions);

        var hotColdValid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            sectionFileOffset: null,
            sectionSize: 0,
            totalRuntimeFunctions: 0,
            out var hotColdMap,
            out var hotColdDiagnostic);
        Assert.IsTrue(hotColdValid, hotColdDiagnostic);
        Assert.IsNotNull(hotColdMap);

        return (
            reader,
            addressSpace,
            runtimeFunctions,
            hotColdMap,
            SectionFileOffset,
            checked(SectionFileOffset + first.Length));
    }

    private static byte[] EncodeNativeUnsigned(uint value)
    {
        if (value <= 0x7F)
        {
            return [checked((byte)(value << 1))];
        }

        if (value <= 0x3FFF)
        {
            return [(byte)((value << 2) | 1), checked((byte)(value >> 6))];
        }

        if (value <= 0x1F_FFFF)
        {
            return
            [
                (byte)((value << 3) | 3),
                (byte)(value >> 5),
                (byte)(value >> 13),
            ];
        }

        if (value <= 0x0FFF_FFFF)
        {
            return
            [
                (byte)((value << 4) | 7),
                (byte)(value >> 4),
                (byte)(value >> 12),
                (byte)(value >> 20),
            ];
        }

        var encoded = new byte[5];
        encoded[0] = 0x0F;
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(1), value);
        return encoded;
    }

    private static void WriteIndex(
        byte[] destination,
        int offset,
        int entryIndexSize,
        uint value)
    {
        switch (entryIndexSize)
        {
            case 0:
                destination[offset] = checked((byte)value);
                break;
            case 1:
                BinaryPrimitives.WriteUInt16LittleEndian(
                    destination.AsSpan(offset),
                    checked((ushort)value));
                break;
            case 2:
                BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset), value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entryIndexSize));
        }
    }
}
