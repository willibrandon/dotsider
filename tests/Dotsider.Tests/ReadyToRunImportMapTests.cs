using Dotsider.Core.Analysis.ReadyToRun;

namespace Dotsider.Tests;

/// <summary>
/// Verifies bounded ReadyToRun import-record iteration.
/// </summary>
[TestClass]
public sealed class ReadyToRunImportMapTests
{
    /// <summary>
    /// Verifies only completely mapped slot and signature arrays produce an iteration count.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryGetSlotCount_RequiresFullyBackedExtents()
    {
        Assert.IsTrue(ReadyToRunImportMap.TryGetSlotCount(
            slotsSize: 16,
            entrySize: 8,
            slotBytesAvailable: 16,
            signatureBytesAvailable: 8,
            out var count));
        Assert.AreEqual(2, count);

        Assert.IsFalse(ReadyToRunImportMap.TryGetSlotCount(
            slotsSize: 16,
            entrySize: 8,
            slotBytesAvailable: 15,
            signatureBytesAvailable: 8,
            out _));
        Assert.IsFalse(ReadyToRunImportMap.TryGetSlotCount(
            slotsSize: 15,
            entrySize: 8,
            slotBytesAvailable: 15,
            signatureBytesAvailable: 8,
            out _));
        Assert.IsFalse(ReadyToRunImportMap.TryGetSlotCount(
            slotsSize: 16,
            entrySize: 8,
            slotBytesAvailable: 16,
            signatureBytesAvailable: 7,
            out _));
    }

    /// <summary>
    /// Verifies a forged maximal slot count is rejected arithmetically without iteration.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryGetSlotCount_MaximalForgedCount_IsRejected()
    {
        Assert.IsFalse(ReadyToRunImportMap.TryGetSlotCount(
            slotsSize: int.MaxValue,
            entrySize: 1,
            slotBytesAvailable: int.MaxValue,
            signatureBytesAvailable: int.MaxValue,
            out var count));
        Assert.AreEqual(0, count);
    }
}
