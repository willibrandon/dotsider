using Dotsider.Analysis;
using Dotsider.Analysis.Models;
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

        return ctx.VStack(outer =>
        [
            // Package metadata
            outer.Border(
                outer.VStack(info =>
                [
                    InfoLine(info, "Package ID", pkg.PackageId ?? "(unknown)"),
                    InfoLine(info, "Version", pkg.PackageVersion ?? "(unknown)"),
                    InfoLine(info, "Authors", pkg.Authors ?? "(unknown)"),
                    InfoLine(info, "Description", pkg.Description ?? "(none)"),
                    info.Text(""),
                    InfoLine(info, "Total Files", pkg.Files.Count.ToString()),
                    InfoLine(info, "DLL Files", pkg.DllFiles.Count.ToString()),
                    InfoLine(info, "Total Size", DotsiderState.FormatSize(pkg.Files.Sum(f => f.UncompressedSize)))
                ])
            ).Title(" Package Info "),

            // DLL selector table
            outer.Border(
                outer.Table(pkg.DllFiles)
                    .RowKey(r => r.FullPath)
                    .Header(h =>
                    [
                        h.Cell("Name").Width(SizeHint.Fixed(30)),
                        h.Cell("Directory").Width(SizeHint.Fill),
                        h.Cell("Size").Width(SizeHint.Fixed(12))
                    ])
                    .Row((r, entry, rowState) =>
                    [
                        r.Cell(entry.Name),
                        r.Cell(entry.Directory),
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
            ).Title($" DLL Files ({pkg.DllFiles.Count}) — Enter to inspect ").Fill()
        ]).Fill();
    }

    private static Hex1bWidget InfoLine<T>(WidgetContext<T> ctx, string label, string value) where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.Text($"  {label}: ").FixedWidth(18),
            row.Text(value)
        ]).FixedHeight(1);
    }
}
