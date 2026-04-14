using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Reproduces #134: the squarified treemap layout passes the original totalArea through
/// every recursive call instead of the remaining items' total, leaving a visible gap on
/// the right side of the Size Map tab.
/// </summary>
[Collection("SampleAssemblies")]
public class SizeMapViewTests(SampleAssemblyFixture samples) : IDisposable
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
                _state ??= new DotsiderState(_hex1bApp!, samples.RichLibraryDll)
                {
                    CurrentTab = TabId.SizeMap
                };
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
    /// Verifies that the treemap fills the entire rendering area with no uncovered cells.
    /// The renderer writes an explicit background color to every cell covered by a
    /// treemap rectangle. Cells in the gap region retain the default (null) background,
    /// making them detectable by scanning the screen buffer.
    /// See: https://github.com/willibrandon/dotsider/issues/134
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SizeMap_TreemapFillsEntireArea_NoGapOnRight()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Total:");

        var uncoveredCells = 0;
        var totalTreemapCells = 0;

        await auto.WaitUntilAsync(s =>
        {
            // Locate the treemap area: starts after the breadcrumb ("Total:"),
            // ends before the status/key-hints line ("Tabs").
            var treemapStart = -1;
            var treemapEnd = -1;

            for (var y = 0; y < s.Height; y++)
            {
                var line = s.GetLine(y);
                if (line.Contains("Total:") && treemapStart < 0)
                    treemapStart = y + 1;
                if (treemapStart >= 0 && y > treemapStart && line.Contains("Tabs"))
                {
                    // The detail bar sits between the treemap surface and the
                    // key-hints line, so the treemap area ends two rows above.
                    treemapEnd = y - 1;
                    break;
                }
            }

            if (treemapStart < 0 || treemapEnd <= treemapStart)
                return false;

            uncoveredCells = 0;
            totalTreemapCells = (treemapEnd - treemapStart) * s.Width;

            for (var y = treemapStart; y < treemapEnd; y++)
            {
                for (var x = 0; x < s.Width; x++)
                {
                    var cell = s.GetCell(x, y);
                    if (cell.Background is not { })
                        uncoveredCells++;
                }
            }

            return true;
        }, description: "treemap area rendered with measurable bounds");

        // With the bug, roughly 26% of the treemap area (~780 cells) is uncovered.
        // A correctly tiling treemap has zero uncovered cells.
        Assert.True(totalTreemapCells > 0, "Could not locate treemap area");
        Assert.Equal(0, uncoveredCells);

        cts.Cancel();
        await runTask;
    }

    /// <summary>
    /// Isolates the integer rasterization bug from the layout algorithm.
    /// Constructs floating-point rects that tile perfectly, then verifies
    /// CellBounds produces integer bounds covering every cell in [0, width).
    /// Without Ceiling for end coordinates, truncation leaves the rightmost
    /// cell uncovered when the boundary falls on a non-integer.
    /// See: https://github.com/willibrandon/dotsider/issues/134
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SizeMap_CellBounds_TilesFullViewport()
    {
        const int width = 120;
        const int height = 30;

        // Two rects that tile perfectly in floating-point at a non-integer boundary
        var node = new SizeNode("test", "test", 100, SizeNodeKind.Type, []);
        var rects = new[]
        {
            new TreemapRect(0, 0, 68.7, height, node),
            new TreemapRect(68.7, 0, width - 68.7, height, node),
        };

        // Verify every integer cell is covered by at least one rect's CellBounds
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var covered = false;
                foreach (var rect in rects)
                {
                    var (x1, y1, x2, y2) = SizeTreemapView.CellBounds(rect);
                    if (x >= x1 && x < x2 && y >= y1 && y < y2)
                    {
                        covered = true;
                        break;
                    }
                }

                Assert.True(covered, $"Cell ({x}, {y}) is not covered by any CellBounds rect");
            }
        }
    }

    /// <summary>
    /// When CellBounds expands a fractional boundary with Ceiling, two adjacent
    /// rects both claim the shared cell. DrawTreemap and hover let the later rect
    /// win (last-painted). The click handler must do the same: iterate all rects
    /// and keep the last match. This test verifies that a cell on the overlapping
    /// boundary resolves to the second (later) rect, not the first.
    /// See: https://github.com/willibrandon/dotsider/issues/134
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SizeMap_CellBounds_OverlappingBoundary_ResolvesToLastRect()
    {
        // Split at 68.7 → rect A covers [0, 69), rect B covers [68, 120).
        // Cell x=68 is claimed by both. The last match (rect B) must win.
        var nodeA = new SizeNode("A", "A", 70, SizeNodeKind.Namespace, []);
        var nodeB = new SizeNode("B", "B", 30, SizeNodeKind.Namespace, []);
        var rects = new[]
        {
            new TreemapRect(0, 0, 68.7, 30, nodeA),
            new TreemapRect(68.7, 0, 120 - 68.7, 30, nodeB),
        };

        // Cell (68, 15) is inside both CellBounds
        var (ax1, _, ax2, _) = SizeTreemapView.CellBounds(rects[0]);
        var (bx1, _, bx2, _) = SizeTreemapView.CellBounds(rects[1]);
        Assert.True(68 >= ax1 && 68 < ax2, "Cell 68 should be inside rect A's bounds");
        Assert.True(68 >= bx1 && 68 < bx2, "Cell 68 should be inside rect B's bounds");

        // Simulate the click handler: iterate all, keep last match (same as draw order)
        SizeNode? resolved = null;
        foreach (var rect in rects)
        {
            var (x1, y1, x2, y2) = SizeTreemapView.CellBounds(rect);
            if (68 >= x1 && 68 < x2 && 15 >= y1 && 15 < y2)
                resolved = rect.Node;
        }

        Assert.Same(nodeB, resolved);
    }

    /// <summary>
    /// Verifies that the rightmost painted treemap cell is hoverable — the same
    /// CellBounds are used for both drawing and hover detection, so a painted cell
    /// must always resolve to a node on hover.
    /// See: https://github.com/willibrandon/dotsider/issues/134
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SizeMap_RightmostPaintedCell_IsHoverable()
    {
        var (terminal, app) = CreateDotsiderApp();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Total:");

        // Find the rightmost painted cell on a middle row of the treemap
        var rightmostX = -1;
        var targetY = -1;

        await auto.WaitUntilAsync(s =>
        {
            var treemapStart = -1;
            var treemapEnd = -1;

            for (var y = 0; y < s.Height; y++)
            {
                var line = s.GetLine(y);
                if (line.Contains("Total:") && treemapStart < 0)
                    treemapStart = y + 1;
                if (treemapStart >= 0 && y > treemapStart && line.Contains("Tabs"))
                {
                    // The detail bar sits between the treemap surface and the
                    // key-hints line, so the treemap area ends two rows above.
                    treemapEnd = y - 1;
                    break;
                }
            }

            if (treemapStart < 0 || treemapEnd <= treemapStart)
                return false;

            // Pick the middle row of the treemap area
            targetY = (treemapStart + treemapEnd) / 2;
            rightmostX = -1;

            for (var x = s.Width - 1; x >= 0; x--)
            {
                var cell = s.GetCell(x, targetY);
                if (cell.Background is { })
                {
                    rightmostX = x;
                    break;
                }
            }

            return rightmostX >= 0;
        }, description: "rightmost painted cell found");

        Assert.True(rightmostX >= 0, "No painted cells found on middle row");

        // Click the rightmost painted cell — the click handler uses the same
        // CellBounds as drawing, so if the cell is painted it must be clickable.
        // The treemap also updates hover state during each render, so after the
        // click triggers a re-render the hovered item should be set.
        await auto.ClickAtAsync(rightmostX, targetY, ct: cts.Token);

        await auto.WaitUntilAsync(_ => _state!.TreemapHoveredItem is not null,
            description: "hovered item resolved for rightmost painted cell");

        Assert.NotNull(_state!.TreemapHoveredItem);

        cts.Cancel();
        await runTask;
    }

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
