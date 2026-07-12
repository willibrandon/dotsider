using Dotsider.Core.Analysis.ReadyToRun;

namespace Dotsider.Tests;

/// <summary>
/// Verifies NativeFormat array and hashtable cursors remain inside their declared ReadyToRun
/// section rather than consuming otherwise-valid bytes from adjacent image data.
/// </summary>
[TestClass]
public sealed class ReadyToRunNativeContainerTests
{
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

    /// <summary>Rejects bucket boundaries that move backward and would repeat contained bytes.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeHashtable_NonMonotonicBucketRange_IsRejected()
    {
        byte[] bytes = [0x00, 0x04, 0x02, 0x00, 0x02];

        _ = Assert.ThrowsExactly<BadImageFormatException>(() =>
            new R2RNativeHashtable(new R2RNativeReader(bytes), 0, bytes.Length));
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
}
