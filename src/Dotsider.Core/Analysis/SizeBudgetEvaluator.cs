using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Evaluates size budgets against a size diff. Total budgets measure the caller's
/// basis-resolved totals (file size for binaries, mstat total for bare reports); namespace and
/// assembly budgets always measure the diff's mstat aggregates, with namespace targets
/// covering their sub-namespaces. Each breach carries the scope's top positive regressions —
/// the rows that explain the growth.
/// </summary>
public static class SizeBudgetEvaluator
{
    /// <summary>
    /// Evaluates budgets against a diff.
    /// </summary>
    /// <param name="budgets">The budgets to evaluate, reported in this order.</param>
    /// <param name="diff">The size diff between the baseline and the build under check. For a check without a baseline, a diff against <see cref="MstatData.Empty"/>.</param>
    /// <param name="totalBasis">What the total figures count.</param>
    /// <param name="currentTotalBytes">The build's total on <paramref name="totalBasis"/>.</param>
    /// <param name="baselineTotalBytes">The baseline's total on <paramref name="totalBasis"/>, or null when the check runs without one.</param>
    /// <param name="defaultTopN">How many contributors a breach lists when its budget does not pin its own count.</param>
    /// <returns>The report, failing only on error-severity breaches.</returns>
    /// <exception cref="ArgumentException">A total-scope growth budget was supplied without a baseline; callers reject that combination before evaluating.</exception>
    public static SizeBudgetReport Evaluate(
        IReadOnlyList<SizeBudget> budgets,
        MstatDiffResult diff,
        SizeBasis totalBasis,
        long currentTotalBytes,
        long? baselineTotalBytes,
        int defaultTopN = 10)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(diff);

        var evaluations = new List<SizeBudgetEvaluation>(budgets.Count);
        foreach (var budget in budgets)
        {
            if (budget.Scope == SizeBudgetScope.Total
                && baselineTotalBytes is null
                && (budget.MaxGrowthBytes is not null || budget.MaxGrowthPercent is not null))
            {
                throw new ArgumentException(
                    $"Budget '{budget}' limits growth but no baseline was supplied.", nameof(budgets));
            }

            evaluations.Add(EvaluateOne(budget, diff, totalBasis, currentTotalBytes, baselineTotalBytes, defaultTopN));
        }

        return new SizeBudgetReport(
            Passed: evaluations.All(e => e.Passed || e.Budget.Severity == SizeBudgetSeverity.Warning),
            HasWarnings: evaluations.Any(e => !e.Passed && e.Budget.Severity == SizeBudgetSeverity.Warning),
            totalBasis,
            LeftTotal: baselineTotalBytes ?? 0,
            RightTotal: currentTotalBytes,
            LeftMstatTotal: totalBasis == SizeBasis.FileSize ? diff.Summary.LeftTotal : null,
            RightMstatTotal: totalBasis == SizeBasis.FileSize ? diff.Summary.RightTotal : null,
            evaluations);
    }

    private static SizeBudgetEvaluation EvaluateOne(
        SizeBudget budget, MstatDiffResult diff, SizeBasis totalBasis,
        long currentTotalBytes, long? baselineTotalBytes, int defaultTopN)
    {
        var (actual, baseline, basis) = budget.Scope switch
        {
            SizeBudgetScope.Total => (currentTotalBytes, baselineTotalBytes, totalBasis),
            SizeBudgetScope.Namespace => ScopeTotals(diff.NamespaceDeltas, n => MatchesNamespace(n, budget.Target!)),
            _ => ScopeTotals(diff.AssemblyDeltas, n => string.Equals(n, budget.Target, StringComparison.Ordinal)),
        };

        var violations = new List<SizeBudgetViolation>();

        if (budget.MaxBytes is { } maxBytes && actual > maxBytes)
        {
            violations.Add(new SizeBudgetViolation(
                SizeBudgetMetric.MaxBytes, actual, maxBytes, actual - maxBytes, null, null));
        }

        var growth = actual - (baseline ?? 0);

        if (budget.MaxGrowthBytes is { } maxGrowth && growth > maxGrowth)
        {
            violations.Add(new SizeBudgetViolation(
                SizeBudgetMetric.MaxGrowthBytes, growth, maxGrowth, growth - maxGrowth, null, null));
        }

        if (budget.MaxGrowthPercent is { } maxPercent)
        {
            var baselineBytes = baseline ?? 0;
            if (baselineBytes == 0)
            {
                // A brand-new scope has no baseline to grow from; any growth breaches, and no
                // meaningful percentage exists.
                if (growth > 0)
                {
                    violations.Add(new SizeBudgetViolation(
                        SizeBudgetMetric.MaxGrowthPercent, growth, 0, growth, null, maxPercent));
                }
            }
            else
            {
                var limitBytes = (long)(baselineBytes * (maxPercent / 100.0));
                if (growth > limitBytes)
                {
                    violations.Add(new SizeBudgetViolation(
                        SizeBudgetMetric.MaxGrowthPercent, growth, limitBytes, growth - limitBytes,
                        100.0 * growth / baselineBytes, maxPercent));
                }
            }
        }

        var topN = budget.TopN ?? defaultTopN;
        var contributors = diff.Contributors
            .Where(c => c.Delta > 0 && InScope(c, budget))
            .OrderByDescending(c => c.Delta)
            .ThenBy(c => c.FullPath, StringComparer.Ordinal)
            .Take(topN)
            .ToList();

        return new SizeBudgetEvaluation(
            budget, violations.Count == 0, basis, actual,
            budget.Scope == SizeBudgetScope.Total ? baselineTotalBytes : baseline,
            violations, contributors);
    }

    private static (long Actual, long? Baseline, SizeBasis Basis) ScopeTotals(
        IReadOnlyList<SizeDiffAggregate> aggregates, Func<string, bool> matches)
    {
        long actual = 0;
        long baseline = 0;
        foreach (var aggregate in aggregates)
        {
            if (!matches(aggregate.Name)) continue;
            actual += aggregate.RightSize;
            baseline += aggregate.LeftSize;
        }

        return (actual, baseline, SizeBasis.MstatTotal);
    }

    /// <summary>
    /// A namespace target covers itself and its sub-namespaces: <c>System.Text.Json</c> covers
    /// <c>System.Text.Json.Serialization</c> but never <c>System.Text.Json2</c>.
    /// </summary>
    private static bool MatchesNamespace(string ns, string target) =>
        string.Equals(ns, target, StringComparison.Ordinal)
        || (ns.Length > target.Length && ns[target.Length] == '.'
            && ns.StartsWith(target, StringComparison.Ordinal));

    private static bool InScope(SizeDiffContributor contributor, SizeBudget budget) => budget.Scope switch
    {
        SizeBudgetScope.Namespace => MatchesNamespace(contributor.Namespace, budget.Target!),
        SizeBudgetScope.Assembly => string.Equals(contributor.AssemblyName, budget.Target, StringComparison.Ordinal),
        _ => true,
    };
}
