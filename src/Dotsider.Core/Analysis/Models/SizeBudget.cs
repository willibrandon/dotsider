namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One size budget: a scope plus at least one limit. Parsed from the spec grammar
/// (<c>[scope:]limit(,limit)*</c> — for example <c>total:max=25mb,growth=1%</c> or
/// <c>ns=System.Text.Json:growth=10kb</c>) by
/// <see cref="Dotsider.Core.Analysis.SizeBudgetParser"/>, or from a budget file's object form
/// which can also carry a name, description, severity, and per-budget contributor count.
/// </summary>
/// <param name="Scope">What the budget measures.</param>
/// <param name="Target">The namespace prefix or assembly simple name, or null for <see cref="SizeBudgetScope.Total"/>.</param>
/// <param name="MaxBytes">The absolute cap on the current value in bytes, or null when not limited.</param>
/// <param name="MaxGrowthBytes">The cap on growth versus the baseline in bytes, or null when not limited.</param>
/// <param name="MaxGrowthPercent">The cap on growth versus the baseline as a percentage, or null when not limited.</param>
/// <param name="Severity">Whether a breach fails the check or only warns.</param>
/// <param name="Name">A stable display name for reports, or null to render the spec itself.</param>
/// <param name="Description">An explanation shown alongside a breach, or null.</param>
/// <param name="TopN">A per-budget override for how many contributors a breach lists, or null for the caller's default.</param>
public sealed record SizeBudget(
    SizeBudgetScope Scope,
    string? Target,
    long? MaxBytes,
    long? MaxGrowthBytes,
    double? MaxGrowthPercent,
    SizeBudgetSeverity Severity = SizeBudgetSeverity.Error,
    string? Name = null,
    string? Description = null,
    int? TopN = null)
{
    /// <summary>Renders the budget back into spec-grammar form, for display when it has no <see cref="Name"/>.</summary>
    /// <returns>The spec string.</returns>
    public override string ToString()
    {
        var scope = Scope switch
        {
            SizeBudgetScope.Namespace => $"ns={Target}",
            SizeBudgetScope.Assembly => $"asm={Target}",
            _ => "total",
        };
        var limits = new List<string>(3);
        if (MaxBytes is { } max) limits.Add($"max={max}");
        if (MaxGrowthBytes is { } growth) limits.Add($"growth={growth}");
        if (MaxGrowthPercent is { } pct) limits.Add($"growth={pct}%");
        return $"{scope}:{string.Join(",", limits)}";
    }
}
