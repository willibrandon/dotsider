using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the NuGet package browser view showing package metadata and a DLL selector table.
/// </summary>
public static class NuGetBrowserView
{
    /// <summary>
    /// Builds the NuGet browser view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The NuGet mode application state.</param>
    /// <returns>The root widget for the package browser.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, NuGetState state)
    {
        var pkg = state.Package;
        var search = state.BrowserSearch;
        var query = search.Query;

        // Filter DLL list by search query
        var dlls = (IReadOnlyList<NuGetFileEntry>)pkg.DllFiles;
        if (!string.IsNullOrEmpty(query))
        {
            dlls = [.. dlls.Where(d =>
                d.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.Directory.Contains(query, StringComparison.OrdinalIgnoreCase))];
            search.SetMatchCount(dlls.Count);
        }

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>
            {
                // Package metadata
                outer.Border(
                outer.VStack(info =>
                [
                    InfoLine(info, "Package ID", pkg.PackageId ?? "(unknown)", query),
                    InfoLine(info, "Version", pkg.PackageVersion ?? "(unknown)", query),
                    InfoLine(info, "Authors", pkg.Authors ?? "(unknown)", query),
                    InfoLine(info, "Description", pkg.Description ?? "(none)", query),
                    info.Text(""),
                    InfoLine(info, "Total Files", pkg.Files.Count.ToString(), query),
                    InfoLine(info, "DLL Files", pkg.DllFiles.Count.ToString(), query),
                    InfoLine(info, "Total Size", DotsiderState.FormatSize(pkg.Files.Sum(f => f.UncompressedSize)), query)
                ])
            ).Title(" Package Info ")
            };

            // Search bar
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // DLL selector table
            widgets.Add(outer.Border(
                outer.Table(dlls)
                    .RowKey(r => r.FullPath)
                    .Header(h =>
                    [
                        h.Cell("Name").Width(SizeHint.Fixed(30)),
                        h.Cell("Directory").Width(SizeHint.Fill),
                        h.Cell("Size").Width(SizeHint.Fixed(12))
                    ])
                    .Row((r, entry, rowState) =>
                    [
                        r.Cell(c => HighlightHelper.HighlightCell(c, entry.Name, query, !string.IsNullOrEmpty(query))),
                        r.Cell(c => HighlightHelper.HighlightCell(c, entry.Directory, query, !string.IsNullOrEmpty(query))),
                        r.Cell(DotsiderState.FormatSize(entry.UncompressedSize))
                    ])
                    .Focus(state.FileTreeFocusedKey)
                    .OnFocusChanged(key => state.FileTreeFocusedKey = key)
                    .OnRowActivated((_, entry) =>
                    {
                        try
                        {
                            var analyzer = pkg.OpenDll(entry);
                            state.SelectedDllState?.Dispose();
                            state.SelectedDllState = new DotsiderState(state.App, analyzer);
                            state.SelectedDllEntry = entry;
                            state.IsBrowsingPackage = false;
                            state.App.Invalidate();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to open DLL: {ex.Message}");
                        }
                    })
                    .Compact()
                    .Empty(e => e.Text("  No DLL files in package"))
                    .FillHeight()
            ).Title($" DLL Files ({pkg.DllFiles.Count}) — Enter to inspect ").Fill());

            return [.. widgets];
        }).Fill();
    }

    private static HStackWidget InfoLine<T>(WidgetContext<T> ctx, string label, string value, string? query) where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.Text($"  {label}: ").FixedWidth(18),
            HighlightHelper.HighlightText(row, value, query)
        ]).FixedHeight(1);
    }
}
