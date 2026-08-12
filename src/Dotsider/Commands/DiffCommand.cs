using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Diagnostics;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Diagnostics;
using System.CommandLine;

namespace Dotsider.Commands;

/// <summary>
/// The "diff" command: compares two inputs side by side. Two managed assemblies open the
/// metadata diff TUI; two mstat-backed inputs — bare <c>.mstat</c> size reports or Native AOT
/// binaries with mstat sidecars — open the size-diff TUI, or emit the size-diff JSON document
/// headlessly under <c>--json</c>. Mixing an mstat-backed input with anything else is an
/// error: the two sides would measure different things.
/// </summary>
internal static class DiffCommand
{
    /// <summary>Contributor count for the headless <c>diff --json</c> document (size-check has --top).</summary>
    private const int JsonTop = 20;

    private static readonly Argument<FileInfo> s_leftArg = new("left")
    {
        Description = "First assembly, AOT binary, or .mstat size report"
    };

    private static readonly Argument<FileInfo> s_rightArg = new("right")
    {
        Description = "Second assembly, AOT binary, or .mstat size report"
    };

    private static readonly Option<int> s_escapeTimeoutOption = new("--escape-timeout", "-e")
    {
        Description = "Escape key timeout in milliseconds (default 100)",
        DefaultValueFactory = _ => 100
    };

    /// <summary>
    /// Creates the "diff" command.
    /// </summary>
    /// <param name="jsonOption">The global --json option; honored for mstat-backed pairs.</param>
    /// <returns>The configured command.</returns>
    public static Command Create(Option<bool> jsonOption)
    {
        var command = new Command("diff", "Compare two assemblies or AOT build sizes side-by-side")
        {
            s_leftArg,
            s_rightArg
        };
        command.Options.Add(s_escapeTimeoutOption);

        command.SetAction(async (parseResult, ct) =>
        {
            var left = parseResult.GetValue(s_leftArg)!;
            var right = parseResult.GetValue(s_rightArg)!;

            if (!left.Exists)
            {
                OutputFormatter.WriteError($"Error: File not found: {left.FullName}");
                return 1;
            }

            if (!right.Exists)
            {
                OutputFormatter.WriteError($"Error: File not found: {right.FullName}");
                return 1;
            }

            // Route by input kind. Mstat detection runs before any managed-assembly
            // interpretation because an mstat is itself a valid ECMA-335 assembly —
            // AssemblyLoader would happily open it as one and diff the report container.
            var leftSource = MstatLocator.Resolve(left.FullName);
            var rightSource = MstatLocator.Resolve(right.FullName);

            if (leftSource is not null && rightSource is not null)
            {
                if (parseResult.GetValue(jsonOption))
                    return RunHeadlessSizeDiff(leftSource, rightSource);

                var escTimeout = Math.Max(10, parseResult.GetValue(s_escapeTimeoutOption));
                return await RunSizeDiffTui(leftSource, rightSource, escTimeout, ct);
            }

            if (leftSource is not null || rightSource is not null)
            {
                var mstatSide = leftSource is not null ? left.Name : right.Name;
                var otherSide = leftSource is not null ? right.Name : left.Name;
                OutputFormatter.WriteError(
                    $"Error: cannot diff {mstatSide} (an mstat-backed size input) against "
                    + $"{otherSide}. Give two .mstat reports, two AOT binaries with mstat "
                    + "sidecars, or two assemblies.");
                return 1;
            }

            {
                var escTimeout = Math.Max(10, parseResult.GetValue(s_escapeTimeoutOption));
                return await RunAssemblyDiffTui(left, right, escTimeout, ct);
            }
        });

        return command;
    }

    private static int RunHeadlessSizeDiff(MstatSource leftSource, MstatSource rightSource)
    {
        var diff = MstatDiffer.Compare(leftSource.Data, rightSource.Data);
        var totals = SizeBasisResolver.Resolve(rightSource, leftSource, diff);
        var context = new SizeDiffReportWriter.Context(
            rightSource.BinaryPath ?? rightSource.MstatPath,
            leftSource.BinaryPath ?? leftSource.MstatPath,
            diff,
            totals.Basis,
            totals.RightTotal,
            totals.LeftTotal,
            JsonTop,
            WhyPaths: null,
            Budgets: null,
            TargetSource: rightSource,
            BaselineSource: leftSource);

        using var fmt = new OutputFormatter { JsonMode = true };
        fmt.WriteJson(SizeDiffReportWriter.BuildDocument(context));
        return 0;
    }

    private static async Task<int> RunSizeDiffTui(
        MstatSource leftSource, MstatSource rightSource, int escTimeoutMs, CancellationToken ct)
    {
        SizeDiffState? capturedState = null;

        await using var diagnosticsListener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () =>
            {
                var s = capturedState;
                if (s is null) return null;
                return DotsiderAppJsonContext.SerializeToElement(new SizeDiffSessionAssemblyPayload(
                    "size-diff",
                    $"{s.LeftName} ↔ {s.RightName}",
                    new SizeDiffSessionSourcePayload(
                        s.LeftSource.MstatPath,
                        s.LeftSource.BinaryPath,
                        s.LeftSource.BinaryFileSize,
                        s.Diff.Summary.LeftTotal),
                    new SizeDiffSessionSourcePayload(
                        s.RightSource.MstatPath,
                        s.RightSource.BinaryPath,
                        s.RightSource.BinaryFileSize,
                        s.Diff.Summary.RightTotal),
                    s.Diff.Summary.Delta));
            },
            currentViewProvider: () =>
            {
                var s = capturedState;
                if (s is null) return null;
                return DotsiderAppJsonContext.SerializeToElement(new SizeDiffSessionViewPayload(
                    "size-diff", s.CurrentTab + 1, s.FilterMode));
            });

        var escAdapter = new EscapeTimeoutPresentationAdapter(
            new ConsolePresentationAdapter(enableMouse: true),
            TimeSpan.FromMilliseconds(escTimeoutMs));

        var workload = new Hex1bAppWorkloadAdapter(escAdapter.Capabilities);
        var terminalOptions = new Hex1bTerminalOptions
        {
            PresentationAdapter = escAdapter,
            WorkloadAdapter = workload
        };
        terminalOptions.PresentationFilters.Add(new McpDiagnosticsPresentationFilter("dotsider-size-diff"));
        await using var terminal = new Hex1bTerminal(terminalOptions);
        escAdapter.Terminal = terminal;

        var appOptions = new Hex1bAppOptions
        {
            WorkloadAdapter = workload,
            Theme = DotsiderTheme.Create(),
            EnableMouse = true
        };

        SizeDiffApp? sizeDiffApp = null;
        Hex1bApp? hex1bApp = null;

        hex1bApp = new Hex1bApp(ctx =>
        {
            if (capturedState is null)
            {
                var state = new SizeDiffState(hex1bApp!, leftSource, rightSource);
                capturedState = state;
                sizeDiffApp = new SizeDiffApp(state);
            }

            return sizeDiffApp!.Build(ctx);
        }, appOptions);

        diagnosticsListener.StartListening();

        CursorColorHelper.SetThemeCursorColor();

        try
        {
            await hex1bApp.RunAsync(ct);
        }
        finally
        {
            CursorColorHelper.ResetCursorColor();
            hex1bApp.Dispose();
            capturedState?.Dispose();
        }

        return 0;
    }

    private static async Task<int> RunAssemblyDiffTui(
        FileInfo left, FileInfo right, int escTimeoutMs, CancellationToken ct)
    {
        var leftResult = AssemblyLoader.Open(left.FullName);
        AssemblyAnalyzer leftAnalyzer;
        switch (leftResult)
        {
            case AssemblyOpenResult.ApphostWithCompanion(var host, var companion):
                host.Dispose();
                OutputFormatter.WriteError(
                    $"Note: {left.Name} is a native apphost. "
                    + $"Analyzing {Path.GetFileName(companion)} instead.");
                leftAnalyzer = new AssemblyAnalyzer(companion);
                break;
            case AssemblyOpenResult.BundleEntry(var entry, _):
                OutputFormatter.WriteError(
                    $"Note: {left.Name} is a single-file bundle. "
                    + $"Analyzing entry assembly {entry.FileName} instead.");
                leftAnalyzer = entry;
                break;
            case AssemblyOpenResult.NativeAot(var aot):
                leftAnalyzer = aot;
                break;
            default:
                leftAnalyzer = ((AssemblyOpenResult.Direct)leftResult).Analyzer;
                break;
        }

        var rightResult = AssemblyLoader.Open(right.FullName);
        AssemblyAnalyzer rightAnalyzer;
        switch (rightResult)
        {
            case AssemblyOpenResult.ApphostWithCompanion(var host, var companion):
                host.Dispose();
                OutputFormatter.WriteError(
                    $"Note: {right.Name} is a native apphost. "
                    + $"Analyzing {Path.GetFileName(companion)} instead.");
                rightAnalyzer = new AssemblyAnalyzer(companion);
                break;
            case AssemblyOpenResult.BundleEntry(var entry, _):
                OutputFormatter.WriteError(
                    $"Note: {right.Name} is a single-file bundle. "
                    + $"Analyzing entry assembly {entry.FileName} instead.");
                rightAnalyzer = entry;
                break;
            case AssemblyOpenResult.NativeAot(var aot):
                rightAnalyzer = aot;
                break;
            default:
                rightAnalyzer = ((AssemblyOpenResult.Direct)rightResult).Analyzer;
                break;
        }

        DiffState? capturedDiffState = null;

        await using var diagnosticsListener = new DotsiderDiagnosticsListener(
            () => null,
            assemblyInfoProvider: () =>
            {
                var s = capturedDiffState;
                if (s is null) return null;
                return DotsiderAppJsonContext.SerializeToElement(new DiffSessionAssemblyPayload(
                    "diff",
                    $"{s.Left.FileName} ↔ {s.Right.FileName}",
                    new DiffSessionSidePayload(
                        s.Left.FilePath,
                        s.Left.FileName,
                        s.Left.FileSize,
                        s.Left.AssemblyName,
                        s.Left.AssemblyVersion,
                        s.Left.TargetFramework),
                    new DiffSessionSidePayload(
                        s.Right.FilePath,
                        s.Right.FileName,
                        s.Right.FileSize,
                        s.Right.AssemblyName,
                        s.Right.AssemblyVersion,
                        s.Right.TargetFramework)));
            },
            currentViewProvider: () =>
            {
                var s = capturedDiffState;
                if (s is null) return null;
                return DotsiderAppJsonContext.SerializeToElement(new DiffSessionViewPayload(
                    "diff", s.CurrentTab + 1, s.FilterMode));
            });

        var diffEscAdapter = new EscapeTimeoutPresentationAdapter(
            new ConsolePresentationAdapter(enableMouse: true),
            TimeSpan.FromMilliseconds(escTimeoutMs));

        var diffWorkload = new Hex1bAppWorkloadAdapter(diffEscAdapter.Capabilities);
        var diffTerminalOptions = new Hex1bTerminalOptions
        {
            PresentationAdapter = diffEscAdapter,
            WorkloadAdapter = diffWorkload
        };
        diffTerminalOptions.PresentationFilters.Add(new McpDiagnosticsPresentationFilter("dotsider-diff"));
        await using var diffTerminal = new Hex1bTerminal(diffTerminalOptions);
        diffEscAdapter.Terminal = diffTerminal;

        var diffAppOptions = new Hex1bAppOptions
        {
            WorkloadAdapter = diffWorkload,
            Theme = DotsiderTheme.Create(),
            EnableMouse = true
        };

        DiffApp? diffApp = null;
        Hex1bApp? diffHex1bApp = null;

        diffHex1bApp = new Hex1bApp(ctx =>
        {
            if (capturedDiffState is null)
            {
                var diffState = new DiffState(diffHex1bApp!, leftAnalyzer, rightAnalyzer);
                capturedDiffState = diffState;
                diffApp = new DiffApp(diffState);
            }

            return diffApp!.Build(ctx);
        }, diffAppOptions);

        diagnosticsListener.StartListening();

        CursorColorHelper.SetThemeCursorColor();

        try
        {
            await diffHex1bApp.RunAsync(ct);
        }
        finally
        {
            CursorColorHelper.ResetCursorColor();
            diffHex1bApp.Dispose();
        }

        return 0;
    }
}
