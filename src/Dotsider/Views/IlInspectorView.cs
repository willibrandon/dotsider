using Dotsider.Analysis.Models;
using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the IL Inspector tab (Tab 3), displaying a namespace/type/method
/// hierarchy tree on the left and the selected method's IL disassembly on the right.
/// </summary>
public static class IlInspectorView
{
    /// <summary>
    /// Builds the IL Inspector view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context from the parent tab panel.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the IL Inspector tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var search = state.Search[TabId.IlInspector];

        // Scroll restore state machine:
        //   2 → focus the interactable anchor and capture viewport size, decrement to 1
        //   1 → EnsureFocusedVisible adjusts Offset during layout, decrement to 0
        //   0 → done
        var scrollRestorePhase = state.IlScrollRestoreFrames;
        if (state.IlScrollRestoreFrames > 0)
            state.IlScrollRestoreFrames--;

        if (scrollRestorePhase == 2)
        {
            // The predicate visits ScrollPanelNode before its children (InteractableNode),
            // so we capture ViewportSize as a side effect. OnScroll never fires for this
            // panel because EnsureFocusedVisible bypasses SetOffset/ScrollAction.
            state.App.RequestFocus(node =>
            {
                if (node is ScrollPanelNode sp)
                    state.IlDisassemblyViewportSize = sp.ViewportSize;
                return node is InteractableNode;
            });
        }
        else if (scrollRestorePhase == 1)
        {
            // Force another frame so tree focus runs after scroll is restored
            state.App.Invalidate();
        }

        // Focus the tree after scroll restore completes
        if (state.IlNeedsTreeFocus && scrollRestorePhase == 0)
        {
            state.IlNeedsTreeFocus = false;
            state.App.RequestFocus(node => node is TreeNode);
        }

        // Set up match navigation
        state.NavigateNextMatch = null;
        state.NavigatePrevMatch = null;

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Search bar (shared helper)
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Main content: HSplitter with tree on left, disassembly on right
            widgets.Add(outer.HSplitter(
                // Left pane: Namespace > Type > Method tree
                left =>
                [
                    left.Tree(t => BuildMethodTree(t, state))
                        .FillHeight()
                ],
                // Right pane: IL disassembly in a vertical scroll panel
                right =>
                [
                    right.VScrollPanel(scroll =>
                    {
                        if (state.IlSelectedMethod is { } method)
                        {
                            var searchQuery = state.Search[TabId.IlInspector].Query;
                            var disassembly = state.IlDisassembler.FormatDisassembly(method);
                            var lines = disassembly.Split('\n');

                            return BuildDisassemblyWidgets(
                                scroll, lines, searchQuery, scrollRestorePhase, state);
                        }

                        return [scroll.Text("  Select a method to view IL disassembly")];
                    })
                    .OnScroll(e =>
                    {
                        // Don't overwrite the saved offset during scroll restore —
                        // the scroll panel starts at 0 when recreated after a tab switch,
                        // and clobbering the saved value would defeat the anchor mechanism.
                        if (state.IlScrollRestoreFrames <= 0)
                            state.IlDisassemblyScrollOffset = e.Offset;
                        state.IlDisassemblyViewportSize = e.ViewportSize;
                    })
                    .FillHeight()
                ],
                leftWidth: 35).FillWidth().FillHeight());

            return widgets.ToArray();
        })
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
            {
                if (search.IsActive)
                {
                    search.Dismiss();
                    state.App.Invalidate();
                }
            }, "Esc");

            bindings.Key(Hex1bKey.PageDown).Action(_ =>
            {
                if (state.IlSelectedMethod is null) return;
                state.IlDisassemblyScrollOffset += 20;
                state.IlScrollRestoreFrames = 2;
                state.App.Invalidate();
            }, "Page Down");

            bindings.Key(Hex1bKey.PageUp).Action(_ =>
            {
                if (state.IlSelectedMethod is null) return;
                state.IlDisassemblyScrollOffset = Math.Max(0, state.IlDisassemblyScrollOffset - 20);
                state.IlScrollRestoreFrames = 2;
                state.App.Invalidate();
            }, "Page Up");
        })
        .FillWidth().FillHeight();
    }

    /// <summary>
    /// Builds the disassembly line widgets, optionally wrapping a viewport-sized
    /// span in an Interactable anchor for scroll position restoration.
    /// </summary>
    private static Hex1bWidget[] BuildDisassemblyWidgets<T>(
        WidgetContext<T> ctx,
        string[] lines,
        string? searchQuery,
        int scrollRestorePhase,
        DotsiderState state) where T : Hex1bWidget
    {
        // When not restoring scroll, just build plain text widgets
        if (scrollRestorePhase <= 0)
        {
            return lines
                .Select(line => MakeLineWidget(ctx, line, searchQuery))
                .ToArray();
        }

        // Calculate the anchor span: a viewport-sized range starting at the target offset.
        // When EnsureFocusedVisible sees a focused child spanning [target..target+viewport],
        // it adjusts Offset to exactly `target` regardless of scroll direction.
        // Use a minimum of 1 so the anchor is still created even if OnScroll hasn't
        // fired yet to populate the viewport size (e.g. first PageDown after selecting a method).
        var viewportSize = Math.Max(state.IlDisassemblyViewportSize, 1);
        var anchorStart = Math.Clamp(state.IlDisassemblyScrollOffset, 0, Math.Max(0, lines.Length - 1));
        var anchorEnd = Math.Min(anchorStart + viewportSize, lines.Length);

        if (anchorEnd <= anchorStart)
        {
            return lines
                .Select(line => MakeLineWidget(ctx, line, searchQuery))
                .ToArray();
        }

        var widgets = new Hex1bWidget[lines.Length - (anchorEnd - anchorStart) + 1];
        var wi = 0;

        // Lines before anchor
        for (var i = 0; i < anchorStart; i++)
            widgets[wi++] = MakeLineWidget(ctx, lines[i], searchQuery);

        // Anchor: wrap viewport-sized span in Interactable for EnsureFocusedVisible
        widgets[wi++] = ctx.Interactable(ic =>
        {
            var anchorWidgets = new Hex1bWidget[anchorEnd - anchorStart];
            for (var j = 0; j < anchorWidgets.Length; j++)
                anchorWidgets[j] = MakeLineWidget(ic, lines[anchorStart + j], searchQuery);
            return anchorWidgets;
        });

        // Lines after anchor
        for (var i = anchorEnd; i < lines.Length; i++)
            widgets[wi++] = MakeLineWidget(ctx, lines[i], searchQuery);

        return widgets;
    }

    private static Hex1bWidget MakeLineWidget<T>(
        WidgetContext<T> ctx, string line, string? searchQuery) where T : Hex1bWidget
    {
        return string.IsNullOrEmpty(searchQuery)
            ? ctx.Text(IlColorizer.ColorizeLine(line))
            : HighlightHelper.HighlightText(ctx, line, searchQuery);
    }

    private static IEnumerable<TreeItemWidget> BuildMethodTree(TreeContext t, DotsiderState state)
    {
        var searchQuery = state.Search[TabId.IlInspector].Query;

        // Group methods by declaring type
        var methodsByType = state.Analyzer.MethodDefs
            .GroupBy(m => m.DeclaringType)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group types by namespace
        var typesByNamespace = state.Analyzer.TypeDefs
            .GroupBy(td => string.IsNullOrEmpty(td.Namespace) ? "(global)" : td.Namespace)
            .OrderBy(g => g.Key);

        foreach (var nsGroup in typesByNamespace)
        {
            var nsTypes = nsGroup.ToList();

            // Apply search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                nsTypes = nsTypes.Where(td =>
                    td.FullName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                    (methodsByType.TryGetValue(td.FullName, out var methods) &&
                     methods.Any(m => m.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))))
                    .ToList();

                if (nsTypes.Count == 0) continue;
            }

            var nsKey = $"ns:{nsGroup.Key}";
            yield return t.Item(
                HighlightHelper.HighlightSubstring(nsGroup.Key, searchQuery), ns =>
                BuildTypeItems(ns, nsTypes, methodsByType, searchQuery, state)
            )
            .Expanded(GetTreeExpansionState(state, nsKey, defaultExpanded: true))
            .OnExpanded(_ => state.IlTreeExpansionState[nsKey] = true)
            .OnCollapsed(_ => state.IlTreeExpansionState[nsKey] = false);
        }
    }

    private static IEnumerable<TreeItemWidget> BuildTypeItems(
        TreeContext t,
        List<TypeDefInfo> types,
        Dictionary<string, List<MethodDefInfo>> methodsByType,
        string? searchQuery,
        DotsiderState state)
    {
        foreach (var typeDef in types)
        {
            if (!methodsByType.TryGetValue(typeDef.FullName, out var methods))
                continue;

            var filteredMethods = methods;
            if (!string.IsNullOrEmpty(searchQuery))
            {
                filteredMethods = methods
                    .Where(m => m.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                                typeDef.FullName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (filteredMethods.Count == 0) continue;
            }

            var typeKey = $"type:{typeDef.FullName}";
            yield return t.Item(
                HighlightHelper.HighlightSubstring(typeDef.Name, searchQuery), type =>
                filteredMethods.Select(m =>
                {
                    void SelectMethod()
                    {
                        state.IlSelectedMethod = m;
                        state.IlDisassemblyScrollOffset = 0;
                        state.IlScrollRestoreFrames = 0;
                        state.App.Invalidate();
                    }

                    return type.Item(
                            HighlightHelper.HighlightSubstring($"{m.Name}{m.Signature}", searchQuery))
                        .OnClicked(_ => SelectMethod())
                        .OnActivated(_ => SelectMethod());
                })
            )
            .Expanded(GetTreeExpansionState(state, typeKey, defaultExpanded: false))
            .OnExpanded(_ => state.IlTreeExpansionState[typeKey] = true)
            .OnCollapsed(_ => state.IlTreeExpansionState[typeKey] = false);
        }
    }

    private static bool GetTreeExpansionState(DotsiderState state, string key, bool defaultExpanded) =>
        state.IlTreeExpansionState.TryGetValue(key, out var expanded) ? expanded : defaultExpanded;
}
