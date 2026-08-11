using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using System.Text;

namespace Dotsider.Infrastructure;

/// <summary>
/// Renders a size-diff report in the three headless formats — text for a terminal, markdown
/// for a CI step summary, and the JSON document — from one input, so <c>diff --json</c> and
/// <c>size-check</c> can never print different numbers for the same comparison.
/// </summary>
internal static class SizeDiffReportWriter
{
    /// <summary>Everything one report needs, basis-resolved by the command that built it.</summary>
    internal sealed record Context(
        string TargetPath,
        string? BaselinePath,
        MstatDiffResult Diff,
        SizeBasis TotalBasis,
        long RightTotal,
        long? LeftTotal,
        int Top,
        IReadOnlyDictionary<string, IReadOnlyList<DgmlPathStep>>? WhyPaths,
        SizeBudgetReport? Budgets);

    private const int AggregateRows = 15;
    private const int MarkdownContributorLabelLimit = 140;

    /// <summary>Builds the JSON document for the report.</summary>
    internal static CliSizeReportPayload BuildDocument(Context ctx)
    {
        var contributors = ctx.Diff.Contributors
            .Take(ctx.Top)
            .Select(c => new CliSizeContributorPayload(
                c.Name,
                c.FullPath,
                c.Kind,
                c.Diff,
                c.LeftSize,
                c.RightSize,
                c.Delta,
                c.AssemblyName,
                c.Namespace,
                c.LeftEntryCount,
                c.RightEntryCount,
                c.LeftNodeNames,
                c.RightNodeNames,
                ctx.WhyPaths?.GetValueOrDefault(c.FullPath)))
            .ToList();

        return new CliSizeReportPayload(
            1,
            ctx.TargetPath,
            ctx.BaselinePath,
            ctx.TotalBasis,
            ctx.LeftTotal,
            ctx.RightTotal,
            ctx.TotalBasis == SizeBasis.FileSize ? ctx.Diff.Summary.LeftTotal : null,
            ctx.TotalBasis == SizeBasis.FileSize ? ctx.Diff.Summary.RightTotal : null,
            ctx.Diff.LeftFormatVersion,
            ctx.Diff.RightFormatVersion,
            ctx.Diff.Summary,
            ctx.Diff.AssemblyDeltas,
            ctx.Diff.NamespaceDeltas,
            contributors,
            ctx.Budgets);
    }

    /// <summary>Writes the human-readable report through the formatter.</summary>
    internal static void WriteText(OutputFormatter fmt, Context ctx)
    {
        fmt.WriteLine($"Size check: {ctx.TargetPath}");
        if (ctx.BaselinePath is not null)
            fmt.WriteLine($"Baseline:   {ctx.BaselinePath}");
        fmt.WriteLine($"Basis:      {BasisName(ctx.TotalBasis)}");
        fmt.WriteLine($"Total:      {FormatRange(ctx.LeftTotal, ctx.RightTotal)}");
        if (ctx.TotalBasis == SizeBasis.FileSize)
        {
            fmt.WriteLine("Mstat:      "
                + FormatRange(ctx.BaselinePath is null ? null : ctx.Diff.Summary.LeftTotal,
                    ctx.Diff.Summary.RightTotal));
        }

        fmt.WriteLine($"Formats:    {ctx.Diff.LeftFormatVersion} -> {ctx.Diff.RightFormatVersion}");

        fmt.WriteLine("");
        fmt.WriteTable(
            ["Kind", "Added", "Removed", "Grown", "Shrunk", "Unchanged"],
            ctx.Diff.Summary.Counts.Select(c => new[]
            {
                c.Kind.ToString(),
                c.Added.ToString(),
                c.Removed.ToString(),
                c.Grown.ToString(),
                c.Shrunk.ToString(),
                c.Unchanged.ToString(),
            }));

        WriteAggregates(fmt, "Assemblies", ctx.Diff.AssemblyDeltas);
        WriteAggregates(fmt, "Namespaces", ctx.Diff.NamespaceDeltas);

        WriteContributors(fmt, ctx, "Regressions",
            ctx.Diff.Contributors.Where(c => c.Delta > 0).Take(ctx.Top));
        WriteContributors(fmt, ctx, "Improvements",
            ctx.Diff.Contributors.Where(c => c.Delta < 0).Take(ctx.Top));

        if (ctx.Budgets is { } budgets)
        {
            fmt.WriteLine("");
            fmt.WriteLine("Budgets:");
            foreach (var evaluation in budgets.Evaluations)
                WriteEvaluation(fmt, evaluation);

            fmt.WriteLine("");
            fmt.WriteLine(budgets.Passed
                ? budgets.HasWarnings ? "Result: PASS (with warnings)" : "Result: PASS"
                : "Result: FAIL (a size budget was exceeded)");
        }
    }

    private static void WriteAggregates(
        OutputFormatter fmt, string title, IReadOnlyList<SizeDiffAggregate> aggregates)
    {
        var rows = aggregates.Where(a => a.Delta != 0).Take(AggregateRows).ToList();
        if (rows.Count == 0) return;

        fmt.WriteLine("");
        fmt.WriteLine($"{title} (by |Δ|):");
        fmt.WriteTable(
            ["Name", "Baseline", "Current", "Δ"],
            rows.Select(a => new[]
            {
                a.Name.Length > 0 ? a.Name : "(global)",
                DotsiderState.FormatSize(a.LeftSize),
                DotsiderState.FormatSize(a.RightSize),
                SizeDiffTreemapView.FormatDelta(a.Delta),
            }));
    }

    private static void WriteContributors(
        OutputFormatter fmt, Context ctx, string title, IEnumerable<SizeDiffContributor> contributors)
    {
        var rows = contributors.ToList();
        if (rows.Count == 0) return;

        fmt.WriteLine("");
        fmt.WriteLine($"{title} (top {ctx.Top}):");
        fmt.WriteTable(
            ["Δ", "Kind", "Change", "Name"],
            rows.Select(c => new[]
            {
                SizeDiffTreemapView.FormatDelta(c.Delta),
                c.Kind.ToString(),
                DirectionName(c),
                ContributorLabel(c),
            }));

        if (ctx.WhyPaths is { } whyPaths)
        {
            foreach (var c in rows)
            {
                if (whyPaths.GetValueOrDefault(c.FullPath) is not { Count: > 0 } path) continue;
                fmt.WriteLine($"  why {c.Name} (root first):");
                for (var i = 0; i < path.Count; i++)
                {
                    fmt.WriteLine($"    {i + 1,3}. {path[i].Label}"
                        + (path[i].Reason is { } reason ? $"  ({reason})" : ""));
                }
            }
        }
    }

    private static void WriteEvaluation(OutputFormatter fmt, SizeBudgetEvaluation evaluation)
    {
        var verdict = evaluation.Passed ? "PASS"
            : evaluation.Budget.Severity == SizeBudgetSeverity.Warning ? "WARN" : "FAIL";
        var label = evaluation.Budget.Name ?? evaluation.Budget.ToString();
        var baseline = evaluation.BaselineBytes is { } b
            ? $"{DotsiderState.FormatSize(b)} -> " : "";
        fmt.WriteLine($"  {verdict}  {label} — {baseline}{DotsiderState.FormatSize(evaluation.ActualBytes)}"
            + $" ({BasisName(evaluation.Basis)})");
        if (evaluation.Budget.Description is { } description)
            fmt.WriteLine($"        {description}");

        foreach (var violation in evaluation.Violations)
        {
            fmt.WriteLine($"        {MetricName(violation.Metric)}: "
                + $"{DotsiderState.FormatSize(violation.ActualBytes)} over a limit of "
                + $"{DotsiderState.FormatSize(violation.LimitBytes)} — exceeded by "
                + $"{DotsiderState.FormatSize(violation.OverageBytes)}"
                + (violation.ActualPercent is { } actual && violation.LimitPercent is { } limit
                    ? $" ({actual:F1}% vs {limit:F1}%)"
                    : violation.LimitPercent is { } limitOnly
                        ? $" (new scope; any growth exceeds {limitOnly:F1}%)"
                        : ""));
        }

        if (!evaluation.Passed && evaluation.TopContributors.Count > 0)
        {
            fmt.WriteLine("        Top contributors:");
            foreach (var c in evaluation.TopContributors)
            {
                fmt.WriteLine($"          {SizeDiffTreemapView.FormatDelta(c.Delta),12}  "
                    + $"{DirectionName(c),-8} {ContributorLabel(c)}");
            }
        }
    }

    /// <summary>Builds the markdown report — the shape a GitHub step summary renders.</summary>
    internal static string BuildMarkdown(Context ctx)
    {
        var sb = new StringBuilder();
        var snapshot = ctx.BaselinePath is null;
        sb.AppendLine("## Size check");

        if (ctx.Budgets is { } statusBudgets)
        {
            sb.AppendLine();
            sb.AppendLine(statusBudgets.Passed
                ? statusBudgets.HasWarnings
                    ? "> ⚠️ **PASS with warnings** — all error-severity size budgets passed."
                    : "> ✅ **PASS** — all size budgets passed."
                : "> ❌ **FAIL** — a size budget was exceeded.");
        }

        if (snapshot)
        {
            sb.AppendLine();
            sb.AppendLine("> ℹ️ **Snapshot** — no baseline was supplied.");
        }

        sb.AppendLine();
        sb.AppendLine("### Overview");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine($"| Target | {MarkdownCodeSpan(ctx.TargetPath)} |");
        if (ctx.BaselinePath is not null)
            sb.AppendLine($"| Baseline | {MarkdownCodeSpan(ctx.BaselinePath)} |");
        sb.AppendLine($"| Basis | {BasisName(ctx.TotalBasis)} |");
        sb.AppendLine($"| Total | {MarkdownRange(ctx.LeftTotal, ctx.RightTotal)} |");
        if (ctx.TotalBasis == SizeBasis.FileSize)
        {
            sb.AppendLine("| Mstat | "
                + MarkdownRange(snapshot ? null : ctx.Diff.Summary.LeftTotal,
                    ctx.Diff.Summary.RightTotal) + " |");
        }

        if (ctx.Budgets is { } budgets)
            AppendBudgetTable(sb, budgets, snapshot);

        sb.AppendLine();
        if (snapshot)
        {
            sb.AppendLine("### Contents");
            sb.AppendLine();
            sb.AppendLine("| Kind | Count |");
            sb.AppendLine("| --- | ---: |");
            foreach (var c in ctx.Diff.Summary.Counts)
                sb.AppendLine($"| {c.Kind} | {c.Added} |");

            AppendAggregateTable(sb, "Assemblies", ctx.Diff.AssemblyDeltas, snapshot: true);
            AppendAggregateTable(sb, "Namespaces", ctx.Diff.NamespaceDeltas, snapshot: true);
            var contributors = ctx.Diff.Contributors.Where(c => c.RightSize > 0).Take(ctx.Top).ToList();
            AppendContributorTable(sb, $"Largest contributors (top {ctx.Top})", contributors, snapshot: true);
            AppendWhyChains(sb, ctx, contributors);
        }
        else
        {
            sb.AppendLine("### Changes");
            sb.AppendLine();
            sb.AppendLine("| Kind | Added | Removed | Grown | Shrunk | Unchanged |");
            sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
            foreach (var c in ctx.Diff.Summary.Counts)
                sb.AppendLine($"| {c.Kind} | {c.Added} | {c.Removed} | {c.Grown} | {c.Shrunk} | {c.Unchanged} |");

            AppendAggregateTable(sb, "Assemblies", ctx.Diff.AssemblyDeltas, snapshot: false);
            AppendAggregateTable(sb, "Namespaces", ctx.Diff.NamespaceDeltas, snapshot: false);

            var regressions = ctx.Diff.Contributors.Where(c => c.Delta > 0).Take(ctx.Top).ToList();
            AppendContributorTable(sb, $"Regressions (top {ctx.Top})", regressions, snapshot: false);
            AppendWhyChains(sb, ctx, regressions);
            AppendContributorTable(sb, $"Improvements (top {ctx.Top})",
                ctx.Diff.Contributors.Where(c => c.Delta < 0).Take(ctx.Top), snapshot: false);
        }

        return sb.ToString();
    }

    private static void AppendBudgetTable(StringBuilder sb, SizeBudgetReport budgets, bool snapshot)
    {
        sb.AppendLine();
        sb.AppendLine("### Budgets");
        sb.AppendLine();
        sb.AppendLine(snapshot
            ? "| Status | Budget | Current | Basis |"
            : "| Status | Budget | Baseline → current | Basis |");
        sb.AppendLine("| --- | --- | ---: | --- |");
        foreach (var evaluation in budgets.Evaluations)
        {
            var verdict = evaluation.Passed ? "✅ PASS"
                : evaluation.Budget.Severity == SizeBudgetSeverity.Warning ? "⚠️ WARN" : "❌ FAIL";
            var value = evaluation.BaselineBytes is { } b
                ? $"{MarkdownSize(DotsiderState.FormatSize(b))} → "
                    + MarkdownSize(DotsiderState.FormatSize(evaluation.ActualBytes))
                : MarkdownSize(DotsiderState.FormatSize(evaluation.ActualBytes));
            sb.AppendLine($"| {verdict} | {MarkdownCodeSpan(BudgetLabel(evaluation.Budget))} | "
                + $"{value} | {BasisName(evaluation.Basis)} |");
        }

        foreach (var evaluation in budgets.Evaluations.Where(e => !e.Passed))
        {
            sb.AppendLine();
            sb.AppendLine($"#### {MarkdownCodeSpan(BudgetLabel(evaluation.Budget))}");
            if (evaluation.Budget.Description is { } description)
                sb.AppendLine($"{description}\n");
            foreach (var violation in evaluation.Violations)
            {
                sb.AppendLine($"- {MetricName(violation.Metric)}: "
                    + $"{DotsiderState.FormatSize(violation.ActualBytes)} over a limit of "
                    + $"{DotsiderState.FormatSize(violation.LimitBytes)} — exceeded by "
                    + $"**{DotsiderState.FormatSize(violation.OverageBytes)}**");
            }

            AppendContributorTable(sb, "Top contributors", evaluation.TopContributors, snapshot);
        }
    }

    private static void AppendAggregateTable(
        StringBuilder sb, string title, IReadOnlyList<SizeDiffAggregate> aggregates, bool snapshot)
    {
        var rows = aggregates.Where(a => snapshot ? a.RightSize > 0 : a.Delta != 0)
            .Take(AggregateRows)
            .ToList();
        if (rows.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine($"### {title}");
        sb.AppendLine();
        sb.AppendLine(snapshot ? "| Name | Size |" : "| Name | Baseline | Current | Δ |");
        sb.AppendLine(snapshot ? "| --- | ---: |" : "| --- | ---: | ---: | ---: |");
        foreach (var a in rows)
        {
            var name = MarkdownCodeSpan(a.Name.Length > 0 ? a.Name : "(global)");
            if (snapshot)
            {
                sb.AppendLine($"| {name} | {MarkdownSize(DotsiderState.FormatSize(a.RightSize))} |");
            }
            else
            {
                sb.AppendLine($"| {name} | {MarkdownSize(DotsiderState.FormatSize(a.LeftSize))} | "
                    + $"{MarkdownSize(DotsiderState.FormatSize(a.RightSize))} | "
                    + $"{MarkdownSize(SizeDiffTreemapView.FormatDelta(a.Delta))} |");
            }
        }
    }

    /// <summary>
    /// Renders the resolved "why did this appear" chains for the given rows — the markdown
    /// counterpart of the chains the text report prints, so a CI step summary keeps them.
    /// </summary>
    private static void AppendWhyChains(
        StringBuilder sb, Context ctx, IEnumerable<SizeDiffContributor> rows)
    {
        if (ctx.WhyPaths is not { } whyPaths) return;

        var explained = rows
            .Select(c => (Contributor: c, Path: whyPaths.GetValueOrDefault(c.FullPath)))
            .Where(x => x.Path is { Count: > 0 })
            .ToList();
        if (explained.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("### Why did these appear?");
        foreach (var (contributor, path) in explained)
        {
            sb.AppendLine();
            sb.AppendLine($"**{MarkdownCodeSpan(contributor.Name)}** — kept by (root first):");
            sb.AppendLine();
            for (var i = 0; i < path!.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {MarkdownCodeSpan(path[i].Label)}"
                    + (path[i].Reason is { } reason ? $" ({reason})" : ""));
            }
        }
    }

    private static void AppendContributorTable(
        StringBuilder sb, string title, IEnumerable<SizeDiffContributor> contributors, bool snapshot)
    {
        var rows = contributors.ToList();
        if (rows.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine($"### {title}");
        sb.AppendLine();
        sb.AppendLine(snapshot ? "| Size | Kind | Name |" : "| Δ | Kind | Change | Name |");
        sb.AppendLine(snapshot ? "| ---: | --- | --- |" : "| ---: | --- | --- | --- |");
        var shortened = false;
        foreach (var c in rows)
        {
            var fullLabel = ContributorLabel(c);
            var label = MarkdownContributorLabel(c);
            shortened |= !string.Equals(label, fullLabel, StringComparison.Ordinal);
            if (snapshot)
            {
                sb.AppendLine($"| {MarkdownSize(DotsiderState.FormatSize(c.RightSize))} | {c.Kind} | "
                    + $"{MarkdownCodeSpan(label)} |");
            }
            else
            {
                sb.AppendLine($"| {MarkdownSize(SizeDiffTreemapView.FormatDelta(c.Delta))} | {c.Kind} | "
                    + $"{DirectionName(c)} | {MarkdownCodeSpan(label)} |");
            }
        }

        if (shortened)
        {
            sb.AppendLine();
            sb.AppendLine("_Full contributor names remain available in the JSON report._");
        }
    }

    private static string MarkdownContributorLabel(SizeDiffContributor contributor)
    {
        var fullLabel = ContributorLabel(contributor);
        if (fullLabel.Length <= MarkdownContributorLabelLimit)
            return fullLabel;

        var name = contributor.Name;
        var suffix = fullLabel[name.Length..];
        var openParenthesis = name.IndexOf('(');
        var closeParenthesis = name.LastIndexOf(')');
        if (openParenthesis >= 0 && closeParenthesis > openParenthesis)
        {
            var firstComma = name.IndexOf(',', openParenthesis + 1);
            var secondComma = firstComma < 0 ? -1 : name.IndexOf(',', firstComma + 1);
            var end = secondComma >= 0 ? secondComma : firstComma;
            if (end >= 0)
                return name[..end] + ", …)" + name[(closeParenthesis + 1)..] + suffix;
        }

        var available = Math.Max(20, MarkdownContributorLabelLimit - suffix.Length - 2);
        return name[..Math.Min(name.Length, available)] + "…" + suffix;
    }

    private static string BudgetLabel(SizeBudget budget)
    {
        if (budget.Name is { } name)
            return name;

        var scope = budget.Scope switch
        {
            SizeBudgetScope.Namespace => $"namespace {budget.Target}",
            SizeBudgetScope.Assembly => $"assembly {budget.Target}",
            _ => "total",
        };
        var limits = new List<string>(3);
        if (budget.MaxBytes is { } max)
            limits.Add($"max {DotsiderState.FormatSize(max)}");
        if (budget.MaxGrowthBytes is { } growth)
            limits.Add($"growth {DotsiderState.FormatSize(growth)}");
        if (budget.MaxGrowthPercent is { } percent)
            limits.Add($"growth {percent}%");
        return $"{scope}: {string.Join(", ", limits)}";
    }

    /// <summary>
    /// Keeps a formatted size on one line inside GitHub's narrow table columns.
    /// </summary>
    private static string MarkdownSize(string value) =>
        value.Replace(" ", "&nbsp;", StringComparison.Ordinal);

    /// <summary>
    /// Wraps arbitrary text in a CommonMark code span whose delimiter is longer than every
    /// backtick run in the value. Padding keeps leading or trailing backticks and spaces part
    /// of the rendered code instead of the delimiter.
    /// </summary>
    private static string MarkdownCodeSpan(string value)
    {
        var longestRun = 0;
        var currentRun = 0;
        foreach (var character in value)
        {
            if (character == '`')
            {
                currentRun++;
                longestRun = Math.Max(longestRun, currentRun);
            }
            else
            {
                currentRun = 0;
            }
        }

        var delimiter = new string('`', longestRun + 1);
        var needsPadding = value.Length > 0
            && (value[0] is '`' or ' ' || value[^1] is '`' or ' ');
        var padding = needsPadding ? " " : "";
        return $"{delimiter}{padding}{value}{padding}{delimiter}";
    }

    private static string ContributorLabel(SizeDiffContributor c)
    {
        var entries = Math.Max(c.LeftEntryCount, c.RightEntryCount);
        return c.Name
            + (entries > 1 ? $" ({entries} entries)" : "")
            + (c.AssemblyName.Length > 0 && c.AssemblyName != MstatSizeIndex.UnattributedName
                ? $"  [{c.AssemblyName}]"
                : c.AssemblyName == MstatSizeIndex.UnattributedName ? "  [(unattributed)]" : "");
    }

    private static string DirectionName(SizeDiffContributor c) => c.Diff switch
    {
        DiffKind.Added => "added",
        DiffKind.Removed => "removed",
        _ => c.Delta > 0 ? "grown" : "shrunk",
    };

    private static string BasisName(SizeBasis basis) =>
        basis == SizeBasis.FileSize ? "fileSize" : "mstatTotal";

    private static string MetricName(SizeBudgetMetric metric) => metric switch
    {
        SizeBudgetMetric.MaxBytes => "max size",
        SizeBudgetMetric.MaxGrowthBytes => "growth",
        _ => "growth %",
    };

    private static string FormatRange(long? left, long right) =>
        left is { } l
            ? $"{DotsiderState.FormatSize(l)} -> {DotsiderState.FormatSize(right)} "
                + $"(Δ{SizeDiffTreemapView.FormatDelta(right - l)})"
            : DotsiderState.FormatSize(right);

    private static string MarkdownRange(long? left, long right) =>
        left is { } l
            ? $"{MarkdownSize(DotsiderState.FormatSize(l))} → "
                + $"{MarkdownSize(DotsiderState.FormatSize(right))} "
                + $"(Δ{MarkdownSize(SizeDiffTreemapView.FormatDelta(right - l))})"
            : MarkdownSize(DotsiderState.FormatSize(right));
}
