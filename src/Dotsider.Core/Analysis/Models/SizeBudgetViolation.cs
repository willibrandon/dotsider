namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One breached limit of a size budget. Every figure is expressed in bytes — for the percent
/// metric the limit is resolved against the baseline so the overage stays a byte count — with
/// the percentages carried alongside where they apply.
/// </summary>
/// <param name="Metric">The limit that was breached.</param>
/// <param name="ActualBytes">The measured value: the current size for <see cref="SizeBudgetMetric.MaxBytes"/>, the growth in bytes for the growth metrics.</param>
/// <param name="LimitBytes">The limit in bytes; for <see cref="SizeBudgetMetric.MaxGrowthPercent"/> this is the baseline times the allowed percentage.</param>
/// <param name="OverageBytes"><see cref="ActualBytes"/> minus <see cref="LimitBytes"/>.</param>
/// <param name="ActualPercent">The measured growth percentage, or null when the baseline was zero (a new scope — any growth breaches).</param>
/// <param name="LimitPercent">The allowed growth percentage, or null for the byte metrics.</param>
public sealed record SizeBudgetViolation(
    SizeBudgetMetric Metric,
    long ActualBytes,
    long LimitBytes,
    long OverageBytes,
    double? ActualPercent,
    double? LimitPercent);
