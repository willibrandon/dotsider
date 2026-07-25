using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Verifies format-2.2 deduplicated-method count and pair boundaries through the public mstat
/// reader facade.
/// </summary>
[TestClass]
public sealed class MstatDeduplicatedMethodBoundsTests
{
    private const long AllocationTolerance = 1024L * 1024;
    private const int AllocationMeasurementCount = 5;

    /// <summary>
    /// A zero target count produces one completed row with an empty target set.
    /// </summary>
    [TestMethod]
    public void ReadDeduplicatedMethods_ZeroCount_ReturnsEmptyTargetSet()
    {
        var data = Read(SyntheticMstat22Builder.Create([0], [0]));

        var method = Assert.ContainsSingle(data.DeduplicatedMethods);
        Assert.AreEqual("Fixture.Worker::Original1", method.Name);
        Assert.IsEmpty(method.TargetNames);
    }

    /// <summary>
    /// One complete target pair resolves its serialized dependency-node name.
    /// </summary>
    [TestMethod]
    public void ReadDeduplicatedMethods_ExactCount_ReturnsNamedTarget()
    {
        var data = Read(SyntheticMstat22Builder.Create([1], [1]));

        var method = Assert.ContainsSingle(data.DeduplicatedMethods);
        Assert.AreEqual("Fixture.Worker::Original1", method.Name);
        Assert.HasCount(1, method.TargetNames);
        Assert.AreEqual("Folded target 1", method.TargetNames[0]);
    }

    /// <summary>
    /// Multiple complete target pairs preserve every serialized node name in writer order.
    /// </summary>
    [TestMethod]
    public void ReadDeduplicatedMethods_MultipleCount_ReturnsEveryNamedTargetInOrder()
    {
        var data = Read(SyntheticMstat22Builder.Create([3], [3]));

        var method = Assert.ContainsSingle(data.DeduplicatedMethods);
        Assert.HasCount(3, method.TargetNames);
        Assert.AreEqual("Folded target 1", method.TargetNames[0]);
        Assert.AreEqual("Folded target 2", method.TargetNames[1]);
        Assert.AreEqual("Folded target 3", method.TargetNames[2]);
    }

    /// <summary>
    /// Counts that are negative or cannot fit in the remaining IL omit the current row while
    /// preserving independent sections.
    /// </summary>
    /// <param name="count">The malformed declared target count.</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(1)]
    [DataRow(int.MaxValue)]
    public void ReadDeduplicatedMethods_InvalidCount_OmitsCurrentRowAndPreservesOtherSections(
        int count)
    {
        var data = Read(SyntheticMstat22Builder.Create([count], [0]));

        Assert.IsEmpty(data.DeduplicatedMethods);
        AssertIndependentSections(data);
    }

    /// <summary>
    /// A physically truncated four-byte count operand omits the row without damaging
    /// independent streams.
    /// </summary>
    [TestMethod]
    public void ReadDeduplicatedMethods_TruncatedCount_OmitsCurrentRowAndPreservesOtherSections()
    {
        var data = Read(SyntheticMstat22Builder.Create(
            [0],
            [0],
            SyntheticMstat22Fault.TruncatedCount));

        Assert.IsEmpty(data.DeduplicatedMethods);
        AssertIndependentSections(data);
    }

    /// <summary>
    /// A target pair ending after its token omits that row and retains a previously completed
    /// deduplication row.
    /// </summary>
    [TestMethod]
    public void ReadDeduplicatedMethods_TruncatedPair_KeepsCompletedPrefix()
    {
        var data = Read(SyntheticMstat22Builder.Create(
            [1, 1],
            [1, 1],
            SyntheticMstat22Fault.TruncatedTargetNameOffset));

        var completed = Assert.ContainsSingle(data.DeduplicatedMethods);
        Assert.AreEqual("Fixture.Worker::Original1", completed.Name);
        Assert.HasCount(1, completed.TargetNames);
        Assert.AreEqual("Folded target 1", completed.TargetNames[0]);
        AssertIndependentSections(data);
    }

    /// <summary>
    /// An invalid target-token opcode omits the damaged row and retains a previously completed
    /// deduplication row.
    /// </summary>
    [TestMethod]
    public void ReadDeduplicatedMethods_MalformedTargetToken_KeepsCompletedPrefix()
    {
        var data = Read(SyntheticMstat22Builder.Create(
            [1, 1],
            [1, 1],
            SyntheticMstat22Fault.MalformedTargetToken));

        var completed = Assert.ContainsSingle(data.DeduplicatedMethods);
        Assert.AreEqual("Fixture.Worker::Original1", completed.Name);
        Assert.AreEqual("Folded target 1", Assert.ContainsSingle(completed.TargetNames));
        AssertIndependentSections(data);
    }

    /// <summary>
    /// A current row remains atomic when one pair completes and the next name operand is
    /// malformed; only the earlier completed row is returned.
    /// </summary>
    [TestMethod]
    public void ReadDeduplicatedMethods_MalformedTargetNameOffset_OmitsPartialCurrentRow()
    {
        var data = Read(SyntheticMstat22Builder.Create(
            [1, 2],
            [1, 2],
            SyntheticMstat22Fault.MalformedTargetNameOffset));

        var completed = Assert.ContainsSingle(data.DeduplicatedMethods);
        Assert.AreEqual("Fixture.Worker::Original1", completed.Name);
        Assert.AreEqual("Folded target 1", Assert.ContainsSingle(completed.TargetNames));
        AssertIndependentSections(data);
    }

    /// <summary>
    /// A structurally complete pair whose name offset is out of range keeps the original row
    /// while omitting the unavailable target name.
    /// </summary>
    [TestMethod]
    public void ReadDeduplicatedMethods_OutOfRangeTargetNameOffset_KeepsRowWithoutTarget()
    {
        var data = Read(SyntheticMstat22Builder.Create(
            [1],
            [1],
            SyntheticMstat22Fault.OutOfRangeTargetNameOffset));

        var method = Assert.ContainsSingle(data.DeduplicatedMethods);
        Assert.AreEqual("Fixture.Worker::Original1", method.Name);
        Assert.IsEmpty(method.TargetNames);
        AssertIndependentSections(data);
    }

    /// <summary>
    /// A tiny stream declaring two million targets allocates within one MiB of a warmed
    /// zero-count baseline rather than reserving storage proportional to the declaration.
    /// </summary>
    [TestMethod]
    public void ReadDeduplicatedMethods_InflatedCount_DoesNotAllocateFromDeclaredCount()
    {
        var baseline = SyntheticMstat22Builder.Create([0], [0]);
        var hostile = SyntheticMstat22Builder.Create([2_000_000], [0]);
        _ = Read(baseline);
        _ = Read(hostile);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var baselineMinimum = long.MaxValue;
        var hostileMinimum = long.MaxValue;
        for (var i = 0; i < AllocationMeasurementCount; i++)
        {
            baselineMinimum = Math.Min(baselineMinimum, MeasureAllocation(baseline, 1));
            hostileMinimum = Math.Min(hostileMinimum, MeasureAllocation(hostile, 0));
        }

        Assert.IsLessThanOrEqualTo(
            baselineMinimum + AllocationTolerance,
            hostileMinimum,
            $"Hostile read allocated {hostileMinimum:N0} bytes; baseline allocated "
            + $"{baselineMinimum:N0} bytes.");
    }

    private static MstatData Read(byte[] image)
    {
        using var stream = new MemoryStream(image, writable: false);
        var data = MstatReader.Read(stream);
        Assert.IsNotNull(data);
        Assert.AreEqual(2, data.FormatMajorVersion);
        Assert.AreEqual(2, data.FormatMinorVersion);
        return data;
    }

    private static void AssertIndependentSections(MstatData data)
    {
        var method = Assert.ContainsSingle(data.Methods);
        Assert.AreEqual("Original1", method.Name);
        Assert.AreEqual("Fixture.Worker", method.DeclaringType);
        Assert.AreEqual(17, method.Size);
        Assert.AreEqual(2, method.GcInfoSize);
        Assert.AreEqual(1, method.EhInfoSize);
        Assert.AreEqual("Method entry node", method.NodeName);

        var type = Assert.ContainsSingle(data.Types);
        Assert.AreEqual("Fixture.Worker", type.Name);
        Assert.AreEqual("Fixture", type.Namespace);
        Assert.AreEqual("FixtureAssembly", type.AssemblyName);
        Assert.AreEqual(24, type.Size);
        Assert.AreEqual("Type entry node", type.NodeName);
    }

    private static long MeasureAllocation(byte[] image, int expectedDeduplicatedMethodCount)
    {
        using var stream = new MemoryStream(image, writable: false);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var data = MstatReader.Read(stream);

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsNotNull(data);
        Assert.HasCount(expectedDeduplicatedMethodCount, data.DeduplicatedMethods);
        return allocated;
    }
}
