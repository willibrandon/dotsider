using Dotsider.Core.Analysis.Models;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Strings tab (Tab 4), showing string entries extracted from the assembly
/// across four sub-tabs: User Strings (#US), Metadata Strings (#Strings), Raw Binary,
/// and Raw UTF-16.
/// </summary>
public static class StringsView
{
    private static readonly Hex1bColor AddressColor = Hex1bColor.FromRgb(100, 100, 130);
    [ThreadStatic] private static bool s_yankFlash;
    private static readonly string[] SourceTabs =
        ["User Strings (#US)", "Metadata (#Strings)", "Raw Binary", "Raw (UTF-16)", "Frozen (AOT)"];

    /// <summary>
    /// Builds the Strings view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context from the parent tab panel.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the Strings tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        s_yankFlash = state.YankFlashRow;
        var search = state.Search[TabId.Strings];
        var activeStrings = state.GetActiveStrings();
        var query = search.Query;

        if (state.CurrentTab == TabId.Strings)
        {
            // Set match count when search is active
            if (!string.IsNullOrEmpty(query))
                search.SetMatchCount(activeStrings.Count);

            // Set up match navigation — cycle through filtered table rows
            if (activeStrings.Count > 0 && !string.IsNullOrEmpty(query))
            {
                state.NavigateNextMatch = () =>
                {
                    var idx = FindFocusedIndex(activeStrings, state.StringsFocusedKey);
                    idx = (idx + 1) % activeStrings.Count;
                    state.StringsFocusedKey = RowKey(activeStrings[idx]);
                };
                state.NavigatePrevMatch = () =>
                {
                    var idx = FindFocusedIndex(activeStrings, state.StringsFocusedKey);
                    idx = idx <= 0 ? activeStrings.Count - 1 : idx - 1;
                    state.StringsFocusedKey = RowKey(activeStrings[idx]);
                };
            }
            else
            {
                state.NavigateNextMatch = null;
                state.NavigatePrevMatch = null;
            }

            // Ensure the first row is focused when arriving at the tab
            if (state.StringsFocusedKey is null && activeStrings.Count > 0)
            {
                state.StringsFocusedKey = RowKey(activeStrings[0]);
            }
        }

        // Build detail popup editor state when content changes
        if (state.StringsDetailContent is not null && state.StringsDetailEditorText != state.StringsDetailContent)
        {
            state.StringsDetailEditorText = state.StringsDetailContent;
            var escapedContent = TerminalText.EscapeMultiline(state.StringsDetailContent);
            var detailText = $"  Length: {state.StringsDetailContent.Length}\n\n  {escapedContent.Replace("\n", "\n  ")}";
            state.StringsDetailEditorState = new EditorState(new Hex1bDocument(detailText)) { IsReadOnly = true };
        }

        return ctx.ZStack(z =>
        [
            // Layer 0: Main content
            z.VStack(outer =>
            {
                var widgets = new List<Hex1bWidget>();

                // Search bar (shared helper)
                SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

                // Sub-tab selector with strings table as content
                Hex1bWidget stringsTabs = outer.TabPanel(tp =>
                    [.. SourceTabs.Select((name, i) =>
                        tp.Tab(name, t => [BuildStringsTable(t, state, activeStrings, query)])
                            .Selected(state.StringsSourceTab == i)
                    )]
                )
                .OnSelectionChanged(e =>
                {
                    if (state.StringsDetailContent is not null)
                    {
                        state.App.Invalidate();
                        return;
                    }

                    state.StringsSourceTab = e.SelectedIndex;
                    search.Reset();
                    state.StringsFocusedKey = null;
                    state.RequestContentFocus();
                    state.App.Invalidate();
                })
                .Compact()
                .Fill();

                // Always wrap in a ThemePanel so the widget tree stays stable when
                // the detail popup toggles — avoids re-measure that resets scroll.
                // When the popup is open, suppress the teal tab highlight so it
                // doesn't bleed through the transparent backdrop.
                var popupOpen = state.StringsDetailContent is not null;
                stringsTabs = outer.ThemePanel(t => popupOpen
                    ? t.Set(TabBarTheme.SelectedForegroundColor, Hex1bColor.FromRgb(140, 140, 160))
                         .Set(TabBarTheme.SelectedBackgroundColor, Hex1bColor.Default)
                    : t, stringsTabs)
                .Fill();

                widgets.Add(stringsTabs);

                // Status line
                var statusParts = new List<string> { $"{activeStrings.Count} strings" };
                if (state.StringsSourceTab is StringsSubTabId.RawBinary or StringsSubTabId.RawBinaryUtf16)
                {
                    statusParts.Add($"Min length: {state.StringsMinLength}");
                }

                var skipped = state.StringsSourceTab switch
                {
                    StringsSubTabId.UserStrings => state.MetadataStringExtractor.SkippedUserStringCount,
                    StringsSubTabId.Metadata => state.MetadataStringExtractor.SkippedMetadataStringCount,
                    _ => 0
                };

                if (skipped > 0)
                {
                    statusParts.Add($"{skipped} malformed skipped");
                }

                widgets.Add(outer.Text($" {string.Join(" | ", statusParts)}").FixedHeight(1));

                return [.. widgets];
            })
            .InputBindings(bindings =>
            {
                var isSearchEditing = search.IsActive && !search.IsConfirmed;

                // Left/Right arrows to switch sub-tabs. Detail popups behave modally,
                // so their editor/click-away surface owns navigation while open.
                if (!isSearchEditing && state.StringsDetailContent is null)
                {
                    if (state.App.FocusedNode is not EditorNode)
                    {
                        bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ =>
                        {
                            state.VimPending = VimMotionState.Idle;
                            if (state.StringsSourceTab > 0)
                            {
                                state.StringsSourceTab--;
                                search.Reset();
                                state.StringsFocusedKey = null;
                                state.RequestContentFocus();
                                state.App.Invalidate();
                            }
                        }, "Previous sub-tab");

                        bindings.Key(Hex1bKey.RightArrow).Global().Action(_ =>
                        {
                            state.VimPending = VimMotionState.Idle;
                            if (state.StringsSourceTab < StringsSubTabId.Count - 1)
                            {
                                state.StringsSourceTab++;
                            search.Reset();
                            state.StringsFocusedKey = null;
                            state.RequestContentFocus();
                            state.App.Invalidate();
                        }
                    }, "Next sub-tab");
                    }
                }

                bindings.Key(Hex1bKey.OemPlus).Action(_ =>
                {
                    state.StringsMinLength++;
                    state.CachedRawStrings = null;
                    state.CachedRawUtf16Strings = null;
                    state.App.Invalidate();
                }, "Increase min length");

                bindings.Key(Hex1bKey.Add).Action(_ =>
                {
                    state.StringsMinLength++;
                    state.CachedRawStrings = null;
                    state.CachedRawUtf16Strings = null;
                    state.App.Invalidate();
                }, "Increase min length");

                bindings.Key(Hex1bKey.OemMinus).Action(_ =>
                {
                    if (state.StringsMinLength > 1)
                    {
                        state.StringsMinLength--;
                        state.CachedRawStrings = null;
                        state.CachedRawUtf16Strings = null;
                        state.App.Invalidate();
                    }
                }, "Decrease min length");

                bindings.Key(Hex1bKey.Subtract).Action(_ =>
                {
                    if (state.StringsMinLength > 1)
                    {
                        state.StringsMinLength--;
                        state.CachedRawStrings = null;
                        state.CachedRawUtf16Strings = null;
                        state.App.Invalidate();
                    }
                }, "Decrease min length");

                // Detail popup dismiss — only register when search is not active
                // to avoid conflicting with DotsiderApp's global "Clear search" binding
                if (!search.IsActive && state.StringsDetailContent is not null)
                {
                    bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
                    {
                        state.VimPending = VimMotionState.Idle;
                        state.StringsDetailContent = null;
                        state.RequestContentFocus();
                        state.App.Invalidate();
                    }, "Dismiss detail");
                }
            })
            .Fill(),

            // Layer 1: String detail popup (read-only editor for selection + yank)
            state.StringsDetailContent is not null && state.StringsDetailEditorState is not null
                ? z.Backdrop(
                    z.Align(Alignment.Center,
                        z.VStack(outer =>
                        [
                            outer.Border(
                                outer.ThemePanel(t => t
                                    .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                                    .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                                outer.Editor(state.StringsDetailEditorState)
                                    .ViewRenderer(InfoEditorViewRenderer.Instance)
                                    .Decorations(new StringsDetailDecorationProvider())
                                    .Decorations(state.StringsDetailYankProvider)
                                    .InputBindings(bindings =>
                                    {
                                        TextObjectHelper.ConfigureReadOnlyEditorBindings(
                                            bindings,
                                            state.StringsDetailEditorState!,
                                            () => state.VimPending,
                                            () => state.VimPendingEditor,
                                            () => state.VimPendingCursorOffset,
                                            () => state.VimPendingTimestamp,
                                            (s, e, o) => { state.VimPending = s; state.VimPendingEditor = e; state.VimPendingCursorOffset = o; state.VimPendingTimestamp = DateTime.UtcNow; },
                                            state.PerformEditorYank,
                                            () => state.App.Invalidate());
                                    })
                                    .FillWidth().FillHeight())
                            ).Title(" String Detail ").FixedWidth(70).FillHeight()
                        ]).FixedWidth(70).FixedHeight(15)
                    )
                ).OnClickAway(() =>
                {
                    state.StringsDetailContent = null;
                    state.StringsDetailEditorText = null;
                    state.StringsDetailEditorState = null;
                    state.RequestContentFocus();
                    state.App.Invalidate();
                })
                : null
        ]).Fill();
    }

    private static TableWidget<StringEntry> BuildStringsTable(
        WidgetContext<VStackWidget> ctx,
        DotsiderState state,
        IReadOnlyList<StringEntry> activeStrings,
        string? query)
    {
        return ctx.Table(activeStrings)
            .RowKey(RowKey)
            .Header(h =>
            [
                h.Cell("Offset").Width(SizeHint.Fixed(12)),
                h.Cell("Value").Width(SizeHint.Fill)
            ])
            .Row((r, entry, rs) =>
            [
                r.Cell(c => FocusStyle(c,
                    c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, AddressColor),
                        c.Text($"0x{entry.Offset:X8}")),
                    rs.IsFocused)),
                r.Cell(c => FocusStyle(c,
                    HighlightHelper.HighlightCell(c,
                        entry.Value.Length > 200 ? entry.Value[..200] + "..." : entry.Value,
                        query, !string.IsNullOrEmpty(query),
                        rs.IsFocused ? FocusFg : null, rs.IsFocused ? FocusBg : null),
                    rs.IsFocused))
            ])
            .Focus(state.StringsDetailContent is not null ? null : state.StringsFocusedKey)
            .OnFocusChanged(key => state.StringsFocusedKey = key)
            .OnRowActivated((key, entry) =>
            {
                state.StringsDetailContent = entry.Value;
                state.App.RequestFocus(node => node is EditorNode);
                state.App.Invalidate();
            })
            .Compact().Fill();
    }

    private static readonly Hex1bColor FocusFg = Hex1bColor.Black;
    private static readonly Hex1bColor FocusBg = Hex1bColor.FromRgb(0, 200, 180);
    private static readonly Hex1bColor YankFlashFg = Hex1bColor.FromRgb(24, 24, 37);
    private static readonly Hex1bColor YankFlashBg = Hex1bColor.FromRgb(126, 201, 216);

    private static Hex1bWidget FocusStyle<T>(WidgetContext<T> c, Hex1bWidget child, bool isFocused)
        where T : Hex1bWidget
    {
        if (!isFocused) return child;
        var flash = s_yankFlash;
        var fg = flash ? YankFlashFg : FocusFg;
        var bg = flash ? YankFlashBg : FocusBg;
        return c.ThemePanel(t => t
            .Set(GlobalTheme.ForegroundColor, fg)
            .Set(GlobalTheme.BackgroundColor, bg), child);
    }

    private static string RowKey(StringEntry e) =>
        $"{e.Offset}:{e.Source}";

    private static int FindFocusedIndex(IReadOnlyList<StringEntry> entries, object? focusedKey)
    {
        if (focusedKey is not string key) return -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (RowKey(entries[i]) == key)
                return i;
        }

        return -1;
    }
}
