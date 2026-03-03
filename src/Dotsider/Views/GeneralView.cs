using Dotsider.Analysis;
using Dotsider.Analysis.Models;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
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

        return ctx.VStack(outer =>
        [
            // Assembly Info section
            outer.Border(
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
            ).Title(" Assembly Info "),

            // Assembly References table
            outer.Border(
                outer.Table((IReadOnlyList<AssemblyRefInfo>)analyzer.AssemblyRefs)
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
                        r.Cell(asmRef.Name),
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
            ).Title($" Assembly References ({analyzer.AssemblyRefs.Count}) ").Fill()
        ])
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Backspace).Action(_ =>
            {
                if (state.PopAssembly())
                {
                    state.App.Invalidate();
                }
            }, "Back");
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
