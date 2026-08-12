using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="SizeBudgetEvaluator"/> against the real V1/V2 size diff. Expected
/// values are recomputed from the same diff the evaluator sees — never golden byte counts.
/// </summary>
[TestClass]
public class SizeBudgetEvaluatorTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private static MstatDiffResult DiffV1V2()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        TestSkip.When(Samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v1 = MstatReader.Read(Samples.NativeAotConsoleMstat!);
        var v2 = MstatReader.Read(Samples.NativeAotConsoleV2Mstat!);
        Assert.IsNotNull(v1);
        Assert.IsNotNull(v2);
        return MstatDiffer.Compare(v1, v2);
    }

    private static SizeBudgetReport Evaluate(
        MstatDiffResult diff, params string[] specs) =>
        SizeBudgetEvaluator.Evaluate(
            [.. specs.Select(SizeBudgetParser.Parse)], diff,
            SizeBasis.MstatTotal, diff.Summary.RightTotal, diff.Summary.LeftTotal);

    /// <summary>
    /// Verifies a zero-growth budget on the namespace added in V2 fails, and the report
    /// fails with it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_TelemetryZeroGrowthBudget_Fails()
    {
        var diff = DiffV1V2();

        var report = Evaluate(diff, "ns=NativeAotConsole.Telemetry:growth=0");

        Assert.IsFalse(report.Passed);
        var evaluation = Assert.ContainsSingle(report.Evaluations);
        Assert.IsFalse(evaluation.Passed);
        var violation = Assert.ContainsSingle(evaluation.Violations);
        Assert.AreEqual(SizeBudgetMetric.MaxGrowthBytes, violation.Metric);
        Assert.IsGreaterThan(0, violation.OverageBytes);
        Assert.AreEqual(SizeBasis.MstatTotal, evaluation.Basis);
    }

    /// <summary>Verifies a generous total percentage budget passes on the real growth.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_GenerousTotalPercent_Passes()
    {
        var diff = DiffV1V2();

        var report = Evaluate(diff, "total:growth=1000%");

        Assert.IsTrue(report.Passed);
        Assert.IsTrue(Assert.ContainsSingle(report.Evaluations).Passed);
    }

    /// <summary>Verifies a self-diff passes a zero-growth total budget.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_SelfDiffZeroGrowth_Passes()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        var v1 = MstatReader.Read(Samples.NativeAotConsoleMstat!);
        Assert.IsNotNull(v1);
        var diff = MstatDiffer.Compare(v1, v1);

        var report = Evaluate(diff, "total:growth=0");

        Assert.IsTrue(report.Passed);
    }

    /// <summary>
    /// Verifies the percentage math is computed against the baseline: the expected violation
    /// figures are recomputed here from the same totals.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_PercentComputedAgainstBaseline()
    {
        var diff = DiffV1V2();
        var growth = diff.Summary.RightTotal - diff.Summary.LeftTotal;
        TestSkip.When(growth <= 0, "V2 did not grow relative to V1");

        var report = Evaluate(diff, "total:growth=0.001%");

        var violation = Assert.ContainsSingle(Assert.ContainsSingle(report.Evaluations).Violations);
        Assert.AreEqual(SizeBudgetMetric.MaxGrowthPercent, violation.Metric);
        Assert.AreEqual(growth, violation.ActualBytes);
        Assert.AreEqual((long)(diff.Summary.LeftTotal * 0.00001), violation.LimitBytes);
        Assert.IsNotNull(violation.ActualPercent);
        Assert.AreEqual(100.0 * growth / diff.Summary.LeftTotal, violation.ActualPercent!.Value, 3);
    }

    /// <summary>
    /// Verifies namespace prefix semantics: the parent namespace's budget covers the fixture's
    /// added child namespace, while a lookalike sibling prefix covers nothing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_NamespacePrefixCoversChildNotSibling()
    {
        var diff = DiffV1V2();

        var report = Evaluate(
            diff,
            "ns=NativeAotConsole:growth=0",           // covers NativeAotConsole.Telemetry
            "ns=NativeAotConsole.Tele:growth=0");     // a prefix, not a namespace — covers nothing

        Assert.IsFalse(report.Evaluations[0].Passed);
        Assert.IsGreaterThan(0, report.Evaluations[0].ActualBytes);
        Assert.IsTrue(report.Evaluations[1].Passed);
        Assert.AreEqual(0, report.Evaluations[1].ActualBytes);
    }

    /// <summary>
    /// Verifies the assembly scope measures the app assembly's aggregate, recomputed from the
    /// same diff.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_AssemblyScopeMeasuresAggregate()
    {
        var diff = DiffV1V2();
        var expected = diff.AssemblyDeltas.Single(a => a.Name == "NativeAotConsole");

        var report = Evaluate(diff, "asm=NativeAotConsole:growth=0");

        var evaluation = Assert.ContainsSingle(report.Evaluations);
        Assert.AreEqual(expected.RightSize, evaluation.ActualBytes);
        Assert.AreEqual(expected.LeftSize, evaluation.BaselineBytes);
        Assert.IsFalse(evaluation.Passed);
    }

    /// <summary>
    /// Verifies a breach's contributors are positive regressions only, inside the budget's
    /// scope, ordered by delta — improvements never crowd out the rows explaining growth.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_ViolationCarriesOrderedScopedContributors()
    {
        var diff = DiffV1V2();

        var report = Evaluate(diff, "ns=NativeAotConsole.Telemetry:growth=0");

        var contributors = Assert.ContainsSingle(report.Evaluations).TopContributors;
        Assert.IsNotEmpty(contributors);
        TestAssert.All(contributors, c =>
        {
            Assert.IsGreaterThan(0, c.Delta);
            Assert.StartsWith("NativeAotConsole.Telemetry", c.Namespace, StringComparison.Ordinal);
        });
        for (var i = 1; i < contributors.Count; i++)
            Assert.IsGreaterThanOrEqualTo(contributors[i].Delta, contributors[i - 1].Delta);
    }

    /// <summary>
    /// Verifies an absolute budget evaluates without a baseline against the empty report:
    /// everything is added and the actual equals the build's total.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_MaxBudgetWithoutBaseline_UsesEmptyLeft()
    {
        TestSkip.When(Samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v2 = MstatReader.Read(Samples.NativeAotConsoleV2Mstat!);
        Assert.IsNotNull(v2);
        var diff = MstatDiffer.Compare(MstatData.Empty, v2);

        var report = SizeBudgetEvaluator.Evaluate(
            [SizeBudgetParser.Parse("max=1b")], diff,
            SizeBasis.MstatTotal, diff.Summary.RightTotal, baselineTotalBytes: null);

        Assert.IsFalse(report.Passed);
        var violation = Assert.ContainsSingle(Assert.ContainsSingle(report.Evaluations).Violations);
        Assert.AreEqual(SizeBudgetMetric.MaxBytes, violation.Metric);
        Assert.AreEqual(diff.Summary.RightTotal, violation.ActualBytes);
    }

    /// <summary>
    /// Verifies a total-scope growth budget without a baseline is rejected loudly — callers
    /// must reject it upstream, and the evaluator refuses to guess.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_GrowthWithoutBaseline_Throws()
    {
        TestSkip.When(Samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v2 = MstatReader.Read(Samples.NativeAotConsoleV2Mstat!);
        Assert.IsNotNull(v2);
        var diff = MstatDiffer.Compare(MstatData.Empty, v2);

        Assert.ThrowsExactly<ArgumentException>(() => SizeBudgetEvaluator.Evaluate(
            [SizeBudgetParser.Parse("total:growth=1%")], diff,
            SizeBasis.MstatTotal, diff.Summary.RightTotal, baselineTotalBytes: null));
    }

    /// <summary>
    /// Verifies a confirmed first run defers every growth metric while still enforcing an
    /// absolute limit in the same evaluation pass.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_FirstRun_DefersGrowthAndEnforcesAbsoluteLimits()
    {
        TestSkip.When(Samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v2 = MstatReader.Read(Samples.NativeAotConsoleV2Mstat!);
        Assert.IsNotNull(v2);
        var diff = MstatDiffer.Compare(MstatData.Empty, v2);

        var report = SizeBudgetEvaluator.Evaluate(
            [
                SizeBudgetParser.Parse("max=1b"),
                SizeBudgetParser.Parse("ns=NativeAotConsole:growth=1b"),
                SizeBudgetParser.Parse("asm=NativeAotConsole:growth=1%"),
            ],
            diff,
            SizeBasis.MstatTotal,
            diff.Summary.RightTotal,
            baselineTotalBytes: null,
            defaultTopN: 10,
            deferGrowthWithoutBaseline: true);

        Assert.IsFalse(report.Passed, "The absolute max budget must still fail.");
        Assert.IsTrue(report.HasDeferred);
        Assert.HasCount(1, report.Evaluations[0].Violations);
        Assert.AreSequenceEqual<SizeBudgetMetric>(
            [SizeBudgetMetric.MaxGrowthBytes],
            report.Evaluations[1].DeferredMetrics);
        Assert.AreSequenceEqual<SizeBudgetMetric>(
            [SizeBudgetMetric.MaxGrowthPercent],
            report.Evaluations[2].DeferredMetrics);
        Assert.IsNull(report.Evaluations[1].BaselineBytes);
        Assert.IsNull(report.Evaluations[2].BaselineBytes);
    }

    /// <summary>
    /// Verifies strict evaluation rejects scoped growth without a baseline.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_ScopedGrowthWithoutBaseline_Throws()
    {
        TestSkip.When(Samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v2 = MstatReader.Read(Samples.NativeAotConsoleV2Mstat!);
        Assert.IsNotNull(v2);
        var diff = MstatDiffer.Compare(MstatData.Empty, v2);

        Assert.ThrowsExactly<ArgumentException>(() => SizeBudgetEvaluator.Evaluate(
            [SizeBudgetParser.Parse("asm=NativeAotConsole:growth=1b")], diff,
            SizeBasis.MstatTotal, diff.Summary.RightTotal, baselineTotalBytes: null));
    }

    /// <summary>
    /// Verifies warning severity reports the breach without failing the check, and the
    /// warning surfaces through <see cref="SizeBudgetReport.HasWarnings"/>.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_WarningSeverity_ReportsWithoutFailing()
    {
        var diff = DiffV1V2();
        var warning = SizeBudgetParser.Parse("ns=NativeAotConsole.Telemetry:growth=0")
            with
        { Severity = SizeBudgetSeverity.Warning };

        var report = SizeBudgetEvaluator.Evaluate(
            [warning], diff, SizeBasis.MstatTotal,
            diff.Summary.RightTotal, diff.Summary.LeftTotal);

        Assert.IsTrue(report.Passed);
        Assert.IsTrue(report.HasWarnings);
        Assert.IsFalse(Assert.ContainsSingle(report.Evaluations).Passed);
    }

    /// <summary>
    /// Verifies the report surfaces the basis and both bases' totals when the check ran on
    /// file sizes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Evaluate_FileSizeBasis_SurfacesBothBases()
    {
        var diff = DiffV1V2();

        var report = SizeBudgetEvaluator.Evaluate(
            [SizeBudgetParser.Parse("max=10gb")], diff,
            SizeBasis.FileSize, currentTotalBytes: 123_456, baselineTotalBytes: 100_000);

        Assert.AreEqual(SizeBasis.FileSize, report.TotalBasis);
        Assert.AreEqual(123_456, report.RightTotal);
        Assert.AreEqual(100_000, report.LeftTotal);
        Assert.AreEqual(diff.Summary.LeftTotal, report.LeftMstatTotal);
        Assert.AreEqual(diff.Summary.RightTotal, report.RightMstatTotal);
        Assert.AreEqual(SizeBasis.FileSize, Assert.ContainsSingle(report.Evaluations).Basis);
    }
}
