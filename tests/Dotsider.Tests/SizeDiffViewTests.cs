using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the size-diff TUI — <see cref="SizeDiffApp"/> and its treemap/summary views —
/// on a headless 120×30 terminal over the real V1/V2 mstat pair, plus direct tests of the
/// filter, weight, why-chain, and symbol-resolution logic against the same real data.
/// </summary>
[Collection("SampleAssemblies")]
public class SizeDiffViewTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private SizeDiffState? _state;

    private (MstatSource V1, MstatSource V2) ResolvePair(bool binaries = false)
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        Assert.SkipWhen(samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");

        var leftPath = binaries ? samples.NativeAotConsoleExe : samples.NativeAotConsoleMstat;
        var rightPath = binaries ? samples.NativeAotConsoleV2Exe : samples.NativeAotConsoleV2Mstat;
        if (binaries)
        {
            Assert.SkipWhen(leftPath is null, "V1 AOT binary was not produced");
            Assert.SkipWhen(rightPath is null, "V2 AOT binary was not produced");
        }

        var left = MstatLocator.Resolve(leftPath!);
        var right = MstatLocator.Resolve(rightPath!);
        Assert.NotNull(left);
        Assert.NotNull(right);
        return (left, right);
    }

    private (Hex1bTerminal Terminal, Hex1bApp App) CreateSizeDiffApp(MstatSource left, MstatSource right)
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        SizeDiffApp? sizeDiffApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new SizeDiffState(_hex1bApp!, left, right);
                sizeDiffApp ??= new SizeDiffApp(_state);
                return Task.FromResult<Hex1bWidget>(sizeDiffApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp);
    }

    /// <summary>
    /// Verifies the size-diff app opens for an mstat pair with exactly the Summary and Size
    /// Map tabs — never the managed diff's empty Types/Methods/References tables — with the
    /// treemap tab active and the signed total delta in the title bar.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SizeDiffApp_MstatPair_ShowsSizeTabsOnly()
    {
        var (v1, v2) = ResolvePair();
        var (terminal, app) = CreateSizeDiffApp(v1, v2);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("dotsider size diff");
        await auto.WaitUntilTextAsync("Size Map");

        await auto.WaitUntilAsync(s =>
        {
            var text = ScreenText(s);
            return text.Contains("Summary")
                && text.Contains("Δ +")
                && text.Contains("Filter: All")
                && !text.Contains("Types (")
                && !text.Contains("References (");
        }, description: "size-diff chrome without managed tabs");

        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies the delta treemap tiles its full area with no uncovered cells — the same
    /// coverage guarantee the single-build Size Map holds (#134), under the |Δ| weighting.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SizeDiffTreemap_FillsAreaNoGaps()
    {
        var (v1, v2) = ResolvePair();
        var (terminal, app) = CreateSizeDiffApp(v1, v2);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("filter:");

        var uncoveredCells = 0;
        var totalTreemapCells = 0;

        await auto.WaitUntilAsync(s =>
        {
            var treemapStart = -1;
            var treemapEnd = -1;
            for (var y = 0; y < s.Height; y++)
            {
                var line = s.GetLine(y);
                if (line.Contains("filter:") && treemapStart < 0)
                    treemapStart = y + 1;
                if (treemapStart >= 0 && y > treemapStart && line.Contains("Tabs"))
                {
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
                    if (s.GetCell(x, y).Background is not { })
                        uncoveredCells++;
                }
            }

            return true;
        }, description: "delta treemap rendered with measurable bounds");

        Assert.True(totalTreemapCells > 0, "Could not locate treemap area");
        Assert.Equal(0, uncoveredCells);

        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies drill-down and breadcrumb restore: Enter on a selected subtree descends (the
    /// breadcrumb gains a level), Esc pops back to the root level.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SizeDiffTreemap_DrillEnter_EscRestoresLevel()
    {
        var (v1, v2) = ResolvePair();
        var (terminal, app) = CreateSizeDiffApp(v1, v2);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("filter:");

        await auto.KeyAsync(Hex1bKey.RightArrow, ct: cts.Token);
        await auto.WaitUntilAsync(
            _ => _state is { TreemapSelectedIndex: >= 0 }, description: "an item is selected");

        await auto.KeyAsync(Hex1bKey.Enter, ct: cts.Token);
        await auto.WaitUntilAsync(
            _ => _state is { TreemapBreadcrumb.Count: 1, TreemapCurrentLevel: not null },
            description: "drilled one level down");
        await auto.WaitUntilTextAsync("Total > ");

        await auto.KeyAsync(Hex1bKey.Escape, ct: cts.Token);
        await auto.WaitUntilAsync(
            _ => _state is { TreemapBreadcrumb.Count: 0 }, description: "breadcrumb popped");

        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies f cycles all five direction filters and the drill state resets with each
    /// switch — the filtered tree is a different tree.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SizeDiffTreemap_FilterKeyCyclesFiveModes()
    {
        var (v1, v2) = ResolvePair();
        var (terminal, app) = CreateSizeDiffApp(v1, v2);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("Filter: All");

        foreach (var expected in new[] { "Filter: Added", "Filter: Removed", "Filter: Grown", "Filter: Shrunk", "Filter: All" })
        {
            await auto.KeyAsync(Hex1bKey.F, ct: cts.Token);
            await auto.WaitUntilTextAsync(expected);
        }

        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies the w binding opens the why popup for the targeted node.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SizeDiffTreemap_WhyKey_OpensPopup()
    {
        var (v1, v2) = ResolvePair();
        var (terminal, app) = CreateSizeDiffApp(v1, v2);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("filter:");

        await auto.KeyAsync(Hex1bKey.RightArrow, ct: cts.Token);
        await auto.WaitUntilAsync(
            _ => _state is { TreemapSelectedIndex: >= 0 }, description: "an item is selected");
        await auto.KeyAsync(Hex1bKey.W, ct: cts.Token);
        Hex1b.Automation.Hex1bTerminalSnapshot? popupSnapshot = null;
        await auto.WaitUntilAsync(s =>
        {
            if (!ScreenText(s).Contains("Why in binary", StringComparison.Ordinal))
                return false;
            popupSnapshot = s;
            return true;
        }, description: "why popup rendered");
        Assert.NotNull(_state!.WhyContent);
        AssertPopupSurfaceReadable(popupSnapshot!, "Why in binary", "[right/current:");
        var selectedBeforeDismiss = _state.TreemapSelectedIndex;
        var visibleCount = (_state.TreemapCurrentLevel ?? _state.FilteredRoot)!.Children.Count;
        Assert.True(visibleCount > 1, "The fixture must expose more than one treemap item.");
        var expectedSelection = (selectedBeforeDismiss + 1) % visibleCount;

        await auto.KeyAsync(Hex1bKey.Escape, ct: cts.Token);
        await auto.WaitUntilAsync(
            _ => _state is { WhyContent: null },
            description: "why popup dismissed");
        await auto.KeyAsync(Hex1bKey.RightArrow, ct: cts.Token);
        await auto.WaitUntilAsync(
            _ => _state!.TreemapSelectedIndex == expectedSelection,
            description: "treemap focus restored after popup dismiss");

        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies the d binding opens the disassembly popup, and that a bare-mstat pair states
    /// honestly that there is no binary to disassemble.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task SizeDiffTreemap_DisasmKey_BareMstatPair_ExplainsNoBinary()
    {
        var (v1, v2) = ResolvePair();
        var (terminal, app) = CreateSizeDiffApp(v1, v2);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await auto.WaitUntilAlternateScreenAsync();
        await auto.WaitUntilTextAsync("filter:");

        await auto.KeyAsync(Hex1bKey.RightArrow, ct: cts.Token);
        await auto.WaitUntilAsync(
            _ => _state is { TreemapSelectedIndex: >= 0 }, description: "an item is selected");
        await auto.KeyAsync(Hex1bKey.D, ct: cts.Token);
        Hex1b.Automation.Hex1bTerminalSnapshot? popupSnapshot = null;
        await auto.WaitUntilAsync(s =>
        {
            if (_state is not { DisasmContent: not null }
                || !ScreenText(s).Contains("Native disassembly", StringComparison.Ordinal))
            {
                return false;
            }

            popupSnapshot = s;
            return true;
        }, description: "disasm popup rendered");
        AssertPopupSurfaceReadable(popupSnapshot!, "Native disassembly");

        cts.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Direct logic tests (no terminal) over the same real diff ---

    private MstatDiffResult DiffV1V2()
    {
        var (v1, v2) = ResolvePair();
        return MstatDiffer.Compare(v1.Data, v2.Data);
    }

    /// <summary>
    /// Verifies the direction filters keep exactly their entries: Removed keeps the deleted
    /// accessor and drops the added namespace; Added does the reverse; interior sums are
    /// recomputed from the surviving children.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ApplyFilter_DirectionsPartitionEntries()
    {
        var diff = DiffV1V2();

        static bool ContainsLeaf(SizeDiffNode node, Func<SizeDiffNode, bool> predicate) =>
            node.Children.Count == 0
                ? predicate(node)
                : node.Children.Any(c => ContainsLeaf(c, predicate));

        static void AssertRecomputedNodeNames(SizeDiffNode node)
        {
            if (node.Children.Count == 0) return;

            Assert.Equal(
                node.Children.SelectMany(c => c.LeftNodeNames).Distinct(StringComparer.Ordinal),
                node.LeftNodeNames);
            Assert.Equal(
                node.Children.SelectMany(c => c.RightNodeNames).Distinct(StringComparer.Ordinal),
                node.RightNodeNames);
            foreach (var child in node.Children)
                AssertRecomputedNodeNames(child);
        }

        // The V2 build pulls new BCL surface in, so common accessor names like get_Name()
        // exist as *added* entries elsewhere — the fixture's own members are identified by
        // their full paths, never by bare display names.
        const string GetNamePath = "NativeAotConsole/Greeter::get_Name()";
        const string GreetStringPath = "NativeAotConsole/Greeter::Greet(string)";

        AssertRecomputedNodeNames(diff.Root);

        var removed = SizeDiffTreemapView.ApplyFilter(diff.Root, SizeDiffFilterMode.Removed);
        Assert.True(ContainsLeaf(removed, n => n.FullPath == GetNamePath));
        Assert.False(ContainsLeaf(removed, n => n.FullPath.Contains("Telemetry")));
        Assert.All(removed.Children, AssertRecomputedSums);
        Assert.All(removed.Children, n => AssertAllDirections(n, DiffKind.Removed));
        AssertRecomputedNodeNames(removed);

        var added = SizeDiffTreemapView.ApplyFilter(diff.Root, SizeDiffFilterMode.Added);
        Assert.False(ContainsLeaf(added, n => n.FullPath == GetNamePath));
        Assert.True(ContainsLeaf(added, n => n.FullPath.Contains("Telemetry")));
        Assert.All(added.Children, n => AssertAllDirections(n, DiffKind.Added));
        AssertRecomputedNodeNames(added);

        var grown = SizeDiffTreemapView.ApplyFilter(diff.Root, SizeDiffFilterMode.Grown);
        Assert.True(ContainsLeaf(grown, n => n.FullPath == GreetStringPath));
        Assert.False(ContainsLeaf(grown, n => n.FullPath == GetNamePath));
        AssertRecomputedNodeNames(grown);

        static void AssertRecomputedSums(SizeDiffNode node)
        {
            if (node.Children.Count == 0) return;
            Assert.Equal(node.Children.Sum(c => c.Delta), node.Delta);
            foreach (var child in node.Children)
                AssertRecomputedSums(child);
        }

        // A one-sided filter must leave a one-sided tree: an interior that was mixed in the
        // full diff is re-labeled from its surviving children, so no filtered tile can claim
        // to be grown/shrunk when everything visible beneath it is added or removed.
        static void AssertAllDirections(SizeDiffNode node, DiffKind expected)
        {
            Assert.Equal(expected, node.Diff);
            foreach (var child in node.Children)
                AssertAllDirections(child, expected);
        }
    }

    /// <summary>
    /// Verifies the disassembly candidates of a changed entry cover both builds — the new
    /// build's body first, then the baseline's — while one-sided entries offer only their
    /// own side.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void DisasmCandidates_ChangedEntry_CoversBothSides()
    {
        var diff = DiffV1V2();

        static SizeDiffNode? FindLeaf(SizeDiffNode node, string fullPath)
        {
            if (node.FullPath == fullPath && node.Children.Count == 0) return node;
            foreach (var child in node.Children)
            {
                if (FindLeaf(child, fullPath) is { } found) return found;
            }

            return null;
        }

        var grown = FindLeaf(diff.Root, "NativeAotConsole/Greeter::Greet(string)");
        Assert.NotNull(grown);
        var grownCandidates = SizeDiffTreemapView.DisasmCandidates(grown);
        Assert.Contains(grownCandidates, c => !c.UseLeft);
        Assert.Contains(grownCandidates, c => c.UseLeft);
        Assert.False(grownCandidates[0].UseLeft); // new build first

        var removed = FindLeaf(diff.Root, "NativeAotConsole/Greeter::get_Name()");
        Assert.NotNull(removed);
        var removedCandidates = SizeDiffTreemapView.DisasmCandidates(removed);
        Assert.NotEmpty(removedCandidates);
        Assert.All(removedCandidates, c => Assert.True(c.UseLeft));
    }

    /// <summary>
    /// Verifies the treemap weight: a leaf weighs its absolute delta, and an interior whose
    /// children cancel still weighs their churn — mass never disappears because it netted to
    /// zero.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Weight_InteriorChurn_NeverCancels()
    {
        var diff = DiffV1V2();

        static void AssertWeights(SizeDiffNode node)
        {
            var weight = SizeDiffTreemapView.Weight(node);
            if (node.Children.Count == 0)
            {
                Assert.Equal(Math.Abs(node.Delta), weight);
            }
            else
            {
                Assert.Equal(node.Children.Sum(SizeDiffTreemapView.Weight), weight);
                Assert.True(weight >= Math.Abs(node.Delta));
                foreach (var child in node.Children)
                    AssertWeights(child);
            }
        }

        AssertWeights(diff.Root);
    }

    /// <summary>
    /// Verifies the treemap label palette clears WCAG AA: for each direction background, the
    /// chosen black-or-white foreground contrasts at 4.5:1 or better.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void LabelForeground_PaletteClearsWcagAa()
    {
        var backgrounds = new[]
        {
            Hex1bColor.FromRgb(190, 60, 60),    // added
            Hex1bColor.FromRgb(180, 105, 60),   // grown
            Hex1bColor.FromRgb(40, 140, 70),    // removed
            Hex1bColor.FromRgb(95, 150, 100),   // shrunk
            Hex1bColor.FromRgb(110, 110, 140),  // mixed interior
        };

        foreach (var background in backgrounds)
        {
            var foreground = SizeDiffTreemapView.LabelForeground(background);
            Assert.True(
                ContrastRatio(foreground, background) >= 4.5,
                $"contrast below 4.5:1 on rgb({background.R},{background.G},{background.B})");
        }

        static double ContrastRatio(Hex1bColor a, Hex1bColor b)
        {
            var la = Luminance(a);
            var lb = Luminance(b);
            var (lighter, darker) = la >= lb ? (la, lb) : (lb, la);
            return (lighter + 0.05) / (darker + 0.05);
        }

        static double Luminance(Hex1bColor c)
        {
            static double Channel(byte v)
            {
                var s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        }
    }

    /// <summary>
    /// Verifies the popup palette clears WCAG AA on its owned dark surface.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void PopupPalette_ClearsWcagAa()
    {
        Assert.True(ContrastRatio(
            SizeDiffTreemapView.PopupForeground,
            SizeDiffTreemapView.PopupPanelBackground) >= 4.5);
        Assert.True(ContrastRatio(
            SizeDiffTreemapView.PopupLabelForeground,
            SizeDiffTreemapView.PopupPanelBackground) >= 4.5);
        Assert.True(ContrastRatio(
            SizeDiffTreemapView.PopupBorderColor,
            SizeDiffTreemapView.PopupPanelBackground) >= 4.5);
    }

    /// <summary>
    /// Verifies the two-sided why-chain data: an added Telemetry method's node names resolve
    /// to chains in the V2 graph, and the removed accessor's node names resolve in the V1
    /// graph — each side answers only for its own build.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void WhyChains_ResolvePerSide()
    {
        Assert.SkipWhen(samples.NativeAotConsoleDgml is null, "V1 DGML sidecar was not produced");
        Assert.SkipWhen(samples.NativeAotConsoleV2Dgml is null, "V2 DGML sidecar was not produced");
        var diff = DiffV1V2();
        var leftDgml = DgmlReader.Read(samples.NativeAotConsoleDgml!);
        var rightDgml = DgmlReader.Read(samples.NativeAotConsoleV2Dgml!);
        Assert.NotNull(leftDgml);
        Assert.NotNull(rightDgml);

        var added = diff.Contributors.First(c =>
            c.Diff == DiffKind.Added && c.Namespace == "NativeAotConsole.Telemetry"
            && c.Kind == SizeNodeKind.Method && c.RightNodeNames.Count > 0);
        var addedChain = WhyChainFormatter.FormatWhyChains(rightDgml, added.FullPath, added.RightNodeNames);
        Assert.Contains("Kept by", addedChain);

        var removed = diff.Contributors.First(c =>
            c.Name == "get_Name()" && c.AssemblyName == "NativeAotConsole");
        Assert.NotEmpty(removed.LeftNodeNames);
        Assert.Empty(removed.RightNodeNames);
        var removedChain = WhyChainFormatter.FormatWhyChains(leftDgml, removed.FullPath, removed.LeftNodeNames);
        Assert.Contains("Kept by", removedChain);
    }

    /// <summary>
    /// Verifies an aggregate treemap tile can explain its growth directly by rolling up the
    /// child dependency-graph node names, so users do not need to drill to a leaf first.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void WhyChains_AggregateNodeRollsUpDescendantNames()
    {
        Assert.SkipWhen(samples.NativeAotConsoleV2Dgml is null, "V2 DGML sidecar was not produced");
        var diff = DiffV1V2();
        var rightDgml = DgmlReader.Read(samples.NativeAotConsoleV2Dgml!);
        Assert.NotNull(rightDgml);

        static SizeDiffNode? FindNode(SizeDiffNode node, string fullPath)
        {
            if (node.FullPath == fullPath) return node;
            foreach (var child in node.Children)
            {
                if (FindNode(child, fullPath) is { } found) return found;
            }

            return null;
        }

        var aggregate = FindNode(diff.Root, "System.Private.TypeLoader/Internal.Runtime.TypeLoader");
        Assert.NotNull(aggregate);
        Assert.NotEmpty(aggregate.RightNodeNames);
        Assert.True(aggregate.Children.Count > 0);

        var chain = WhyChainFormatter.FormatWhyChains(
            rightDgml, aggregate.FullPath, aggregate.RightNodeNames);

        Assert.Contains("aggregated nodes", chain);
        Assert.Contains("root first", chain);
    }

    /// <summary>
    /// Verifies disassembly resolution goes through mangled node names, which disambiguate
    /// overloads for free: the grown Greet(string) and the untouched Greet(int) carry
    /// different node names, each resolving to its own native symbol in the V2 binary.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ResolveSymbol_OverloadsResolveToDistinctSymbols()
    {
        var (_, v2Source) = ResolvePair(binaries: true);
        Assert.SkipWhen(v2Source.BinaryPath is null, "V2 AOT binary was not produced");

        using var analyzer = new AssemblyAnalyzer(v2Source.BinaryPath!);
        Assert.SkipWhen(
            analyzer.NativeSymbols is not { Symbols.Count: > 0 },
            "native symbols were not produced for the V2 binary");

        var index = MstatSizeIndex.Create(v2Source.Data);
        var greetString = index.Entries.Single(e =>
            e.Section == MstatSectionKind.Method && e.AssemblyName == "NativeAotConsole"
            && e.LeafName == "Greet(string)");
        var greetInt = index.Entries.Single(e =>
            e.Section == MstatSectionKind.Method && e.AssemblyName == "NativeAotConsole"
            && e.LeafName == "Greet(int)");
        Assert.NotEmpty(greetString.NodeNames);
        Assert.NotEmpty(greetInt.NodeNames);
        Assert.NotEqual(greetString.NodeNames[0], greetInt.NodeNames[0]);

        var stringSymbol = SizeDiffTreemapView.ResolveSymbol(analyzer, greetString.NodeNames[0]);
        var intSymbol = SizeDiffTreemapView.ResolveSymbol(analyzer, greetInt.NodeNames[0]);
        Assert.SkipWhen(
            stringSymbol is null || intSymbol is null,
            "mstat node names not present in the symbol table on this toolchain");
        Assert.NotEqual(stringSymbol!.VirtualAddress, intSymbol!.VirtualAddress);
        Assert.EndsWith(greetString.NodeNames[0], stringSymbol.Name);
    }

    private static string ScreenText(Hex1b.Automation.Hex1bTerminalSnapshot s)
    {
        var lines = new List<string>(s.Height);
        for (var y = 0; y < s.Height; y++)
            lines.Add(s.GetLine(y));
        return string.Join('\n', lines);
    }

    private static void AssertPopupSurfaceReadable(
        Hex1b.Automation.Hex1bTerminalSnapshot snapshot,
        string title,
        string? contentNeedle = null)
    {
        var titleY = FindLine(snapshot, title);
        Assert.True(titleY >= 0, $"Could not locate popup title '{title}'.");

        var panelCells = 0;
        var sampleY = Math.Min(snapshot.Height - 1, titleY + 1);
        for (var x = 0; x < snapshot.Width; x++)
        {
            if (ColorsEqual(snapshot.GetCell(x, sampleY).Background,
                    SizeDiffTreemapView.PopupPanelBackground))
            {
                panelCells++;
            }
        }

        Assert.True(panelCells >= 90, $"Popup row only had {panelCells} cells with the panel background.");

        int contentX;
        int contentY;
        if (contentNeedle is not null)
        {
            contentY = FindLine(snapshot, contentNeedle);
            Assert.True(contentY >= 0, $"Could not locate popup content '{contentNeedle}'.");
            contentX = snapshot.GetLine(contentY).IndexOf(contentNeedle, StringComparison.Ordinal);
            Assert.True(contentX >= 0);
        }
        else
        {
            (contentX, contentY) = FindPopupTextCell(snapshot, titleY);
            Assert.True(contentX >= 0 && contentY >= 0, "Could not locate popup text.");
        }

        var cell = snapshot.GetCell(contentX, contentY);
        Assert.NotNull(cell.Foreground);
        Assert.NotNull(cell.Background);
        AssertColorEquals(SizeDiffTreemapView.PopupPanelBackground, cell.Background.Value);
        Assert.True(ContrastRatio(cell.Foreground.Value, cell.Background.Value) >= 4.5);
    }

    private static int FindLine(Hex1b.Automation.Hex1bTerminalSnapshot snapshot, string text)
    {
        for (var y = 0; y < snapshot.Height; y++)
        {
            if (snapshot.GetLine(y).Contains(text, StringComparison.Ordinal))
                return y;
        }

        return -1;
    }

    private static (int X, int Y) FindPopupTextCell(
        Hex1b.Automation.Hex1bTerminalSnapshot snapshot, int titleY)
    {
        for (var y = titleY + 1; y < snapshot.Height; y++)
        {
            for (var x = 0; x < snapshot.Width; x++)
            {
                var cell = snapshot.GetCell(x, y);
                if (!ColorsEqual(cell.Background, SizeDiffTreemapView.PopupPanelBackground)
                    || string.IsNullOrWhiteSpace(cell.Character)
                    || IsBorderGlyph(cell.Character))
                {
                    continue;
                }

                return (x, y);
            }
        }

        return (-1, -1);
    }

    private static bool IsBorderGlyph(string value) =>
        value is "┌" or "┐" or "└" or "┘" or "─" or "│";

    private static bool ColorsEqual(Hex1bColor? actual, Hex1bColor expected) =>
        actual is { } color
        && color.R == expected.R
        && color.G == expected.G
        && color.B == expected.B;

    private static void AssertColorEquals(Hex1bColor expected, Hex1bColor actual)
    {
        Assert.Equal(expected.R, actual.R);
        Assert.Equal(expected.G, actual.G);
        Assert.Equal(expected.B, actual.B);
    }

    private static double ContrastRatio(Hex1bColor a, Hex1bColor b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (lighter, darker) = la >= lb ? (la, lb) : (lb, la);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Hex1bColor c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
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
