using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Dynamic Analysis tab (Tab 8), providing runtime event
/// tracing via EventPipe for executable .NET assemblies.
/// </summary>
public static class DynamicAnalysisView
{
    private static readonly Hex1bColor Cyan = Hex1bColor.FromRgb(0, 200, 200);
    private static readonly Hex1bColor Yellow = Hex1bColor.FromRgb(220, 200, 80);
    private static readonly Hex1bColor Red = Hex1bColor.FromRgb(220, 80, 80);
    private static readonly Hex1bColor Green = Hex1bColor.FromRgb(80, 200, 80);
    private static readonly Hex1bColor Purple = Hex1bColor.FromRgb(180, 130, 220);
    private static readonly Hex1bColor Blue = Hex1bColor.FromRgb(80, 140, 220);
    private static readonly Hex1bColor Orange = Hex1bColor.FromRgb(220, 140, 60);
    private static readonly Hex1bColor DimGray = Hex1bColor.FromRgb(100, 100, 120);
    private static readonly Hex1bColor Teal = Hex1bColor.FromRgb(0, 200, 180);
    private static readonly Hex1bColor LabelColor = Hex1bColor.FromRgb(100, 130, 160);

    internal static readonly Dictionary<TraceEventCategory, Hex1bColor> CategoryColors = new()
    {
        [TraceEventCategory.GC] = Cyan,
        [TraceEventCategory.JIT] = Yellow,
        [TraceEventCategory.Exception] = Red,
        [TraceEventCategory.Loader] = Green,
        [TraceEventCategory.Threading] = Purple,
        [TraceEventCategory.Http] = Blue,
        [TraceEventCategory.Socket] = Orange,
        [TraceEventCategory.Counter] = DimGray,
        [TraceEventCategory.Other] = DimGray,
    };

    /// <summary>
    /// Builds the Dynamic Analysis view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the Dynamic tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        // NativeAOT — no CLR metadata
        if (state.IsNativeAot)
            return BuildMessageView(ctx,
                "This assembly does not contain CLR metadata.",
                "EventPipe requires the CoreCLR runtime.",
                "NativeAOT binaries cannot be traced with this tool.");

        // Library DLL — no entry point
        if (!state.HasEntryPoint)
            return BuildMessageView(ctx,
                "This assembly is a library (no entry point).",
                "Dynamic analysis requires an executable assembly.",
                "Open an .exe or a console app .dll to enable runtime tracing.");

        var tracer = state.Tracer;

        // Idle — not yet launched
        if (tracer is null || tracer.ProcessState == TraceProcessState.Idle)
            return BuildIdleView(ctx, state);

        // Active or completed trace
        return BuildActiveView(ctx, state, tracer);
    }

    private static Hex1bWidget BuildMessageView(WidgetContext<VStackWidget> ctx,
        string line1, string line2, string line3)
    {
        return ctx.VStack(outer =>
        [
            outer.Text(""),
            outer.Text($"  {line1}"),
            outer.Text($"  {line2}"),
            outer.Text(""),
            outer.Text($"  {line3}")
        ]).Fill();
    }

    private static Hex1bWidget BuildIdleView(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var argsDisplay = string.IsNullOrEmpty(state.DynamicArguments)
            ? "(none — press 'a' to set)"
            : state.DynamicArguments;

        return ctx.VStack(outer =>
        [
            outer.Text(""),
            IdleLine(outer, "Assembly:   ", state.Analyzer.FileName),
            IdleLine(outer, "Entry Point:", $"0x{state.Analyzer.ClrHeader!.EntryPointToken:X8}"),
            IdleLine(outer, "Args:       ", argsDisplay),
            outer.Text(""),
            outer.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Teal),
                outer.Text("  Press Enter to launch with EventPipe tracing.")),
            outer.Text(""),
            outer.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, LabelColor),
                outer.Text("  Providers:")),
            outer.Text("    CLR Runtime — GC, JIT, Exceptions, Loader, Threading"),
            outer.Text("    System.Runtime — Performance Counters (1s interval)"),
            outer.Text("    System.Net.Http, System.Net.Sockets")
        ])
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Enter).Global().Action(_ =>
            {
                if (!state.DynamicEditingArgs)
                {
                    state.Tracer = new RuntimeTracer(
                        state.Analyzer.FilePath, state.DynamicArguments, () => state.App.Invalidate());
                    state.Tracer.Start();
                    state.App.RequestFocus(node =>
                        node.GetType().Name.StartsWith("TableNode"));
                    state.App.Invalidate();
                }
            }, "Launch process");

            bindings.Key(Hex1bKey.A).Global().Action(_ =>
            {
                state.DynamicEditingArgs = !state.DynamicEditingArgs;
                state.App.Invalidate();
            }, "Edit args");
        })
        .Fill();
    }

    private static Hex1bWidget BuildActiveView(
        WidgetContext<VStackWidget> ctx, DotsiderState state, RuntimeTracer tracer)
    {
        var search = state.Search[TabId.Dynamic];
        // Search bar hidden on Counters (1) and Summary (3) sub-tabs
        var showSearch = state.DynamicSubTab is DynamicSubTabId.Events or DynamicSubTabId.Output;

        // Set up match navigation
        state.NavigateNextMatch = null;
        state.NavigatePrevMatch = null;

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Status bar
            widgets.Add(BuildStatusBar(outer, tracer));

            // Search bar (only on Events and Output sub-tabs)
            if (showSearch)
                SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Sub-tabs: Events | Counters | Output | Summary
            widgets.Add(outer.TabPanel(tp =>
            [
                tp.Tab("Events", t => [BuildEventsSubTab(t, state, tracer)])
                    .Selected(state.DynamicSubTab == DynamicSubTabId.Events),
                tp.Tab("Counters", t => [BuildCountersSubTab(t, tracer)])
                    .Selected(state.DynamicSubTab == DynamicSubTabId.Counters),
                tp.Tab("Output", t => [BuildOutputSubTab(t, state, tracer)])
                    .Selected(state.DynamicSubTab == DynamicSubTabId.Output),
                tp.Tab("Summary", t => [BuildSummarySubTab(t, tracer)])
                    .Selected(state.DynamicSubTab == DynamicSubTabId.Summary)
            ])
            .OnSelectionChanged(e =>
            {
                state.DynamicSubTab = e.SelectedIndex;
                state.App.Invalidate();
            })
            .Compact()
            .Fill());

            return [.. widgets];
        })
        .WithInputBindings(bindings =>
        {
            var isSearchEditing = search.IsActive && !search.IsConfirmed;

            // Left/Right arrows to switch sub-tabs (suppressed during search editing)
            if (!isSearchEditing)
            {
                bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ =>
                {
                    if (state.DynamicSubTab > 0)
                    {
                        state.DynamicSubTab--;
                        state.App.Invalidate();
                    }
                }, "Previous sub-tab");

                bindings.Key(Hex1bKey.RightArrow).Global().Action(_ =>
                {
                    if (state.DynamicSubTab < DynamicSubTabId.Count - 1)
                    {
                        state.DynamicSubTab++;
                        state.App.Invalidate();
                    }
                }, "Next sub-tab");
            }

            // Ctrl+K to stop the traced process
            bindings.Ctrl().Key(Hex1bKey.K).Global().Action(_ =>
            {
                tracer.Stop();
                state.App.Invalidate();
            }, "Stop traced process");

            // Enter to re-run after exit (suppressed during search editing to avoid
            // conflicting with DotsiderApp's global "Confirm search" Enter binding).
            // On the Events sub-tab, JIT navigation takes priority: if the focused
            // row is a JIT event matching a method in the analyzer, Enter navigates
            // to the IL Inspector instead of re-running.
            if (!isSearchEditing && tracer.ProcessState is TraceProcessState.Exited or TraceProcessState.Error)
            {
                bindings.Key(Hex1bKey.Enter).Global().Action(_ =>
                {
                    if (TryNavigateJitEvent(state, tracer))
                        return;

                    state.Tracer?.Dispose();
                    state.Tracer = new RuntimeTracer(
                        state.Analyzer.FilePath, state.DynamicArguments, () => state.App.Invalidate());
                    state.Tracer.Start();
                    state.DynamicEventsFocusedKey = null;
                    state.DynamicOutputFocusedKey = null;
                    state.DynamicCategoryFilter = null;
                    state.App.RequestFocus(node =>
                        node.GetType().Name.StartsWith("TableNode"));
                    state.App.Invalidate();
                }, "Re-run process");
            }

            bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
            {
                if (search.IsActive)
                {
                    search.Dismiss();
                    state.App.Invalidate();
                }
            }, "Esc");
        })
        .Fill();
    }

    private static Hex1bWidget BuildStatusBar(WidgetContext<VStackWidget> ctx, RuntimeTracer tracer)
    {
        var (statusText, stateColor) = tracer.ProcessState switch
        {
            TraceProcessState.Starting =>
                ("Connecting to diagnostic port...", DimGray),
            TraceProcessState.Running =>
                ($"Running (PID {tracer.ProcessId}) | {tracer.Elapsed:mm\\:ss}",
                    Green),
            TraceProcessState.Exited when tracer.ExitCode == 0 =>
                ($"Exited (code 0) | Duration: {tracer.Elapsed:mm\\:ss}", Teal),
            TraceProcessState.Exited =>
                ($"Exited (code {tracer.ExitCode}) | Duration: {tracer.Elapsed:mm\\:ss}",
                    Yellow),
            TraceProcessState.Error =>
                ($"Error: {tracer.ErrorMessage}", Red),
            _ => ("Idle", DimGray)
        };

        var dot = tracer.ProcessState == TraceProcessState.Running ? "●" : "○";

        return ctx.HStack(row =>
        [
            row.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, stateColor),
                row.Text($" {dot} {statusText} ")).Fill()
        ]).FixedHeight(1);
    }

    private static Hex1bWidget BuildEventsSubTab(
        WidgetContext<VStackWidget> ctx, DotsiderState state, RuntimeTracer tracer)
    {
        var events = (IReadOnlyList<TraceEventEntry>)tracer.GetEvents();
        var search = state.Search[TabId.Dynamic];
        var query = search.Query;

        if (state.DynamicCategoryFilter is { } filter)
            events = [.. events.Where(e => e.Category == filter)];

        // Apply search filter
        if (!string.IsNullOrEmpty(query))
        {
            events = [.. events.Where(e =>
                e.EventName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Detail.Contains(query, StringComparison.OrdinalIgnoreCase))];
            search.SetMatchCount(events.Count);
        }

        var filterText = state.DynamicCategoryFilter is { } f
            ? $" | Filter: {f} (Esc to clear)"
            : " | g/j/e/l/t/h/s to filter";

        return ctx.VStack(inner =>
        [
            inner.HStack(row =>
            [
                row.Text($" Events: {events.Count}{filterText}").Fill()
            ]).FixedHeight(1),

            inner.Table(events)
                .RowKey(e => $"{e.Timestamp.Ticks}:{e.EventName}:{e.Detail}")
                .Header(h =>
                [
                    h.Cell("Time").Width(SizeHint.Fixed(12)),
                    h.Cell("Category").Width(SizeHint.Fixed(12)),
                    h.Cell("Event").Width(SizeHint.Fixed(22)),
                    h.Cell("Detail").Width(SizeHint.Fill)
                ])
                .Row((r, evt, _) =>
                [
                    r.Cell(evt.Timestamp.ToString(@"mm\:ss\.fff")),
                    r.Cell(c => c.ThemePanel(
                        t => t.Set(GlobalTheme.ForegroundColor,
                            CategoryColors.GetValueOrDefault(evt.Category, Hex1bColor.White)),
                        c.Text(evt.Category.ToString()))),
                    r.Cell(c => HighlightHelper.HighlightCell(c, evt.EventName, query,
                        !string.IsNullOrEmpty(query))),
                    r.Cell(c => HighlightHelper.HighlightCell(c, evt.Detail, query,
                        !string.IsNullOrEmpty(query)))
                ])
                .Focus(state.DynamicEventsFocusedKey)
                .OnFocusChanged(key => state.DynamicEventsFocusedKey = key)
                .Compact()
                .Empty(e => e.Text("  Waiting for events..."))
                .FillWidth()
                .FillHeight()
        ])
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.G).Action(_ => SetFilter(state, TraceEventCategory.GC), "Filter GC");
            bindings.Key(Hex1bKey.J).Action(_ => SetFilter(state, TraceEventCategory.JIT), "Filter JIT");
            bindings.Key(Hex1bKey.E).Action(_ => SetFilter(state, TraceEventCategory.Exception), "Filter Exceptions");
            bindings.Key(Hex1bKey.L).Action(_ => SetFilter(state, TraceEventCategory.Loader), "Filter Loader");
            bindings.Key(Hex1bKey.H).Action(_ => SetFilter(state, TraceEventCategory.Http), "Filter HTTP");
            bindings.Key(Hex1bKey.T).Action(_ => SetFilter(state, TraceEventCategory.Threading), "Filter Threading");
            bindings.Key(Hex1bKey.S).Action(_ => SetFilter(state, TraceEventCategory.Socket), "Filter Socket");

            // Enter on JIT event: navigate to IL Inspector for the jitted method.
            // During Running state, this is the only Enter handler (the .Global()
            // re-run handler is not registered). After Exit, the .Global() handler
            // calls TryNavigateJitEvent first, so this local handler is redundant
            // but harmless (the .Global() handler takes priority).
            bindings.Key(Hex1bKey.Enter).Action(_ =>
            {
                TryNavigateJitEvent(state, tracer);
            }, "Go to IL");

            bindings.Key(Hex1bKey.Escape).Action(_ =>
            {
                if (search.IsActive)
                {
                    search.Dismiss();
                    state.App.Invalidate();
                }
                else
                {
                    SetFilter(state, null);
                }
            }, "Esc");
        })
        .Fill();
    }

    private static void SetFilter(DotsiderState state, TraceEventCategory? category)
    {
        state.DynamicCategoryFilter = category;
        state.DynamicEventsFocusedKey = null;
        state.App.Invalidate();
    }

    private static Hex1bWidget BuildCountersSubTab(
        WidgetContext<VStackWidget> ctx, RuntimeTracer tracer)
    {
        var counters = tracer.GetLatestCounters();

        if (counters is null)
            return ctx.Text("  Waiting for counter data (updates every ~1s)...").Fill();

        return ctx.VStack(inner =>
        [
            // CPU gauge
            inner.Border(
                inner.Surface(s =>
                [
                    s.Layer(surface => DrawGauge(surface, "CPU Usage",
                        counters.CpuUsagePercent, 100, "%", Teal))
                ]).FixedHeight(1)
            ).Title(" CPU ").FixedHeight(3),

            // Memory
            inner.Border(
                inner.VStack(v =>
                [
                    CounterLine(v, "  Working Set:  ", $"{counters.WorkingSetMb:F1} MB"),
                    CounterLine(v, "  GC Heap Size: ", $"{counters.GcHeapSizeMb:F1} MB")
                ])
            ).Title(" Memory ").FixedHeight(4),

            // GC Collections
            inner.Border(
                inner.HStack(row =>
                [
                    CounterLine(row, "  Gen 0: ", $"{counters.Gen0Collections}"),
                    CounterLine(row, "    Gen 1: ", $"{counters.Gen1Collections}"),
                    CounterLine(row, "    Gen 2: ", $"{counters.Gen2Collections}").Fill()
                ])
            ).Title(" GC Collections ").FixedHeight(3),

            // Threading
            inner.Border(
                inner.HStack(row =>
                [
                    CounterLine(row, "  Threads: ", $"{counters.ThreadPoolThreadCount}"),
                    CounterLine(row, "    Queue: ", $"{counters.ThreadPoolQueueLength}"),
                    CounterLine(row, "    Exceptions: ", $"{counters.ExceptionCount}"),
                    CounterLine(row, "    Timers: ", $"{counters.ActiveTimerCount}").Fill()
                ])
            ).Title(" Threading ").FixedHeight(3),

            // Spacer
            inner.Text("").Fill()
        ]).Fill();
    }

    private static void DrawGauge(Surface surface, string label, double value,
        double max, string unit, Hex1bColor color)
    {
        var w = surface.Width;
        var labelText = $"{label}: {value:F1}{unit} ";
        var barStart = Math.Min(labelText.Length + 1, w - 2);
        var barWidth = Math.Max(0, w - barStart - 1);
        var filled = (int)(barWidth * Math.Clamp(value / max, 0, 1));

        surface.WriteText(1, 0, labelText, Hex1bColor.White);

        for (var x = 0; x < barWidth; x++)
        {
            var c = x < filled ? color : Hex1bColor.FromRgb(40, 40, 50);
            surface.WriteChar(barStart + x, 0, '█', c);
        }
    }

    private static Hex1bWidget BuildOutputSubTab(
        WidgetContext<VStackWidget> ctx, DotsiderState state, RuntimeTracer tracer)
    {
        var output = (IReadOnlyList<OutputLine>)tracer.GetOutput();
        var search = state.Search[TabId.Dynamic];
        var query = search.Query;

        // Apply search filter
        if (!string.IsNullOrEmpty(query))
        {
            output = [.. output.Where(o =>
                o.Text.Contains(query, StringComparison.OrdinalIgnoreCase))];
            search.SetMatchCount(output.Count);
        }

        return ctx.Table(output)
            .RowKey(o => $"{o.Timestamp.Ticks}:{o.Text}")
            .Header(h =>
            [
                h.Cell("Time").Width(SizeHint.Fixed(12)),
                h.Cell("Src").Width(SizeHint.Fixed(6)),
                h.Cell("Output").Width(SizeHint.Fill)
            ])
            .Row((r, line, _) =>
            [
                r.Cell(line.Timestamp.ToString(@"mm\:ss\.fff")),
                r.Cell(c => line.IsStdErr
                    ? c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Red), c.Text("err"))
                    : c.Text("out")),
                r.Cell(c => line.IsStdErr
                    ? c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Red), c.Text(line.Text))
                    : HighlightHelper.HighlightCell(c, line.Text, query,
                        !string.IsNullOrEmpty(query)))
            ])
            .Focus(state.DynamicOutputFocusedKey)
            .OnFocusChanged(key => state.DynamicOutputFocusedKey = key)
            .Compact()
            .Empty(e => e.Text("  Process output will appear here..."))
            .FillWidth()
            .FillHeight();
    }

    private static Hex1bWidget BuildSummarySubTab(
        WidgetContext<VStackWidget> ctx, RuntimeTracer tracer)
    {
        var summary = tracer.GetSummary();

        return ctx.VStack(inner =>
        [
            inner.Border(
                inner.VStack(info =>
                [
                    InfoLine(info, "Total Events", summary.TotalEvents.ToString("N0")),
                    InfoLine(info, "Duration", summary.Duration.ToString(@"mm\:ss\.fff")),
                    InfoLine(info, "JIT'd Methods", summary.JittedMethodCount.ToString("N0")),
                    InfoLine(info, "GC Collections", summary.TotalGcCollections.ToString("N0")),
                    InfoLine(info, "Exceptions", summary.TotalExceptions.ToString("N0")),
                    InfoLine(info, "Peak Working Set", $"{summary.PeakWorkingSetMb:F1} MB"),
                    InfoLine(info, "Peak GC Heap", $"{summary.PeakGcHeapMb:F1} MB")
                ])
            ).Title(" Trace Summary ").FixedHeight(9),

            // Event distribution as a simple bar chart using Surface
            inner.Border(
                inner.Surface(s =>
                [
                    s.Layer(surface => DrawEventDistribution(surface, summary.EventsByCategory))
                ]).Fill()
            ).Title(" Event Distribution ").Fill()
        ]).Fill();
    }

    private static Hex1bWidget IdleLine<T>(WidgetContext<T> ctx, string label, string value)
        where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, LabelColor),
                row.Text($"  {label}")).FixedWidth(16),
            row.Text(value).Fill()
        ]).FixedHeight(1);
    }

    private static Hex1bWidget CounterLine<T>(WidgetContext<T> ctx, string label, string value)
        where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, LabelColor),
                row.Text(label)),
            row.Text(value)
        ]);
    }

    private static Hex1bWidget InfoLine<T>(WidgetContext<T> ctx, string label, string value)
        where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, LabelColor),
                row.Text($"  {label}:")).FixedWidth(22),
            row.Text($" {value}").Fill()
        ]).FixedHeight(1);
    }

    /// <summary>
    /// Attempts to navigate from a focused JIT event row to the IL Inspector.
    /// Looks up the focused event in the tracer's event list by row key, then
    /// resolves the method by metadata token (preferred) or by name (fallback).
    /// Returns true if navigation occurred.
    /// </summary>
    internal static bool TryNavigateJitEvent(DotsiderState state, RuntimeTracer tracer)
    {
        if (state.DynamicSubTab != DynamicSubTabId.Events) return false;
        if (state.DynamicEventsFocusedKey is not string focusedKey) return false;

        var evt = tracer.GetEvents().FirstOrDefault(e =>
            e.Category == TraceEventCategory.JIT
            && $"{e.Timestamp.Ticks}:{e.EventName}:{e.Detail}" == focusedKey);
        if (evt is null) return false;

        var method = evt.MetadataToken > 0
            ? state.Analyzer.MethodDefs.FirstOrDefault(m => m.Token == evt.MetadataToken)
            : null;
        if (method is null
            && TryParseJitDetail(evt.Detail, out var declaringType, out var methodName))
        {
            method = state.Analyzer.MethodDefs.FirstOrDefault(
                m => m.DeclaringType == declaringType && m.Name == methodName);
        }

        if (method is null) return false;

        state.NavigateToIlMethod(method);
        return true;
    }

    /// <summary>
    /// Parses a JIT event detail string (format: "Namespace.Type.MethodName")
    /// into the declaring type and method name components.
    /// RuntimeTracer emits "{MethodNamespace}.{MethodName}" where MethodNamespace
    /// is the fully qualified declaring type.
    /// </summary>
    internal static bool TryParseJitDetail(string detail, out string declaringType, out string methodName)
    {
        declaringType = "";
        methodName = "";
        var lastDot = detail.LastIndexOf('.');
        if (lastDot <= 0) return false;
        declaringType = detail[..lastDot];
        methodName = detail[(lastDot + 1)..];
        return methodName.Length > 0;
    }

    private static void DrawEventDistribution(Surface surface,
        IReadOnlyDictionary<TraceEventCategory, int> eventsByCategory)
    {
        var total = eventsByCategory.Values.Sum();
        if (total == 0)
        {
            surface.WriteText(2, 1, "No events collected yet", DimGray);
            return;
        }

        var sorted = eventsByCategory
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .ToList();

        var maxBarWidth = Math.Max(1, surface.Width - 26);
        var y = 1;

        foreach (var (category, count) in sorted)
        {
            if (y >= surface.Height - 1) break;

            var pct = (double)count / total;
            var barLen = Math.Max(1, (int)(pct * maxBarWidth));
            var color = CategoryColors.GetValueOrDefault(category, DimGray);

            var label = $"{category,-12} {count,6}  ";
            surface.WriteText(1, y, label, Hex1bColor.White);

            for (var x = 0; x < barLen; x++)
                surface.WriteChar(label.Length + 1 + x, y, '█', color);

            var pctText = $" {pct:P0}";
            surface.WriteText(label.Length + 1 + barLen, y, pctText, DimGray);

            y++;
        }
    }
}
