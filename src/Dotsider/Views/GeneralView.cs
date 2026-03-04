using Dotsider.Analysis;
using Dotsider.Analysis.Models;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the General tab (Tab 1), showing assembly metadata and
/// a dependency table of referenced assemblies with drill-down navigation.
/// </summary>
public static class GeneralView
{
    /// <summary>
    /// Builds the General view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the General tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var analyzer = state.Analyzer;
        var search = state.Search[TabId.General];
        var query = search.Query;

        // Filter assembly refs by search query
        var refs = (IReadOnlyList<AssemblyRefInfo>)analyzer.AssemblyRefs;
        if (!string.IsNullOrEmpty(query))
        {
            refs = refs
                .Where(r => $"{r.Name} {r.Version} {r.Culture} {r.PublicKeyToken}"
                    .Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            search.SetMatchCount(refs.Count);
        }

        // Set up match navigation
        state.NavigateNextMatch = null;
        state.NavigatePrevMatch = null;

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Assembly Info section
            widgets.Add(outer.Border(
                outer.VStack(info =>
                [
                    InfoLine(info, "Assembly Name", analyzer.AssemblyName ?? "(none)"),
                    InfoLine(info, "Version", analyzer.AssemblyVersion ?? "(none)"),
                    InfoLine(info, "Target Framework", analyzer.TargetFramework ?? "(unknown)"),
                    InfoLine(info, "Culture", analyzer.Culture ?? "neutral"),
                    InfoLine(info, "Public Key Token", analyzer.PublicKeyToken ?? "(none)"),
                    info.Text(""),
                    InfoLine(info, "File Size", state.FormatSizeToggleable(analyzer.FileSize)),
                    InfoLine(info, "Architecture", analyzer.Architecture),
                    InfoLine(info, "Last Modified", analyzer.LastModified.ToString("yyyy-MM-dd HH:mm:ss UTC")),
                    InfoLine(info, "Created", analyzer.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss UTC")),
                    InfoLine(info, "Read-Only", analyzer.IsReadOnly ? "Yes" : "No"),
                    InfoLine(info, "Has Metadata", analyzer.HasMetadata ? "Yes" : "No")
                ])
            ).Title(" Assembly Info "));

            // Search bar
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Assembly References table
            widgets.Add(outer.Border(
                outer.Table(refs)
                    .RowKey(r => r.Name)
                    .Header(h =>
                    [
                        h.Cell("Name").Width(SizeHint.Fill),
                        h.Cell("Version").Width(SizeHint.Fixed(18)),
                        h.Cell("Culture").Width(SizeHint.Fixed(10)),
                        h.Cell("Public Key Token").Width(SizeHint.Fixed(20))
                    ])
                    .Row((r, asmRef, rowState) =>
                    [
                        r.Cell(c => HighlightHelper.HighlightCell(c, asmRef.Name, query,
                            !string.IsNullOrEmpty(query))),
                        r.Cell(asmRef.Version),
                        r.Cell(asmRef.Culture),
                        r.Cell(asmRef.PublicKeyToken ?? "")
                    ])
                    .Focus(state.GeneralFocusedDep)
                    .OnFocusChanged(key => state.GeneralFocusedDep = key)
                    .OnRowActivated((_, asmRef) =>
                    {
                        var resolvedPath = AssemblyAnalyzer.ResolveAssemblyPath(
                            state.Analyzer.FilePath, asmRef.Name);
                        if (resolvedPath is not null)
                        {
                            state.PushAssembly(resolvedPath);
                            state.App.Invalidate();
                        }
                    })
                    .Compact()
                    .Empty(e => e.Text("  No assembly references"))
                    .FillHeight()
                    .WithInputBindings(bindings =>
                    {
                        bindings.Key(Hex1bKey.Enter).Action(_ =>
                        {
                            var focusedName = state.GeneralFocusedDep as string
                                ?? analyzer.AssemblyRefs.FirstOrDefault()?.Name;
                            if (focusedName is not null)
                            {
                                var resolvedPath = AssemblyAnalyzer.ResolveAssemblyPath(
                                    state.Analyzer.FilePath, focusedName);
                                if (resolvedPath is not null)
                                {
                                    state.PushAssembly(resolvedPath);
                                    state.App.Invalidate();
                                }
                            }
                        }, "Drill into reference");
                    })
            ).Title($" Assembly References ({refs.Count}) ").Fill());

            return widgets.ToArray();
        })
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Backspace).Action(_ =>
            {
                if (state.PopAssembly())
                {
                    state.App.Invalidate();
                }
            }, "Back");

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

    private static Hex1bWidget InfoLine<T>(WidgetContext<T> ctx, string label, string value) where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.Text($"  {label}: ").FixedWidth(22),
            row.Text(value)
        ]).FixedHeight(1);
    }
}
