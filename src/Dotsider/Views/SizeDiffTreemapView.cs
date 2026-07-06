using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the size-diff treemap: rectangle area is the absolute delta — the largest
/// regression is the largest rectangle — and color carries direction, red for bytes gained
/// (added, grown) and green for bytes shed (removed, shrunk). Direction is never carried by
/// color alone: every label leads with a kind glyph and a signed delta. Note this is the
/// inverse of the managed diff tabs' green-added/red-removed — here red means a bigger binary.
/// </summary>
public static class SizeDiffTreemapView
{
    // Direction backgrounds, each paired at draw time with the black-or-white label
    // foreground that clears WCAG AA (>= 4.5:1) against it.
    private static readonly Hex1bColor AddedColor = Hex1bColor.FromRgb(190, 60, 60);
    private static readonly Hex1bColor GrownColor = Hex1bColor.FromRgb(180, 105, 60);
    private static readonly Hex1bColor RemovedColor = Hex1bColor.FromRgb(40, 140, 70);
    private static readonly Hex1bColor ShrunkColor = Hex1bColor.FromRgb(95, 150, 100);
    private static readonly Hex1bColor MixedColor = Hex1bColor.FromRgb(110, 110, 140);
    private static readonly Hex1bColor SelectionBorder = Hex1bColor.FromRgb(255, 255, 255);

    /// <summary>
    /// Builds the size-diff treemap widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The size-diff state.</param>
    /// <returns>The root widget for the Size Map tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, SizeDiffState state)
    {
        // Rebuild the filtered tree when the direction filter changed; drill state points
        // into the old tree, so it resets with it.
        if (state.FilteredRootMode != state.FilterMode)
        {
            state.FilteredRoot = ApplyFilter(state.Diff.Root, state.FilterMode);
            state.FilteredRootMode = state.FilterMode;
            state.TreemapCurrentLevel = null;
            state.TreemapBreadcrumb.Clear();
            state.TreemapSelectedIndex = -1;
            state.TreemapMatchIndex = -1;
        }

        var root = state.FilteredRoot!;
        var currentLevel = state.TreemapCurrentLevel ?? root;
        var search = state.Search[1];
        var query = search.Query;

        var matchingItems = new List<int>();
        if (!string.IsNullOrEmpty(query))
        {
            for (var i = 0; i < currentLevel.Children.Count; i++)
            {
                if (currentLevel.Children[i].Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    matchingItems.Add(i);
            }

            search.SetMatchCount(matchingItems.Count);
        }

        if (state.TreemapMatchIndex >= matchingItems.Count)
            state.TreemapMatchIndex = matchingItems.Count > 0 ? 0 : -1;

        if (state.CurrentTab == 1)
        {
            state.NavigateNextMatch = matchingItems.Count > 0 ? () =>
            {
                state.TreemapMatchIndex = state.TreemapMatchIndex < 0
                    ? 0 : (state.TreemapMatchIndex + 1) % matchingItems.Count;
            }
            : null;
            state.NavigatePrevMatch = matchingItems.Count > 0 ? () =>
            {
                state.TreemapMatchIndex = state.TreemapMatchIndex <= 0
                    ? matchingItems.Count - 1 : state.TreemapMatchIndex - 1;
            }
            : null;
        }

        if (state.WhyContent is not null && state.WhyEditorText != state.WhyContent)
        {
            state.WhyEditorText = state.WhyContent;
            state.WhyEditorState = new EditorState(new Hex1bDocument(state.WhyContent)) { IsReadOnly = true };
        }

        if (state.DisasmContent is not null && state.DisasmEditorText != state.DisasmContent)
        {
            state.DisasmEditorText = state.DisasmContent;
            state.DisasmEditorState = new EditorState(new Hex1bDocument(state.DisasmContent)) { IsReadOnly = true };
        }

        return ctx.ZStack(z =>
        [
            z.VStack(outer =>
            {
                var widgets = new List<Hex1bWidget>
                {
                    outer.HStack(row =>
                    [
                        row.Text($" {BuildBreadcrumb(state, root)} "),
                        row.Text($"| Δ {FormatDelta(currentLevel.Delta)}"
                            + $" | filter: {state.FilterMode}").Fill()
                    ]).FixedHeight(1)
                };

                SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

                widgets.Add(outer.Interactable(ic =>
                    ic.Surface(s =>
                    [
                        s.Layer(surface =>
                        {
                            if (currentLevel.Children.Count == 0)
                            {
                                surface.WriteText(2, 1,
                                    state.FilterMode == SizeDiffFilterMode.All
                                        ? "No size differences between the two builds"
                                        : $"No {state.FilterMode} entries — f cycles the filter",
                                    Hex1bColor.FromRgb(140, 140, 160));
                                return;
                            }

                            var (projected, map) = Project(currentLevel.Children);
                            var rects = TreemapLayout.Layout(projected, 0, 0, surface.Width, surface.Height);
                            state.TreemapHoveredItem = null;
                            state.TreemapHoveredNode = null;
                            DrawTreemap(surface, rects, map, currentLevel, state, s.MouseX, s.MouseY, query);
                        })
                    ]).Fill()
                ).OnClick(e =>
                {
                    SizeDiffNode? drillTarget = null;

                    if (e.Context.MouseX >= 0)
                    {
                        var relX = e.Context.MouseX - e.Node.Bounds.X;
                        var relY = e.Context.MouseY - e.Node.Bounds.Y;
                        var (projected, map) = Project(currentLevel.Children);
                        var rects = TreemapLayout.Layout(
                            projected, 0, 0, e.Node.Bounds.Width, e.Node.Bounds.Height);
                        // Take the last match: later rects paint over earlier ones at shared
                        // boundary cells.
                        foreach (var rect in rects)
                        {
                            var (cx1, cy1, cx2, cy2) = SizeTreemapView.CellBounds(rect);
                            if (relX >= cx1 && relX < cx2 && relY >= cy1 && relY < cy2)
                                drillTarget = map[rect.Node];
                        }
                    }
                    else
                    {
                        if (state.TreemapMatchIndex >= 0 && state.TreemapMatchIndex < matchingItems.Count)
                            drillTarget = currentLevel.Children[matchingItems[state.TreemapMatchIndex]];
                        else if (state.TreemapSelectedIndex >= 0
                            && state.TreemapSelectedIndex < currentLevel.Children.Count)
                            drillTarget = currentLevel.Children[state.TreemapSelectedIndex];
                    }

                    if (drillTarget is { Children.Count: > 0 })
                    {
                        state.TreemapBreadcrumb.Push(currentLevel);
                        state.TreemapCurrentLevel = drillTarget;
                        state.TreemapSelectedIndex = -1;
                        state.TreemapMatchIndex = -1;
                        state.TreemapHoveredNode = null;
                        state.App.Invalidate();
                    }
                }).InputBindings(bindings =>
                {
                    // Esc dismisses popups first, then pops the breadcrumb (search dismissal
                    // is handled by the outer binding, same guard order as SizeTreemapView).
                    if (!search.IsActive && (state.WhyContent is not null || state.DisasmContent is not null))
                    {
                        bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
                        {
                            state.VimPending = VimMotionState.Idle;
                            CloseWhyPopup(state);
                            CloseDisasmPopup(state);
                            state.App.Invalidate();
                        }, "Dismiss popup");
                    }
                    else if (!search.IsActive && state.TreemapBreadcrumb.Count > 0)
                    {
                        bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
                        {
                            state.VimPending = VimMotionState.Idle;
                            state.TreemapCurrentLevel = state.TreemapBreadcrumb.Pop();
                            state.TreemapSelectedIndex = -1;
                            state.App.Invalidate();
                        }, "Go up");
                    }

                    bindings.Key(Hex1bKey.W).Action(_ =>
                    {
                        var target = TargetNode(state, currentLevel, matchingItems);
                        if (target is null) return;
                        ShowWhyChain(state, target);
                        state.App.Invalidate();
                    }, "Why in binary");

                    bindings.Key(Hex1bKey.D).Action(_ =>
                    {
                        var target = TargetNode(state, currentLevel, matchingItems);
                        if (target is null) return;
                        ShowDisassembly(state, target);
                        state.App.Invalidate();
                    }, "Disassemble");

                    bindings.Mouse(MouseButton.Right).Action(_ =>
                    {
                        if (state.TreemapBreadcrumb.Count > 0)
                        {
                            state.TreemapCurrentLevel = state.TreemapBreadcrumb.Pop();
                            state.TreemapSelectedIndex = -1;
                            state.App.Invalidate();
                        }
                    }, "Go up");

                    bindings.Key(Hex1bKey.RightArrow).Action(_ =>
                    {
                        if (currentLevel.Children.Count > 0)
                        {
                            state.TreemapSelectedIndex =
                                (state.TreemapSelectedIndex + 1) % currentLevel.Children.Count;
                            state.App.Invalidate();
                        }
                    }, "Next item");

                    bindings.Key(Hex1bKey.LeftArrow).Action(_ =>
                    {
                        if (currentLevel.Children.Count > 0)
                        {
                            state.TreemapSelectedIndex = state.TreemapSelectedIndex <= 0
                                ? currentLevel.Children.Count - 1
                                : state.TreemapSelectedIndex - 1;
                            state.App.Invalidate();
                        }
                    }, "Previous item");
                }).Fill());

                var detailText = state.TreemapHoveredItem;
                if (detailText is null && state.TreemapSelectedIndex >= 0
                    && state.TreemapSelectedIndex < currentLevel.Children.Count)
                {
                    detailText = DetailText(currentLevel.Children[state.TreemapSelectedIndex]);
                }

                if (detailText is null && state.TreemapMatchIndex >= 0
                    && state.TreemapMatchIndex < matchingItems.Count)
                {
                    detailText = DetailText(currentLevel.Children[matchingItems[state.TreemapMatchIndex]]);
                }

                widgets.Add(outer.Text(detailText ?? "").FixedHeight(1));

                return [.. widgets];
            })
            .InputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
                {
                    if (search.IsActive)
                    {
                        search.Dismiss();
                        state.App.Invalidate();
                    }
                }, "Esc");
            })
            .Fill(),

            BuildPopup(z, state, state.WhyContent, state.WhyEditorState, " Why in binary ",
                () => CloseWhyPopup(state)),
            BuildPopup(z, state, state.DisasmContent, state.DisasmEditorState, " Native disassembly ",
                () => CloseDisasmPopup(state))
        ]).Fill();
    }

    /// <summary>The node the keyboard verbs (w, d) act on: hover, then search match, then selection.</summary>
    private static SizeDiffNode? TargetNode(
        SizeDiffState state, SizeDiffNode currentLevel, List<int> matchingItems)
    {
        var target = state.TreemapHoveredNode;
        if (target is null && state.TreemapMatchIndex >= 0 && state.TreemapMatchIndex < matchingItems.Count)
            target = currentLevel.Children[matchingItems[state.TreemapMatchIndex]];
        if (target is null && state.TreemapSelectedIndex >= 0
            && state.TreemapSelectedIndex < currentLevel.Children.Count)
            target = currentLevel.Children[state.TreemapSelectedIndex];
        return target;
    }

    /// <summary>
    /// Opens or toggles the why-chain popup. Added entries resolve against the right build's
    /// graph, removed entries against the left; changed entries start on the right and a
    /// second press flips sides when the other build has a graph too. The popup header names
    /// the side, so the answer never silently mixes builds.
    /// </summary>
    private static void ShowWhyChain(SizeDiffState state, SizeDiffNode target)
    {
        CloseDisasmPopup(state);

        var canLeft = target.LeftNodeNames.Count > 0 && state.LeftDgml is not null;
        var canRight = target.RightNodeNames.Count > 0 && state.RightDgml is not null;

        bool showLeft;
        if (target.Diff == DiffKind.Removed) showLeft = true;
        else if (!canRight && canLeft) showLeft = true;
        else if (ReferenceEquals(state.WhyTarget, target) && state.WhyContent is not null
            && canLeft && canRight)
            showLeft = !state.WhyShowingLeft; // second press flips sides
        else showLeft = false;

        var dgml = showLeft ? state.LeftDgml : state.RightDgml;
        var names = showLeft ? target.LeftNodeNames : target.RightNodeNames;
        var sideName = showLeft ? state.LeftName : state.RightName;
        var side = showLeft ? "left/baseline" : "right/current";

        string content;
        if (dgml is null)
        {
            content = $"{target.FullPath}\n\nNo DGML dependency graph next to the {side} build.\nPublish with IlcGenerateDgmlFile and keep the\n*.codegen.dgml.xml beside the executable or mstat.";
        }
        else
        {
            var header = $"[{side}: {sideName}]"
                + (canLeft && canRight ? "  (w flips sides)" : "");
            content = header + "\n" + WhyChainFormatter.FormatWhyChains(dgml, target.FullPath, names);
        }

        state.WhyTarget = target;
        state.WhyShowingLeft = showLeft;
        state.WhyContent = content;
    }

    /// <summary>
    /// Opens or cycles the native-disassembly popup. The entry's dependency-graph node names
    /// are the ILC mangled symbol names — unique per signature and instantiation — so the
    /// popup resolves them against a side's native symbols by exact match, never by display
    /// name. Repeated presses cycle through every candidate: an aggregate's symbols, and for
    /// a changed entry both builds' bodies — new build first, then the baseline — so a grown
    /// method's before/after are one key apart. The header states the side and which of how
    /// many is showing.
    /// </summary>
    private static void ShowDisassembly(SizeDiffState state, SizeDiffNode target)
    {
        CloseWhyPopup(state);

        if (target.Kind is not (SizeNodeKind.Method or SizeNodeKind.MethodTable or SizeNodeKind.Function))
        {
            state.DisasmContent = $"{target.FullPath}\n\nNot a code entry — nothing to disassemble.";
            state.DisasmTarget = target;
            return;
        }

        if (state.LeftAnalyzer is null && state.RightAnalyzer is null)
        {
            state.DisasmContent = $"{target.FullPath}\n\nNo binary behind either side — disassembly needs the\npublished executable, not just the .mstat report.";
            state.DisasmTarget = target;
            return;
        }

        var candidates = DisasmCandidates(target)
            .Where(c => (c.UseLeft ? state.LeftAnalyzer : state.RightAnalyzer) is not null)
            .ToList();
        if (candidates.Count == 0)
        {
            state.DisasmContent = $"{target.FullPath}\n\nNo dependency-graph node names recorded for this entry\n(format 1.x mstat); cannot resolve a native symbol without\nguessing by display name.";
            state.DisasmTarget = target;
            return;
        }

        // Repeated presses on the same entry cycle its symbols — and, for changed entries,
        // its sides.
        state.DisasmSymbolIndex = ReferenceEquals(state.DisasmTarget, target) && state.DisasmContent is not null
            ? (state.DisasmSymbolIndex + 1) % candidates.Count
            : 0;
        state.DisasmTarget = target;

        var (nodeName, useLeft) = candidates[state.DisasmSymbolIndex];
        var analyzer = (useLeft ? state.LeftAnalyzer : state.RightAnalyzer)!;
        var sideName = useLeft ? state.LeftName : state.RightName;
        var side = useLeft ? "left/baseline" : "right/current";
        var header = candidates.Count > 1
            ? $"[{side}: {sideName}] symbol {state.DisasmSymbolIndex + 1} of {candidates.Count} — d cycles\n{nodeName}\n"
            : $"[{side}: {sideName}]\n{nodeName}\n";

        var symbol = ResolveSymbol(analyzer, nodeName);
        if (symbol is null)
        {
            state.DisasmContent = header
                + "\nNo native symbol with this exact name — the binary may be\nstripped or the symbol file missing.";
            return;
        }

        var result = NativeDisassembler.DisassembleSymbol(analyzer, symbol);
        state.DisasmContent = result is null
            ? header + "\nThe symbol has no disassemblable bytes."
            : header + "\n" + result.Value.Text;
    }

    /// <summary>
    /// The disassembly candidates of an entry in cycle order: the new build's node names
    /// first, then the baseline's. An added entry has only right-side names, a removed entry
    /// only left-side, and a changed entry both — which is what lets <c>d</c> flip between a
    /// grown method's before and after bodies.
    /// </summary>
    internal static List<(string NodeName, bool UseLeft)> DisasmCandidates(SizeDiffNode target)
    {
        var candidates = new List<(string NodeName, bool UseLeft)>(
            target.RightNodeNames.Count + target.LeftNodeNames.Count);
        foreach (var name in target.RightNodeNames)
            candidates.Add((name, false));
        foreach (var name in target.LeftNodeNames)
            candidates.Add((name, true));
        return candidates;
    }

    /// <summary>
    /// Resolves a dependency-graph node name — the ILC mangled symbol name, unique per
    /// signature and instantiation — to the binary's native symbol by exact match (allowing
    /// the Mach-O underscore prefix). Never matches by display name: overloads share those.
    /// </summary>
    internal static Dotsider.Core.Analysis.Models.NativeSymbol? ResolveSymbol(
        Dotsider.Core.Analysis.AssemblyAnalyzer analyzer, string nodeName) =>
        analyzer.NativeSymbols?.Symbols.FirstOrDefault(s =>
            string.Equals(s.Name, nodeName, StringComparison.Ordinal)
            || string.Equals(s.Name, "_" + nodeName, StringComparison.Ordinal));

    private static void CloseWhyPopup(SizeDiffState state)
    {
        state.WhyContent = null;
        state.WhyEditorText = null;
        state.WhyEditorState = null;
        state.WhyTarget = null;
    }

    private static void CloseDisasmPopup(SizeDiffState state)
    {
        state.DisasmContent = null;
        state.DisasmEditorText = null;
        state.DisasmEditorState = null;
        state.DisasmTarget = null;
        state.DisasmSymbolIndex = 0;
    }

    private static BackdropWidget? BuildPopup(
        WidgetContext<ZStackWidget> z, SizeDiffState state,
        string? content, EditorState? editorState, string title, Action close)
    {
        if (content is null || editorState is null) return null;
        return z.Backdrop(
            z.Border(
                z.ThemePanel(t => t
                    .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                    .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                z.Editor(editorState)
                    .ViewRenderer(InfoEditorViewRenderer.Instance)
                    .Decorations(new InfoLabelDecorationProvider())
                    .InputBindings(bindings =>
                    {
                        TextObjectHelper.ConfigureReadOnlyEditorBindings(
                            bindings,
                            editorState,
                            () => state.VimPending,
                            () => state.VimPendingEditor,
                            () => state.VimPendingCursorOffset,
                            () => state.VimPendingTimestamp,
                            (s, e, o) =>
                            {
                                state.VimPending = s;
                                state.VimPendingEditor = e;
                                state.VimPendingCursorOffset = o;
                                state.VimPendingTimestamp = DateTime.UtcNow;
                            },
                            state.PerformEditorYank,
                            () => state.App.Invalidate());
                    })
                    .FillWidth().FillHeight())
            ).Title(title).FixedWidth(100).FixedHeight(24)
        ).OnClickAway(() =>
        {
            close();
            state.App.Invalidate();
        });
    }

    /// <summary>
    /// Prunes the delta tree to the entries a direction filter keeps, recomputing interior
    /// sums so a namespace shows only its filtered mass.
    /// </summary>
    internal static SizeDiffNode ApplyFilter(SizeDiffNode root, SizeDiffFilterMode mode)
    {
        if (mode == SizeDiffFilterMode.All) return root;
        return FilterNode(root, mode) ?? root with
        {
            Children = [], Diff = DiffKind.Unchanged,
            LeftSize = 0, RightSize = 0, Delta = 0,
            LeftEntryCount = 0, RightEntryCount = 0,
        };
    }

    private static SizeDiffNode? FilterNode(SizeDiffNode node, SizeDiffFilterMode mode)
    {
        if (node.Children.Count == 0)
        {
            var keep = mode switch
            {
                SizeDiffFilterMode.Added => node.Diff == DiffKind.Added,
                SizeDiffFilterMode.Removed => node.Diff == DiffKind.Removed,
                SizeDiffFilterMode.Grown => node.Diff == DiffKind.Changed && node.Delta > 0,
                SizeDiffFilterMode.Shrunk => node.Diff == DiffKind.Changed && node.Delta < 0,
                _ => true,
            };
            return keep ? node : null;
        }

        var children = node.Children
            .Select(c => FilterNode(c, mode))
            .Where(c => c is not null)
            .Select(c => c!)
            .ToList();
        if (children.Count == 0) return null;

        // Recompute the direction from the surviving children: a subtree that was mixed in
        // the full tree can be one-sided in the filtered view, and its tile must say so.
        var diff =
            children.All(c => c.Diff == DiffKind.Added) ? DiffKind.Added
            : children.All(c => c.Diff == DiffKind.Removed) ? DiffKind.Removed
            : DiffKind.Changed;

        return node with
        {
            Children = children,
            Diff = diff,
            LeftSize = children.Sum(c => c.LeftSize),
            RightSize = children.Sum(c => c.RightSize),
            Delta = children.Sum(c => c.Delta),
            LeftEntryCount = children.Sum(c => c.LeftEntryCount),
            RightEntryCount = children.Sum(c => c.RightEntryCount),
        };
    }

    /// <summary>
    /// The treemap area weight of a node: its absolute delta, or for an interior whose
    /// positive and negative children cancel, the sum of its children's weights — churn is
    /// mass, even when it nets to zero.
    /// </summary>
    internal static long Weight(SizeDiffNode node) =>
        node.Children.Count == 0 ? Math.Abs(node.Delta) : node.Children.Sum(Weight);

    /// <summary>
    /// Projects diff nodes into throwaway <see cref="SizeNode"/>s sized by weight so
    /// <see cref="TreemapLayout"/> lays them out unchanged, with a reference-keyed map back —
    /// the layout sorts internally, so identity, not order, carries the association.
    /// </summary>
    private static (List<SizeNode> Projected, Dictionary<SizeNode, SizeDiffNode> Map) Project(
        IReadOnlyList<SizeDiffNode> children)
    {
        var projected = new List<SizeNode>(children.Count);
        var map = new Dictionary<SizeNode, SizeDiffNode>(children.Count, ReferenceEqualityComparer.Instance);
        foreach (var child in children)
        {
            var weight = Weight(child);
            if (weight <= 0) continue;
            var proxy = new SizeNode(child.Name, child.FullPath, weight, child.Kind, []);
            projected.Add(proxy);
            map[proxy] = child;
        }

        return (projected, map);
    }

    private static string BuildBreadcrumb(SizeDiffState state, SizeDiffNode root)
    {
        var parts = new List<string>();
        foreach (var node in state.TreemapBreadcrumb.Reverse())
            parts.Add(node.Name);
        parts.Add((state.TreemapCurrentLevel ?? root).Name);
        return string.Join(" > ", parts);
    }

    /// <summary>Formats a signed byte delta: <c>+12.3 KB</c>, <c>-4.1 KB</c>, <c>±0 B</c>.</summary>
    internal static string FormatDelta(long delta) => delta switch
    {
        > 0 => $"+{DotsiderState.FormatSize(delta)}",
        < 0 => $"-{DotsiderState.FormatSize(-delta)}",
        _ => "±0 B",
    };

    private static string DetailText(SizeDiffNode node)
    {
        var entries = Math.Max(node.LeftEntryCount, node.RightEntryCount);
        var suffix = node.Children.Count > 0
            ? $" ({node.Children.Count} children)"
            : entries > 1 ? $" ({entries} entries)" : "";
        return $" {node.FullPath}: {DotsiderState.FormatSize(node.LeftSize)} → "
            + $"{DotsiderState.FormatSize(node.RightSize)} (Δ{FormatDelta(node.Delta)}){suffix}";
    }

    private static (Hex1bColor Background, char Glyph) StyleOf(SizeDiffNode node) => node.Diff switch
    {
        DiffKind.Added => (AddedColor, '+'),
        DiffKind.Removed => (RemovedColor, '−'),
        DiffKind.Changed when node.Delta > 0 => (GrownColor, 'Δ'),
        DiffKind.Changed when node.Delta < 0 => (ShrunkColor, 'Δ'),
        _ => (MixedColor, 'Δ'),
    };

    /// <summary>
    /// Picks black or white for a label, whichever contrasts more against the background —
    /// with the palette above both directions clear WCAG AA (4.5:1).
    /// </summary>
    internal static Hex1bColor LabelForeground(Hex1bColor background)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        var luminance = 0.2126 * Channel(background.R)
            + 0.7152 * Channel(background.G)
            + 0.0722 * Channel(background.B);
        var contrastWhite = 1.05 / (luminance + 0.05);
        var contrastBlack = (luminance + 0.05) / 0.05;
        return contrastWhite >= contrastBlack ? Hex1bColor.FromRgb(255, 255, 255) : Hex1bColor.Black;
    }

    private static void DrawTreemap(
        Surface surface, IReadOnlyList<TreemapRect> rects,
        Dictionary<SizeNode, SizeDiffNode> map, SizeDiffNode currentLevel,
        SizeDiffState state, int mouseX, int mouseY, string? query)
    {
        var hasQuery = !string.IsNullOrEmpty(query);
        var selected = state.TreemapSelectedIndex >= 0
            && state.TreemapSelectedIndex < currentLevel.Children.Count
            ? currentLevel.Children[state.TreemapSelectedIndex]
            : null;

        foreach (var rect in rects)
        {
            var node = map[rect.Node];
            var (color, glyph) = StyleOf(node);
            var isMatch = hasQuery && node.Name.Contains(query!, StringComparison.OrdinalIgnoreCase);
            if (hasQuery && !isMatch) color = HighlightHelper.DimColor;
            var isSelected = ReferenceEquals(node, selected);

            var (x1, y1, x2, y2) = SizeTreemapView.CellBounds(rect);
            if (x2 <= x1 || y2 <= y1) continue;

            for (var y = y1; y < y2 && y < surface.Height; y++)
                for (var x = x1; x < x2 && x < surface.Width; x++)
                    surface.WriteChar(x, y, ' ', color, color);

            var borderColor = isSelected
                ? SelectionBorder
                : Hex1bColor.FromRgb(
                    (byte)(color.R * 40 / 100),
                    (byte)(color.G * 40 / 100),
                    (byte)(color.B * 40 / 100));

            for (var x = x1; x < x2 && x < surface.Width; x++)
            {
                if (y1 < surface.Height) surface.WriteChar(x, y1, '▁', borderColor, color);
            }

            for (var y = y1; y < y2 && y < surface.Height; y++)
            {
                if (x1 < surface.Width) surface.WriteChar(x1, y, '▕', borderColor, color);
            }

            if (isSelected)
            {
                for (var x = x1; x < x2 && x < surface.Width; x++)
                {
                    if (y2 - 1 < surface.Height) surface.WriteChar(x, y2 - 1, '▔', borderColor, color);
                }

                for (var y = y1; y < y2 && y < surface.Height; y++)
                {
                    if (x2 - 1 < surface.Width) surface.WriteChar(x2 - 1, y, '▏', borderColor, color);
                }
            }

            var cellW = x2 - x1;
            var cellH = y2 - y1;
            if (cellW > 3 && cellH > 0)
            {
                var fg = LabelForeground(color);
                var label = $"{glyph} {node.Name}";
                if (label.Length > cellW - 2) label = label[..(cellW - 4)] + "..";
                surface.WriteText(x1 + 1, y1, label, fg, color);

                if (cellH > 1)
                {
                    var entries = Math.Max(node.LeftEntryCount, node.RightEntryCount);
                    var deltaLabel = FormatDelta(node.Delta)
                        + (node.Children.Count == 0 && entries > 1 ? $" ({entries} entries)" : "");
                    if (deltaLabel.Length > cellW - 2) deltaLabel = FormatDelta(node.Delta);
                    if (deltaLabel.Length <= cellW - 2)
                        surface.WriteText(x1 + 1, y1 + 1, deltaLabel, fg, color);
                }
            }

            if (mouseX >= x1 && mouseX < x2 && mouseY >= y1 && mouseY < y2)
            {
                state.TreemapHoveredItem = DetailText(node);
                state.TreemapHoveredNode = node;
            }
        }
    }
}
