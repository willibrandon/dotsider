using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Reproduces #92: the focused row on the Dynamic tab's Events table shows
/// category-colored text (yellow, green, etc.) instead of black on teal.
/// </summary>
[Collection("SampleAssemblies")]
public class DynamicFocusColorTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        DotsiderApp? dotsiderApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new DotsiderState(_hex1bApp!, samples.HelloWorldDll);
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp);
    }

    /// <summary>
    /// Verifies events focused row category cell uses black foreground.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Events_FocusedRow_CategoryCellUsesBlackForeground()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        // Navigate to Dynamic tab, launch trace, wait for exit to render
        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Assembly Name");
        await auto.KeyAsync(Hex1bKey.D8, cts.Token);
        await auto.WaitUntilAsync(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"));
        await auto.EnterAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.Tracer?.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error,
            timeout: TimeSpan.FromSeconds(30));
        await auto.WaitUntilTextAsync("Events");
        // Move focus into the events table to ensure a row is focused
        await auto.DownAsync(cts.Token);
        await auto.UpAsync(cts.Token);
        await auto.WaitUntilAsync(_ => _state!.DynamicEventsFocusedKey is not null);

        // Wait for the teal focus background to appear and verify the category
        // cell color in the SAME snapshot. We cannot capture the snapshot reference
        // because WaitUntilStep disposes it after the predicate returns. Instead,
        // extract the category cell's foreground color inside the predicate.
        var teal = Hex1bColor.FromRgb(0, 200, 180);
        Hex1bColor? categoryFg = null;
        await auto.WaitUntilAsync(s =>
        {
            for (var y = 0; y < s.Height; y++)
            {
                var cell = s.GetCell(1, y);
                if (cell.Background is { } bg
                    && bg.R == teal.R && bg.G == teal.G && bg.B == teal.B)
                {
                    // Found the focused row — find the category text by scanning
                    // for a known category name (JIT, GC, etc.) on this row.
                    var lineText = s.GetTextAt(y, 0, s.Width);
                    foreach (var cat in DynamicAnalysisView.CategoryColors.Keys)
                    {
                        var catName = cat.ToString();
                        var idx = lineText.IndexOf(catName, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            categoryFg = s.GetCell(idx, y).Foreground;
                            return true;
                        }
                    }

                    return true; // focused row found but no known category text
                }
            }
            return false;
        }, description: "teal focus background to appear");

        var focusedKey = _state!.DynamicEventsFocusedKey;
        Assert.NotNull(focusedKey);

        // The focused row's category cell must NOT have any of the known category
        // colors. When focused, it should be black (0,0,0) or null (theme default).
        var knownCategoryColors = DynamicAnalysisView.CategoryColors.Values
            .Select(c => (c.R, c.G, c.B))
            .ToHashSet();

        if (categoryFg is not null)
        {
            var fg = categoryFg.Value;
            Assert.False(knownCategoryColors.Contains((fg.R, fg.G, fg.B)),
                $"Focused row category cell still has category color ({fg.R},{fg.G},{fg.B}) — should be black on focus");

            // WCAG 2.1 AA requires >= 4.5:1 contrast ratio for normal text.
            // Verify the foreground color against the teal focus background.
            var ratio = ContrastRatio(fg.R, fg.G, fg.B, teal.R, teal.G, teal.B);
            Assert.True(ratio >= 4.5,
                $"Focused category cell fails WCAG AA: contrast ratio {ratio:F2}:1 " +
                $"(fg={fg.R},{fg.G},{fg.B} bg={teal.R},{teal.G},{teal.B}), minimum is 4.5:1");
        }

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Computes WCAG 2.1 contrast ratio between two sRGB colors.
    /// </summary>
    private static double ContrastRatio(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var l1 = RelativeLuminance(r1, g1, b1);
        var l2 = RelativeLuminance(r2, g2, b2);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(byte r, byte g, byte b)
    {
        var rs = Linearize(r / 255.0);
        var gs = Linearize(g / 255.0);
        var bs = Linearize(b / 255.0);
        return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs;
    }

    private static double Linearize(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
