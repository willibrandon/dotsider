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
        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Search bar (visible when IlSearchActive is true)
            if (state.IlSearchActive)
            {
                widgets.Add(outer.HStack(row =>
                [
                    row.Text(" Search: ").FixedWidth(9),
                    row.TextBox(state.IlSearchQuery ?? "")
                        .OnTextChanged(e =>
                        {
                            state.IlSearchQuery = e.NewText;
                            state.App.Invalidate();
                        })
                        .Fill()
                ]).FixedHeight(1));
            }

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
                            var disassembly = state.IlDisassembler.FormatDisassembly(method);
                            return disassembly.Split('\n')
                                .Select(line => scroll.Text(line))
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
            bindings.Key(Hex1bKey.OemQuestion).Action(_ =>
            {
                state.IlSearchActive = !state.IlSearchActive;
                if (!state.IlSearchActive) state.IlSearchQuery = null;
                state.App.Invalidate();
            }, "Toggle search");
        })
        .FillWidth().FillHeight();
    }

    private static IEnumerable<TreeItemWidget> BuildMethodTree(TreeContext t, DotsiderState state)
    {
        var searchQuery = state.IlSearchQuery;

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

            yield return t.Item(nsGroup.Key, ns =>
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

            yield return t.Item(typeDef.Name, type =>
                filteredMethods.Select(m =>
                {
                    void SelectMethod()
                    {
                        state.IlSelectedMethod = m;
                        state.App.Invalidate();
                    }

                    return type.Item($"{m.Name}{m.Signature}")
                        .OnClicked(_ => SelectMethod())
                        .OnActivated(_ => SelectMethod());
                })
            );
        }
    }
}
