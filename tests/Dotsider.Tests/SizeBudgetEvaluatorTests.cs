using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="SizeBudgetEvaluator"/> against the real V1/V2 size diff. Expected
/// values are recomputed from the same diff the evaluator sees — never golden byte counts.
/// </summary>
[Collection("SampleAssemblies")]
public class SizeBudgetEvaluatorTests(SampleAssemblyFixture samples)
{
    private MstatDiffResult DiffV1V2()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        Assert.SkipWhen(samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v1 = MstatReader.Read(samples.NativeAotConsoleMstat!);
        var v2 = MstatReader.Read(samples.NativeAotConsoleV2Mstat!);
        Assert.NotNull(v1);
        Assert.NotNull(v2);
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
    [Fact(Timeout = 30_000)]
    public void Evaluate_TelemetryZeroGrowthBudget_Fails()
    {
        var diff = DiffV1V2();

        var report = Evaluate(diff, "ns=NativeAotConsole.Telemetry:growth=0");

        Assert.False(report.Passed);
        var evaluation = Assert.Single(report.Evaluations);
        Assert.False(evaluation.Passed);
        var violation = Assert.Single(evaluation.Violations);
        Assert.Equal(SizeBudgetMetric.MaxGrowthBytes, violation.Metric);
        Assert.True(violation.OverageBytes > 0);
        Assert.Equal(SizeBasis.MstatTotal, evaluation.Basis);
    }

    /// <summary>Verifies a generous total percentage budget passes on the real growth.</summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_GenerousTotalPercent_Passes()
    {
        var diff = DiffV1V2();

        var report = Evaluate(diff, "total:growth=1000%");

        Assert.True(report.Passed);
        Assert.True(Assert.Single(report.Evaluations).Passed);
    }

    /// <summary>Verifies a self-diff passes a zero-growth total budget.</summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_SelfDiffZeroGrowth_Passes()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        var v1 = MstatReader.Read(samples.NativeAotConsoleMstat!);
        Assert.NotNull(v1);
        var diff = MstatDiffer.Compare(v1, v1);

        var report = Evaluate(diff, "total:growth=0");

        Assert.True(report.Passed);
    }

    /// <summary>
    /// Verifies the percentage math is computed against the baseline: the expected violation
    /// figures are recomputed here from the same totals.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_PercentComputedAgainstBaseline()
    {
        var diff = DiffV1V2();
        var growth = diff.Summary.RightTotal - diff.Summary.LeftTotal;
        Assert.SkipWhen(growth <= 0, "V2 did not grow relative to V1");

        var report = Evaluate(diff, "total:growth=0.001%");

        var violation = Assert.Single(Assert.Single(report.Evaluations).Violations);
        Assert.Equal(SizeBudgetMetric.MaxGrowthPercent, violation.Metric);
        Assert.Equal(growth, violation.ActualBytes);
        Assert.Equal((long)(diff.Summary.LeftTotal * 0.00001), violation.LimitBytes);
        Assert.NotNull(violation.ActualPercent);
        Assert.Equal(100.0 * growth / diff.Summary.LeftTotal, violation.ActualPercent!.Value, 3);
    }

    /// <summary>
    /// Verifies namespace prefix semantics: the parent namespace's budget covers the fixture's
    /// added child namespace, while a lookalike sibling prefix covers nothing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_NamespacePrefixCoversChildNotSibling()
    {
        var diff = DiffV1V2();

        var report = Evaluate(
            diff,
            "ns=NativeAotConsole:growth=0",           // covers NativeAotConsole.Telemetry
            "ns=NativeAotConsole.Tele:growth=0");     // a prefix, not a namespace — covers nothing

        Assert.False(report.Evaluations[0].Passed);
        Assert.True(report.Evaluations[0].ActualBytes > 0);
        Assert.True(report.Evaluations[1].Passed);
        Assert.Equal(0, report.Evaluations[1].ActualBytes);
    }

    /// <summary>
    /// Verifies the assembly scope measures the app assembly's aggregate, recomputed from the
    /// same diff.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_AssemblyScopeMeasuresAggregate()
    {
        var diff = DiffV1V2();
        var expected = diff.AssemblyDeltas.Single(a => a.Name == "NativeAotConsole");

        var report = Evaluate(diff, "asm=NativeAotConsole:growth=0");

        var evaluation = Assert.Single(report.Evaluations);
        Assert.Equal(expected.RightSize, evaluation.ActualBytes);
        Assert.Equal(expected.LeftSize, evaluation.BaselineBytes);
        Assert.False(evaluation.Passed);
    }

    /// <summary>
    /// Verifies a breach's contributors are positive regressions only, inside the budget's
    /// scope, ordered by delta — improvements never crowd out the rows explaining growth.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_ViolationCarriesOrderedScopedContributors()
    {
        var diff = DiffV1V2();

        var report = Evaluate(diff, "ns=NativeAotConsole.Telemetry:growth=0");

        var contributors = Assert.Single(report.Evaluations).TopContributors;
        Assert.NotEmpty(contributors);
        Assert.All(contributors, c =>
        {
            Assert.True(c.Delta > 0);
            Assert.StartsWith("NativeAotConsole.Telemetry", c.Namespace, StringComparison.Ordinal);
        });
        for (var i = 1; i < contributors.Count; i++)
            Assert.True(contributors[i - 1].Delta >= contributors[i].Delta);
    }

    /// <summary>
    /// Verifies an absolute budget evaluates without a baseline against the empty report:
    /// everything is added and the actual equals the build's total.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_MaxBudgetWithoutBaseline_UsesEmptyLeft()
    {
        Assert.SkipWhen(samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v2 = MstatReader.Read(samples.NativeAotConsoleV2Mstat!);
        Assert.NotNull(v2);
        var diff = MstatDiffer.Compare(MstatData.Empty, v2);

        var report = SizeBudgetEvaluator.Evaluate(
            [SizeBudgetParser.Parse("max=1b")], diff,
            SizeBasis.MstatTotal, diff.Summary.RightTotal, baselineTotalBytes: null);

        Assert.False(report.Passed);
        var violation = Assert.Single(Assert.Single(report.Evaluations).Violations);
        Assert.Equal(SizeBudgetMetric.MaxBytes, violation.Metric);
        Assert.Equal(diff.Summary.RightTotal, violation.ActualBytes);
    }

    /// <summary>
    /// Verifies a total-scope growth budget without a baseline is rejected loudly — callers
    /// must reject it upstream, and the evaluator refuses to guess.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_GrowthWithoutBaseline_Throws()
    {
        Assert.SkipWhen(samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v2 = MstatReader.Read(samples.NativeAotConsoleV2Mstat!);
        Assert.NotNull(v2);
        var diff = MstatDiffer.Compare(MstatData.Empty, v2);

        Assert.Throws<ArgumentException>(() => SizeBudgetEvaluator.Evaluate(
            [SizeBudgetParser.Parse("total:growth=1%")], diff,
            SizeBasis.MstatTotal, diff.Summary.RightTotal, baselineTotalBytes: null));
    }

    /// <summary>
    /// Verifies warning severity reports the breach without failing the check, and the
    /// warning surfaces through <see cref="SizeBudgetReport.HasWarnings"/>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_WarningSeverity_ReportsWithoutFailing()
    {
        var diff = DiffV1V2();
        var warning = SizeBudgetParser.Parse("ns=NativeAotConsole.Telemetry:growth=0")
            with { Severity = SizeBudgetSeverity.Warning };

        var report = SizeBudgetEvaluator.Evaluate(
            [warning], diff, SizeBasis.MstatTotal,
            diff.Summary.RightTotal, diff.Summary.LeftTotal);

        Assert.True(report.Passed);
        Assert.True(report.HasWarnings);
        Assert.False(Assert.Single(report.Evaluations).Passed);
    }

    /// <summary>
    /// Verifies the report surfaces the basis and both bases' totals when the check ran on
    /// file sizes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Evaluate_FileSizeBasis_SurfacesBothBases()
    {
        var diff = DiffV1V2();

        var report = SizeBudgetEvaluator.Evaluate(
            [SizeBudgetParser.Parse("max=10gb")], diff,
            SizeBasis.FileSize, currentTotalBytes: 123_456, baselineTotalBytes: 100_000);

        Assert.Equal(SizeBasis.FileSize, report.TotalBasis);
        Assert.Equal(123_456, report.RightTotal);
        Assert.Equal(100_000, report.LeftTotal);
        Assert.Equal(diff.Summary.LeftTotal, report.LeftMstatTotal);
        Assert.Equal(diff.Summary.RightTotal, report.RightMstatTotal);
        Assert.Equal(SizeBasis.FileSize, Assert.Single(report.Evaluations).Basis);
    }
}
