using Dotsider.Analysis.Models;
using Hex1b;
using Hex1b.Input;
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
                            return disassembly.Split('\n')
                                .Select(line => string.IsNullOrEmpty(searchQuery)
                                    ? scroll.Text(IlColorizer.ColorizeLine(line))
                                    : HighlightHelper.HighlightText(scroll, line, searchQuery))
                                .ToArray();
                        }

                        return [scroll.Text("  Select a method to view IL disassembly")];
                    })
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
        })
        .FillWidth().FillHeight();
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

            yield return t.Item(
                HighlightHelper.HighlightSubstring(nsGroup.Key, searchQuery), ns =>
                BuildTypeItems(ns, nsTypes, methodsByType, searchQuery, state)
            ).Expanded();
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

            yield return t.Item(
                HighlightHelper.HighlightSubstring(typeDef.Name, searchQuery), type =>
                filteredMethods.Select(m =>
                {
                    void SelectMethod()
                    {
                        state.IlSelectedMethod = m;
                        state.App.Invalidate();
                    }

                    return type.Item(
                            HighlightHelper.HighlightSubstring($"{m.Name}{m.Signature}", searchQuery))
                        .OnClicked(_ => SelectMethod())
                        .OnActivated(_ => SelectMethod());
                })
            );
        }
    }
}
