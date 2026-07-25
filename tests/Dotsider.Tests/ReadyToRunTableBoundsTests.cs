using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.ReadyToRun;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Verifies the structural and resource boundaries applied before ReadyToRun image-wide tables
/// allocate or expose method-map data.
/// </summary>
[TestClass]
public sealed class ReadyToRunTableBoundsTests
{
    private const int SectionFileOffset = 0x400;

    /// <summary>Verifies empty tables are valid for every supported runtime-function layout.</summary>
    /// <param name="architecture">A supported architecture.</param>
    [TestMethod]
    [DataRow(NativeArchitecture.X64)]
    [DataRow(NativeArchitecture.Arm64)]
    [DataRow(NativeArchitecture.X86)]
    [DataRow(NativeArchitecture.Arm32)]
    [DataRow(NativeArchitecture.RiscV64)]
    [DataRow(NativeArchitecture.LoongArch64)]
    [DataRow(NativeArchitecture.Wasm32)]
    public void RuntimeFunctions_EmptySupportedLayout_IsAccepted(NativeArchitecture architecture)
    {
        var (reader, addressSpace) = CreateImage([]);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            0,
            architecture,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsTrue(valid);
        Assert.IsNotNull(table);
        Assert.AreEqual(0, table.Count);
        Assert.IsNull(diagnostic);
    }

    /// <summary>Verifies the complete amd64 record layout produces its declared code range.</summary>
    [TestMethod]
    public void RuntimeFunctions_ExactAmd64Record_IsAccepted()
    {
        var record = SyntheticImageBuilders.Amd64RuntimeFunction(0x1000, 0x1010, 0);
        var (reader, addressSpace) = CreateImage(record);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            record.Length,
            NativeArchitecture.X64,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsTrue(valid);
        Assert.IsNotNull(table);
        Assert.AreEqual(1, table.Count);
        Assert.AreEqual(0x1000, table.StartRva(0));
        Assert.AreEqual(0x10, table.Size(0));
        Assert.IsNull(diagnostic);
    }

    /// <summary>
    /// Verifies a non-amd64 layout derives a range from the following runtime-function start.
    /// </summary>
    [TestMethod]
    public void RuntimeFunctions_ExactNonAmd64Records_AreAccepted()
    {
        var records = new byte[16];
        WriteNonAmd64Record(records, 0, 0x1000, 0);
        WriteNonAmd64Record(records, 8, 0x1010, 0);
        var (reader, addressSpace) = CreateImage(records);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            records.Length,
            NativeArchitecture.Arm64,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsTrue(valid);
        Assert.IsNotNull(table);
        Assert.AreEqual(2, table.Count);
        Assert.AreEqual(0x10, table.Size(0));
        Assert.AreEqual(0, table.Size(1));
        Assert.IsNull(diagnostic);
    }

    /// <summary>Verifies the exact runtime-function resource budget remains accepted.</summary>
    /// <param name="architecture">The record layout to exercise.</param>
    /// <param name="recordSize">The architecture's runtime-function record size.</param>
    [TestMethod]
    [DataRow(NativeArchitecture.X64, 12)]
    [DataRow(NativeArchitecture.Arm64, 8)]
    public void RuntimeFunctions_ExactRecordBudget_IsAccepted(
        NativeArchitecture architecture,
        int recordSize)
    {
        var records = new byte[ReadyToRunRuntimeFunctionTable.MaxRuntimeFunctionCount * recordSize];
        var (reader, addressSpace) = CreateImage(records);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            records.Length,
            architecture,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsTrue(valid);
        Assert.IsNotNull(table);
        Assert.AreEqual(ReadyToRunRuntimeFunctionTable.MaxRuntimeFunctionCount, table.Count);
        Assert.IsNull(diagnostic);
    }

    /// <summary>Verifies one record above the runtime-function budget is rejected before allocation.</summary>
    /// <param name="architecture">The record layout to exercise.</param>
    /// <param name="recordSize">The architecture's runtime-function record size.</param>
    [TestMethod]
    [DataRow(NativeArchitecture.X64, 12)]
    [DataRow(NativeArchitecture.Arm64, 8)]
    public void RuntimeFunctions_AboveRecordBudget_IsRejected(
        NativeArchitecture architecture,
        int recordSize)
    {
        var (reader, addressSpace) = CreateImage([]);
        var declaredSize = checked(
            (ReadyToRunRuntimeFunctionTable.MaxRuntimeFunctionCount + 1) * recordSize);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            declaredSize,
            architecture,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(table);
        Assert.Contains("1,048,576", diagnostic!);
    }

    /// <summary>Verifies signed and partial-record section sizes are rejected.</summary>
    /// <param name="sectionSize">The malformed declared size.</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    [DataRow(7)]
    [DataRow(9)]
    public void RuntimeFunctions_InvalidSectionSize_IsRejected(int sectionSize)
    {
        var (reader, addressSpace) = CreateImage(new byte[16]);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            sectionSize,
            NativeArchitecture.Arm64,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(table);
        Assert.IsNotEmpty(diagnostic!);
    }

    /// <summary>Verifies a table cannot consume bytes beyond its file-backed segment.</summary>
    [TestMethod]
    public void RuntimeFunctions_RangeBeyondMappedSegment_IsRejected()
    {
        var (reader, addressSpace) = CreateImage(new byte[8]);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            0x208,
            NativeArchitecture.Arm64,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(table);
        Assert.Contains("file-backed image segment", diagnostic!);
    }

    /// <summary>Verifies an unsupported architecture never selects a guessed record layout.</summary>
    [TestMethod]
    public void RuntimeFunctions_UnknownArchitecture_IsRejected()
    {
        var (reader, addressSpace) = CreateImage([]);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            0,
            NativeArchitecture.Unknown,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(table);
        Assert.Contains("unsupported architecture", diagnostic!);
    }

    /// <summary>
    /// Verifies unsigned RVA differences are widened before subtraction and clamped to mapped bytes.
    /// </summary>
    [TestMethod]
    public void RuntimeFunctions_LargeUnsignedEndRva_IsClampedWithoutOverflow()
    {
        var record = SyntheticImageBuilders.Amd64RuntimeFunction(0x1000, 0x8000_0000, 0);
        var (reader, addressSpace) = CreateImage(record);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            record.Length,
            NativeArchitecture.X64,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsTrue(valid);
        Assert.IsNotNull(table);
        Assert.AreEqual(0x200, table.Size(0));
        Assert.IsNull(diagnostic);
    }

    /// <summary>Verifies descending starts and amd64 end-before-start records are rejected.</summary>
    /// <param name="malformation">The RVA-order rule to violate.</param>
    [TestMethod]
    [DataRow("DescendingStarts")]
    [DataRow("EndBeforeStart")]
    [DataRow("NonAmd64DescendingStarts")]
    public void RuntimeFunctions_InvalidRvaOrder_IsRejected(string malformation)
    {
        (byte[] Records, NativeArchitecture Architecture) testCase = malformation switch
        {
            "DescendingStarts" => (
                [
                    .. SyntheticImageBuilders.Amd64RuntimeFunction(0x1010, 0x1020, 0),
                    .. SyntheticImageBuilders.Amd64RuntimeFunction(0x1000, 0x1010, 0),
                ],
                NativeArchitecture.X64),
            "EndBeforeStart" => (
                SyntheticImageBuilders.Amd64RuntimeFunction(0x1010, 0x1000, 0),
                NativeArchitecture.X64),
            "NonAmd64DescendingStarts" => (
                BuildNonAmd64Records((0x1010, 0), (0x1000, 0)),
                NativeArchitecture.Arm64),
            _ => throw new ArgumentOutOfRangeException(nameof(malformation)),
        };
        var (reader, addressSpace) = CreateImage(testCase.Records);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            testCase.Records.Length,
            testCase.Architecture,
            0x1400_0000_0,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(table);
        Assert.Contains("RVA range order", diagnostic!);
    }

    /// <summary>Verifies image-base additions for both range boundaries must be representable.</summary>
    /// <param name="overflow">The range boundary whose addition overflows.</param>
    [TestMethod]
    [DataRow("Start")]
    [DataRow("End")]
    public void RuntimeFunctions_OverflowingVirtualAddress_IsRejected(string overflow)
    {
        var endRva = overflow == "End" ? 0x2000U : 0x1010U;
        var imageBase = overflow == "End" ? ulong.MaxValue - 0x1800 : ulong.MaxValue - 0x800;
        var record = SyntheticImageBuilders.Amd64RuntimeFunction(0x1000, endRva, 0);
        var (reader, addressSpace) = CreateImage(record);

        var valid = ReadyToRunRuntimeFunctionTable.TryRead(
            reader,
            SectionFileOffset,
            record.Length,
            NativeArchitecture.X64,
            imageBase,
            addressSpace,
            out var table,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(table);
        Assert.Contains("overflowing virtual address", diagnostic!);
    }

    /// <summary>Verifies an absent hot/cold section is a valid empty map.</summary>
    /// <param name="totalRuntimeFunctions">The enclosing runtime-function count.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(10)]
    public void HotColdMap_AbsentSection_IsAccepted(int totalRuntimeFunctions)
    {
        var (reader, addressSpace) = CreateImage([]);

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            null,
            0,
            totalRuntimeFunctions,
            out var map,
            out var diagnostic);

        Assert.IsTrue(valid);
        Assert.IsNotNull(map);
        Assert.AreEqual(totalRuntimeFunctions, map.FirstColdRuntimeFunction);
        Assert.IsFalse(map.TryGetColdRange(1, out _, out _));
        Assert.IsNull(diagnostic);
    }

    /// <summary>Verifies ordered hot/cold pairs produce exact contiguous cold ranges.</summary>
    [TestMethod]
    public void HotColdMap_OrderedPairs_AreAccepted()
    {
        var pairs = BuildHotColdPairs((6, 1), (8, 3));
        var (reader, addressSpace) = CreateImage(pairs);

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            SectionFileOffset,
            pairs.Length,
            10,
            out var map,
            out var diagnostic);

        Assert.IsTrue(valid);
        Assert.IsNotNull(map);
        Assert.AreEqual(6, map.FirstColdRuntimeFunction);
        Assert.IsTrue(map.TryGetColdRange(1, out var firstStart, out var firstCount));
        Assert.AreEqual(6, firstStart);
        Assert.AreEqual(2, firstCount);
        Assert.IsTrue(map.TryGetColdRange(3, out var secondStart, out var secondCount));
        Assert.AreEqual(8, secondStart);
        Assert.AreEqual(2, secondCount);
        Assert.IsFalse(map.TryGetColdRange(2, out _, out _));
        Assert.IsNull(diagnostic);
    }

    /// <summary>
    /// Verifies a fully populated hot/cold map at the runtime-function boundary remains accepted.
    /// </summary>
    [TestMethod]
    public void HotColdMap_ExactRuntimeFunctionBoundary_IsAccepted()
    {
        var total = ReadyToRunRuntimeFunctionTable.MaxRuntimeFunctionCount;
        var pairCount = total / 2;
        var pairs = new byte[total * sizeof(int)];
        for (var i = 0; i < pairCount; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(pairs.AsSpan(i * 8), pairCount + i);
            BinaryPrimitives.WriteInt32LittleEndian(pairs.AsSpan(i * 8 + 4), i);
        }
        var (reader, addressSpace) = CreateImage(pairs);

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            SectionFileOffset,
            pairs.Length,
            total,
            out var map,
            out var diagnostic);

        Assert.IsTrue(valid);
        Assert.IsNotNull(map);
        Assert.AreEqual(pairCount, map.FirstColdRuntimeFunction);
        Assert.IsTrue(map.TryGetColdRange(pairCount - 1, out var start, out var count));
        Assert.AreEqual(total - 1, start);
        Assert.AreEqual(1, count);
        Assert.IsNull(diagnostic);
    }

    /// <summary>Verifies a hot/cold section must contain complete pairs.</summary>
    [TestMethod]
    public void HotColdMap_PartialPair_IsRejected()
    {
        var (reader, addressSpace) = CreateImage(new byte[8]);

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            SectionFileOffset,
            4,
            10,
            out var map,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(map);
        Assert.Contains("complete 8-byte pairs", diagnostic!);
    }

    /// <summary>Verifies invalid signed and absent hot/cold ranges are rejected.</summary>
    /// <param name="malformation">The section-range rule to violate.</param>
    [TestMethod]
    [DataRow("NegativeSize")]
    [DataRow("MissingOffset")]
    public void HotColdMap_InvalidSectionRange_IsRejected(string malformation)
    {
        var (reader, addressSpace) = CreateImage(new byte[8]);
        var offset = malformation == "MissingOffset" ? null : (int?)SectionFileOffset;
        var size = malformation == "NegativeSize" ? -1 : 8;

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            offset,
            size,
            10,
            out var map,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(map);
        Assert.IsNotEmpty(diagnostic!);
    }

    /// <summary>Verifies the flattened hot/cold entry count cannot exceed its runtime-function table.</summary>
    [TestMethod]
    public void HotColdMap_EntryCountAboveRuntimeFunctionCount_IsRejected()
    {
        var pairs = BuildHotColdPairs((1, 0));
        var (reader, addressSpace) = CreateImage(pairs);

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            SectionFileOffset,
            pairs.Length,
            1,
            out var map,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(map);
        Assert.Contains("2 indices for 1 runtime functions", diagnostic!);
    }

    /// <summary>Verifies every hot/cold index must lie inside the runtime-function table.</summary>
    /// <param name="index">The pair field to place outside the table.</param>
    [TestMethod]
    [DataRow("Cold")]
    [DataRow("Hot")]
    public void HotColdMap_OutOfRangeIndex_IsRejected(string index)
    {
        var pairs = index == "Cold"
            ? BuildHotColdPairs((10, 1))
            : BuildHotColdPairs((6, 10));
        var (reader, addressSpace) = CreateImage(pairs);

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            SectionFileOffset,
            pairs.Length,
            10,
            out var map,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(map);
        Assert.Contains("out-of-range", diagnostic!);
    }

    /// <summary>Verifies the enclosing runtime-function count must itself be within its budget.</summary>
    /// <param name="totalRuntimeFunctions">The invalid enclosing count.</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(ReadyToRunRuntimeFunctionTable.MaxRuntimeFunctionCount + 1)]
    public void HotColdMap_InvalidRuntimeFunctionBound_IsRejected(int totalRuntimeFunctions)
    {
        var (reader, addressSpace) = CreateImage([]);

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            null,
            0,
            totalRuntimeFunctions,
            out var map,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(map);
        Assert.Contains("invalid runtime-function bound", diagnostic!);
    }

    /// <summary>Verifies cold and hot indexes obey the runtime's ordered partition.</summary>
    /// <param name="malformation">The ordering rule to violate.</param>
    [TestMethod]
    [DataRow("ColdNotIncreasing")]
    [DataRow("HotNotIncreasing")]
    [DataRow("HotInColdRegion")]
    public void HotColdMap_InvalidOrdering_IsRejected(string malformation)
    {
        var pairs = malformation switch
        {
            "ColdNotIncreasing" => BuildHotColdPairs((6, 1), (6, 2)),
            "HotNotIncreasing" => BuildHotColdPairs((6, 2), (8, 1)),
            "HotInColdRegion" => BuildHotColdPairs((6, 6)),
            _ => throw new ArgumentOutOfRangeException(nameof(malformation)),
        };
        var (reader, addressSpace) = CreateImage(pairs);

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            SectionFileOffset,
            pairs.Length,
            10,
            out var map,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(map);
        Assert.Contains("required hot/cold order", diagnostic!);
    }

    /// <summary>Verifies a hot/cold map cannot consume bytes beyond its file-backed segment.</summary>
    [TestMethod]
    public void HotColdMap_RangeBeyondMappedSegment_IsRejected()
    {
        var (reader, addressSpace) = CreateImage(new byte[8]);

        var valid = ReadyToRunHotColdMap.TryRead(
            reader,
            addressSpace,
            SectionFileOffset,
            0x208,
            0x400,
            out var map,
            out var diagnostic);

        Assert.IsFalse(valid);
        Assert.IsNull(map);
        Assert.Contains("file-backed image segment", diagnostic!);
    }

    private static byte[] BuildHotColdPairs(params (int Cold, int Hot)[] pairs)
    {
        var bytes = new byte[pairs.Length * 8];
        for (var i = 0; i < pairs.Length; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 8), pairs[i].Cold);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 8 + 4), pairs[i].Hot);
        }

        return bytes;
    }

    private static byte[] BuildNonAmd64Records(params (int StartRva, int UnwindRva)[] records)
    {
        var bytes = new byte[records.Length * 8];
        for (var i = 0; i < records.Length; i++)
        {
            WriteNonAmd64Record(bytes, i * 8, records[i].StartRva, records[i].UnwindRva);
        }

        return bytes;
    }

    private static (R2RNativeReader Reader, NativeAddressSpace AddressSpace) CreateImage(byte[] data)
    {
        var image = SyntheticImageBuilders.BuildPe(0x8664, data, 0, 0);
        var addressSpace = NativeAddressSpace.Create(image);
        Assert.IsNotNull(addressSpace);
        return (new R2RNativeReader(image), addressSpace);
    }

    private static void WriteNonAmd64Record(byte[] destination, int offset, int startRva, int unwindRva)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset), startRva);
        BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(offset + 4), unwindRva);
    }
}
