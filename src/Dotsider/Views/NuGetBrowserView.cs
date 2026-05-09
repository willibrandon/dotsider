using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
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

        // Seed initial focus to first DLL row and ensure table has focus
        if (state.FileTreeFocusedKey is null && pkg.DllFiles.Count > 0)
        {
            state.FileTreeFocusedKey = pkg.DllFiles[0].FullPath;
            state.App.RequestFocus(node =>
                node.GetType().Name.StartsWith("TableNode"));
        }

        // Build Package Info text for read-only editor
        var infoText = string.Join("\n",
            $"  Package ID:   {pkg.PackageId ?? "(unknown)"}",
            $"  Version:      {pkg.PackageVersion ?? "(unknown)"}",
            $"  Authors:      {pkg.Authors ?? "(unknown)"}",
            $"  Description:  {pkg.Description ?? "(none)"}",
            "",
            $"  Total Files:  {pkg.Files.Count}",
            $"  DLL Files:    {pkg.DllFiles.Count}",
            $"  Total Size:   {DotsiderState.FormatSize(pkg.Files.Sum(f => f.UncompressedSize))}");

        if (state.PackageInfoEditorText != infoText)
        {
            state.PackageInfoEditorText = infoText;
            state.PackageInfoEditorState = new EditorState(new Hex1bDocument(infoText)) { IsReadOnly = true };
        }

        // Adjust word boundaries after double-click (consistent with IL Inspector)
        if (state.PackageInfoEditorState is not null && state.IsBrowsingPackage)
        {
            IlInspectorView.AdjustWordSelectionCursorOneShot(
                state.PackageInfoEditorState,
                ref state.PackageInfoPrevSelectionAnchor,
                ref state.PackageInfoPrevCursorPosition);
        }

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>
            {
                // Package metadata (read-only editor for text selection + yank)
                outer.Border(
                    outer.ThemePanel(t => t
                        .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                        .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                    outer.Editor(state.PackageInfoEditorState!)
                        .ViewRenderer(InfoEditorViewRenderer.Instance)
                        .Decorations(new InfoLabelDecorationProvider())
                        .Decorations(state.PackageInfoYankProvider)
                        .InputBindings(bindings =>
                        {
                            TextObjectHelper.ConfigureReadOnlyEditorBindings(
                                bindings,
                                state.PackageInfoEditorState!,
                                () => state.VimPending,
                                () => state.VimPendingEditor,
                                () => state.VimPendingCursorOffset,
                                () => state.VimPendingTimestamp,
                                (s, e, o) => { state.VimPending = s; state.VimPendingEditor = e; state.VimPendingCursorOffset = o; state.VimPendingTimestamp = DateTime.UtcNow; },
                                state.PerformEditorYank,
                                () => state.App.Invalidate());
                        })
                        .FillWidth().FillHeight())
                ).Title(" Package Info ").FixedHeight(11)
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
                    {
                        var flash = rowState.IsFocused && state.YankFlashRow;
                        var fg = flash ? Hex1bColor.FromRgb(24, 24, 37)
                            : rowState.IsFocused ? Hex1bColor.Black
                            : (Hex1bColor?)null;
                        var bg = flash ? Hex1bColor.FromRgb(126, 201, 216)
                            : rowState.IsFocused ? Hex1bColor.FromRgb(0, 200, 180)
                            : (Hex1bColor?)null;

                        return
                        [
                            r.Cell(c => rowState.IsFocused
                                ? c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg!.Value).Set(GlobalTheme.BackgroundColor, bg!.Value),
                                    HighlightHelper.HighlightCell(c, entry.Name, query, !string.IsNullOrEmpty(query), fg, bg))
                                : HighlightHelper.HighlightCell(c, entry.Name, query, !string.IsNullOrEmpty(query), fg, bg)),
                            r.Cell(c => rowState.IsFocused
                                ? c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg!.Value).Set(GlobalTheme.BackgroundColor, bg!.Value),
                                    HighlightHelper.HighlightCell(c, entry.Directory, query, !string.IsNullOrEmpty(query), fg, bg))
                                : HighlightHelper.HighlightCell(c, entry.Directory, query, !string.IsNullOrEmpty(query), fg, bg)),
                            r.Cell(c => rowState.IsFocused
                                ? c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg!.Value).Set(GlobalTheme.BackgroundColor, bg!.Value),
                                    c.Text(DotsiderState.FormatSize(entry.UncompressedSize)))
                                : c.Text(DotsiderState.FormatSize(entry.UncompressedSize)))
                        ];
                    })
                    .Focus(state.App.FocusedNode is EditorNode ? null : state.FileTreeFocusedKey)
                    .OnFocusChanged(key => state.FileTreeFocusedKey = key)
                    .OnRowActivated((_, entry) =>
                    {
                        try
                        {
                            state.SavedFileTreeFocusedKey = state.FileTreeFocusedKey;
                            var analyzer = pkg.OpenDll(entry);
                            state.SelectedDllState?.Dispose();
                            state.SelectedDllState = new DotsiderState(state.App, analyzer);
                            state.SelectedDllEntry = entry;
                            state.IsBrowsingPackage = false;
                            state.App.RequestFocus(node =>
                                node.GetType().Name.StartsWith("TableNode"));
                            state.App.Invalidate();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to open DLL: {ex.Message}");
                        }
                    })
                    .Compact()
                    .Empty(e => e.Text("  No DLL files in package"))
                    .FillWidth()
                    .FillHeight()
            ).Title($" DLL Files ({pkg.DllFiles.Count}) — Enter to inspect ").Fill());

            return [.. widgets];
        })
        .InputBindings(bindings =>
        {
            // Tab toggles focus between Package Info editor and DLL table
            bindings.Key(Hex1b.Input.Hex1bKey.Tab).Global().Action(_ =>
            {
                state.VimPending = VimMotionState.Idle;
                if (state.App.FocusedNode is EditorNode)
                {
                    state.FileTreeFocusedKey ??=
                        pkg.DllFiles.Count > 0 ? pkg.DllFiles[0].FullPath : null;
                    state.App.RequestFocus(node =>
                        node.GetType().Name.StartsWith("TableNode"));
                    state.App.Invalidate();
                }
                else
                {
                    state.App.RequestFocus(node => node is EditorNode);
                    state.App.Invalidate();
                }
            }, "Toggle focus");
        })
        .Fill();
    }
}
