using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Infrastructure;
using System.CommandLine;

namespace Dotsider.Commands;

/// <summary>
/// Headless size-regression command for CI pipelines: compares a Native AOT build against a
/// baseline via their mstat size reports and enforces size budgets. Exit codes: 0 the report
/// was produced and every error-severity budget passed; 1 usage or input error; 2 a budget
/// was exceeded.
/// </summary>
internal static class SizeCheckCommand
{
    private static readonly Argument<FileInfo> s_targetArg = new("target")
    {
        Description = "Native AOT binary or .mstat size report to check"
    };

    private static readonly Option<FileInfo?> s_baselineOption = new("--baseline")
    {
        Description = "Baseline binary or .mstat to diff against"
    };

    private static readonly Option<string[]> s_budgetOption = new("--budget")
    {
        Description = "Size budget (e.g. max=25mb or ns=System.Text.Json:growth=10kb); repeatable",
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = false
    };

    private static readonly Option<FileInfo?> s_budgetFileOption = new("--budget-file")
    {
        Description = "JSON file with budgets ({ \"budgets\": [spec or object, ...] })"
    };

    private static readonly Option<int> s_topOption = new("--top")
    {
        Description = "Top contributors per section and per violated budget (default 10)",
        DefaultValueFactory = _ => 10
    };

    private static readonly Option<bool> s_whyOption = new("--why")
    {
        Description = "Attach dependency chains for top added contributors (needs the target's DGML sidecar)"
    };

    private static readonly Option<string?> s_formatOption = new("--format")
    {
        Description = "Output format: text (default), json, or markdown"
    };

    private static readonly Option<string?> s_summaryFileOption = new("--summary-file")
    {
        Description = "Additionally write the markdown report to a file (e.g. \"$GITHUB_STEP_SUMMARY\")"
    };

    private static readonly Option<string?> s_outputOption = new("--output", "-o")
    {
        Description = "Write output to a file instead of stdout"
    };

    /// <summary>
    /// Creates the "size-check" command.
    /// </summary>
    /// <param name="jsonOption">The global --json option (equivalent to --format json).</param>
    /// <returns>The configured command.</returns>
    public static Command Create(Option<bool> jsonOption)
    {
        var command = new Command(
            "size-check", "Compare AOT build size against a baseline and enforce size budgets")
        {
            s_targetArg,
            s_baselineOption,
            s_budgetOption,
            s_budgetFileOption,
            s_topOption,
            s_whyOption,
            s_formatOption,
            s_summaryFileOption,
            s_outputOption
        };

        command.SetAction((parseResult, _) =>
        {
            var target = parseResult.GetValue(s_targetArg)!;
            var baseline = parseResult.GetValue(s_baselineOption);
            var budgetSpecs = parseResult.GetValue(s_budgetOption) ?? [];
            var budgetFile = parseResult.GetValue(s_budgetFileOption);
            var top = Math.Max(0, parseResult.GetValue(s_topOption));
            var why = parseResult.GetValue(s_whyOption);
            var json = parseResult.GetValue(jsonOption);
            var formatValue = parseResult.GetValue(s_formatOption);
            var summaryFile = parseResult.GetValue(s_summaryFileOption);
            var outputPath = parseResult.GetValue(s_outputOption);

            var format = formatValue?.ToLowerInvariant();
            if (format is not (null or "text" or "json" or "markdown"))
            {
                OutputFormatter.WriteError($"Error: --format '{formatValue}' is not text, json, or markdown.");
                return Task.FromResult(1);
            }

            if (json && format is "text" or "markdown")
            {
                OutputFormatter.WriteError("Error: --json conflicts with the requested --format.");
                return Task.FromResult(1);
            }

            format ??= json ? "json" : "text";

            return Task.FromResult(Run(
                target, baseline, budgetSpecs, budgetFile, top, why, format, summaryFile, outputPath));
        });

        return command;
    }

    private static int Run(
        FileInfo target, FileInfo? baseline, string[] budgetSpecs, FileInfo? budgetFile,
        int top, bool why, string format, string? summaryFile, string? outputPath)
    {
        if (!target.Exists)
        {
            OutputFormatter.WriteError($"Error: File not found: {target.FullName}");
            return 1;
        }

        if (baseline is { Exists: false })
        {
            OutputFormatter.WriteError($"Error: File not found: {baseline.FullName}");
            return 1;
        }

        var budgets = new List<SizeBudget>();
        try
        {
            if (budgetFile is not null)
                budgets.AddRange(SizeBudgetFile.Load(budgetFile.FullName));
            foreach (var spec in budgetSpecs)
                budgets.Add(SizeBudgetParser.Parse(spec));
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            OutputFormatter.WriteError($"Error: {ex.Message}");
            return 1;
        }

        if (baseline is null)
        {
            var growthBudget = budgets.FirstOrDefault(b =>
                b.MaxGrowthBytes is not null || b.MaxGrowthPercent is not null);
            if (growthBudget is not null)
            {
                OutputFormatter.WriteError(
                    $"Error: budget '{growthBudget.Name ?? growthBudget.ToString()}' limits growth, "
                    + "which needs --baseline.");
                return 1;
            }

            if (budgets.Count == 0)
            {
                OutputFormatter.WriteError(
                    "Error: nothing to do — give --baseline for a size-diff report, or --budget "
                    + "max=... for an absolute gate. (For a single build's breakdown, use "
                    + "'dotsider analyze <file> --size'.)");
                return 1;
            }
        }

        if (MstatLocator.Resolve(target.FullName) is not { } targetSource)
        {
            OutputFormatter.WriteError(
                $"Error: {target.Name} is not mstat-backed — pass a .mstat size report or a "
                + "Native AOT binary published with IlcGenerateMstatFile (sidecar beside the binary).");
            return 1;
        }

        MstatSource? baselineSource = null;
        if (baseline is not null)
        {
            baselineSource = MstatLocator.Resolve(baseline.FullName);
            if (baselineSource is null)
            {
                OutputFormatter.WriteError(
                    $"Error: {baseline.Name} is not mstat-backed — pass a .mstat size report or a "
                    + "Native AOT binary published with IlcGenerateMstatFile.");
                return 1;
            }
        }

        var diff = MstatDiffer.Compare(baselineSource?.Data ?? MstatData.Empty, targetSource.Data);
        var totals = SizeBasisResolver.Resolve(targetSource, baselineSource, diff);

        SizeBudgetReport? report = null;
        if (budgets.Count > 0)
        {
            report = SizeBudgetEvaluator.Evaluate(
                budgets, diff, totals.Basis, totals.RightTotal, totals.LeftTotal, defaultTopN: top);
        }

        var whyPaths = why ? ResolveWhyPaths(targetSource, diff, top) : null;
        if (why && whyPaths is null)
        {
            OutputFormatter.WriteError(
                "Warning: --why needs a DGML sidecar beside the target "
                + "(publish with IlcGenerateDgmlFile); continuing without dependency chains.");
        }

        var context = new SizeDiffReportWriter.Context(
            targetSource.BinaryPath ?? targetSource.MstatPath,
            baselineSource is null ? null : baselineSource.BinaryPath ?? baselineSource.MstatPath,
            diff, totals.Basis, totals.RightTotal, totals.LeftTotal, top, whyPaths, report);

        using (var fmt = new OutputFormatter(outputPath) { JsonMode = format == "json" })
        {
            switch (format)
            {
                case "json":
                    fmt.WriteJson(SizeDiffReportWriter.BuildDocument(context));
                    break;
                case "markdown":
                    fmt.WriteBlock(SizeDiffReportWriter.BuildMarkdown(context));
                    break;
                default:
                    SizeDiffReportWriter.WriteText(fmt, context);
                    break;
            }
        }

        if (summaryFile is not null)
        {
            try
            {
                File.WriteAllText(summaryFile, SizeDiffReportWriter.BuildMarkdown(context));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                OutputFormatter.WriteError($"Error: cannot write --summary-file: {ex.Message}");
                return 1;
            }
        }

        return report is { Passed: false } ? 2 : 0;
    }

    /// <summary>
    /// Resolves "why did this appear" chains for the top added contributors against the
    /// target's dependency graph, or null when no DGML sits beside the target. Added rows
    /// are selected before the top-N cut: large removals must not push the added rows the
    /// report actually shows out of coverage.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<DgmlPathStep>>? ResolveWhyPaths(
        MstatSource target, MstatDiffResult diff, int top)
    {
        if (target.DgmlPath is not { } dgmlPath || DgmlReader.Read(dgmlPath) is not { } dgml)
            return null;

        var paths = new Dictionary<string, IReadOnlyList<DgmlPathStep>>(StringComparer.Ordinal);
        var added = diff.Contributors
            .Where(c => c.Diff == DiffKind.Added && c.RightNodeNames.Count > 0)
            .Take(top);
        foreach (var contributor in added)
        {
            var path = dgml.PathToRoot(contributor.RightNodeNames[0]);
            if (path.Count > 0)
                paths[contributor.FullPath] = path;
        }

        return paths;
    }
}
