using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.ReadyToRun;

namespace Dotsider.Tests;

/// <summary>
/// Verifies bounded ReadyToRun import-record iteration.
/// </summary>
[TestClass]
public sealed class ReadyToRunImportMapTests
{
    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

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

    /// <summary>
    /// Validates the largest signature-backed count, the next count, and signed arithmetic
    /// boundaries without allocating or iterating the declared slots.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryGetSlotCount_IntegerBoundaries_AreValidatedWithoutOverflow()
    {
        const int largestSignatureBackedCount = int.MaxValue / sizeof(uint);

        Assert.IsTrue(ReadyToRunImportMap.TryGetSlotCount(
            slotsSize: largestSignatureBackedCount,
            entrySize: 1,
            slotBytesAvailable: largestSignatureBackedCount,
            signatureBytesAvailable: int.MaxValue,
            out var exactCount));
        Assert.AreEqual(largestSignatureBackedCount, exactCount);

        Assert.IsFalse(ReadyToRunImportMap.TryGetSlotCount(
            slotsSize: largestSignatureBackedCount + 1,
            entrySize: 1,
            slotBytesAvailable: largestSignatureBackedCount + 1,
            signatureBytesAvailable: int.MaxValue,
            out var overCount));
        Assert.AreEqual(0, overCount);

        Assert.IsTrue(ReadyToRunImportMap.TryGetSlotCount(
            slotsSize: int.MaxValue,
            entrySize: int.MaxValue,
            slotBytesAvailable: int.MaxValue,
            signatureBytesAvailable: sizeof(uint),
            out var singleCount));
        Assert.AreEqual(1, singleCount);
    }

    /// <summary>Rejects negative signed extents and divisors before any count arithmetic.</summary>
    /// <param name="slotsSize">The declared slot-region byte size.</param>
    /// <param name="entrySize">The declared size of one slot.</param>
    /// <param name="slotBytesAvailable">The mapped bytes available for slots.</param>
    /// <param name="signatureBytesAvailable">The mapped bytes available for signature RVAs.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(int.MinValue, 1, int.MaxValue, int.MaxValue)]
    [DataRow(1, int.MinValue, int.MaxValue, int.MaxValue)]
    [DataRow(1, 1, int.MinValue, int.MaxValue)]
    [DataRow(1, 1, int.MaxValue, int.MinValue)]
    public void TryGetSlotCount_NegativeExtent_IsRejected(
        int slotsSize,
        int entrySize,
        int slotBytesAvailable,
        int signatureBytesAvailable)
    {
        Assert.IsFalse(ReadyToRunImportMap.TryGetSlotCount(
            slotsSize,
            entrySize,
            slotBytesAvailable,
            signatureBytesAvailable,
            out var count));
        Assert.AreEqual(0, count);
    }

    /// <summary>
    /// Accepts exactly 1,048,576 cumulative import slots across two records and reads the final
    /// record while the independent method-map budget remains usable.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_ImportSlotsAtCumulativeBudget_ReadsLaterRecord()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchImportSlotBudget(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunTraversalBudget.MaximumWork - 1,
            secondCount: 1);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);
        var map = ReadyToRunImportMap.Build(analyzer);

        Assert.IsNotEmpty(analyzer.ReadyToRunMethods);
        Assert.IsNotNull(analyzer.ReadyToRunIndex);
        Assert.IsNotNull(map);
        Assert.IsTrue(map.TryResolve(patched.FirstSlotVirtualAddress, out var first));
        Assert.AreEqual("DelayLoad_MethodCall", first.Name);
        Assert.IsTrue(map.TryResolve(patched.SecondSlotVirtualAddress, out var resolved));
        Assert.AreEqual("DelayLoad_MethodCall", resolved.Name);
    }

    /// <summary>
    /// Stops before a later import record when its first slot would exceed the image-wide budget
    /// by one, without disabling the independently budgeted method map.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_ImportSlotsOneOverCumulativeBudget_SkipsLaterRecord()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchImportSlotBudget(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunTraversalBudget.MaximumWork,
            secondCount: 1);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);
        var map = ReadyToRunImportMap.Build(analyzer);

        Assert.IsNotEmpty(analyzer.ReadyToRunMethods);
        Assert.IsNotNull(analyzer.ReadyToRunIndex);
        Assert.IsNotNull(map);
        Assert.IsTrue(map.TryResolve(patched.FirstSlotVirtualAddress, out var prefix));
        Assert.AreEqual("DelayLoad_MethodCall", prefix.Name);
        Assert.IsFalse(map.TryResolve(patched.SecondSlotVirtualAddress, out _));
    }

    /// <summary>Preserves a valid import slot immediately before a malformed slot.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_MalformedSlot_PreservesValidPrefix()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchImportValidThenMalformedSlots(
            Samples.ReadyToRunConsoleDll!);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);
        var map = ReadyToRunImportMap.Build(analyzer);

        Assert.IsNotNull(map);
        Assert.IsTrue(map.TryResolve(patched.ValidSlotVirtualAddress, out var resolved));
        Assert.AreEqual("DelayLoad_MethodCall", resolved.Name);
        Assert.IsFalse(map.TryResolve(patched.MalformedSlotVirtualAddress, out _));
    }

    /// <summary>
    /// Rejects signed and unsigned-extreme import RVAs without escaping an arithmetic exception or
    /// manufacturing a symbol for the otherwise-valid slot location.
    /// </summary>
    /// <param name="forgeSlotsRva">Whether the slots RVA rather than signature-table RVA is forged.</param>
    /// <param name="forgedRva">The record-field value.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(false, int.MinValue)]
    [DataRow(false, -1)]
    [DataRow(false, int.MaxValue)]
    [DataRow(true, int.MinValue)]
    [DataRow(true, -1)]
    [DataRow(true, int.MaxValue)]
    public void Build_ExtremeImportRva_RejectsRecordWithoutArithmeticEscape(
        bool forgeSlotsRva,
        int forgedRva)
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchImportRvaBoundary(
            Samples.ReadyToRunConsoleDll!,
            forgeSlotsRva,
            forgedRva);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);
        var map = ReadyToRunImportMap.Build(analyzer);

        Assert.AreEqual(ReadyToRunStatus.Valid, analyzer.ReadyToRunInfo!.Status);
        Assert.IsNotEmpty(analyzer.ReadyToRunMethods);
        Assert.IsNotNull(map);
        Assert.IsFalse(map.TryResolve(patched.ValidSlotVirtualAddress, out _));
    }

    /// <summary>Rejects overflowing and negative import-section ranges before record traversal.</summary>
    /// <param name="declaredSize">The forged signed import-section size.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(-1)]
    [DataRow(int.MaxValue)]
    public void Build_InvalidImportSectionRange_ReturnsNull(int declaredSize)
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        var patched = ReadyToRunImagePatcher.PatchImageWideTable(
            Samples.ReadyToRunConsoleDll!,
            ReadyToRunSectionType.ImportSections,
            [0],
            declaredSize);

        using var analyzer = new AssemblyAnalyzer(patched.Image, Samples.ReadyToRunConsoleDll!);
        var map = ReadyToRunImportMap.Build(analyzer);

        Assert.AreEqual(ReadyToRunStatus.Valid, analyzer.ReadyToRunInfo!.Status);
        Assert.IsNotEmpty(analyzer.ReadyToRunMethods);
        Assert.IsNull(map);
    }
}
