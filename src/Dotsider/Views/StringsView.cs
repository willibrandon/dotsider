using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Strings tab (Tab 4), showing string entries extracted from the assembly
/// across three sub-tabs: User Strings (#US), Metadata Strings (#Strings), and Raw Binary.
/// </summary>
public static class StringsView
{
    private static readonly Hex1bColor AddressColor = Hex1bColor.FromRgb(100, 100, 130);
    private static readonly string[] SourceTabs = ["User Strings (#US)", "Metadata (#Strings)", "Raw Binary"];

    /// <summary>
    /// Builds the Strings view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context from the parent tab panel.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the Strings tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var search = state.Search[TabId.Strings];
        var activeStrings = state.GetActiveStrings();
        var query = search.Query;

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

        return ctx.ZStack(z =>
        [
            // Layer 0: Main content
            z.VStack(outer =>
            {
                var widgets = new List<Hex1bWidget> {
                    // Sub-tab selector for string sources
                outer.TabPanel(tp =>
                    SourceTabs.Select((name, i) =>
                        tp.Tab(name, t => [t.Text("")])
                            .Selected(state.StringsSourceTab == i)
                    ).ToArray()
                )
                .Compact()
                .OnSelectionChanged(e =>
                {
                    state.StringsSourceTab = e.SelectedIndex;
                    search.Reset();
                    state.StringsFocusedKey = null;
                    state.App.Invalidate();
                })
                .FixedHeight(1) };

                // Search bar (shared helper)
                SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

                // Strings table
                widgets.Add(outer.Table((IReadOnlyList<Analysis.Models.StringEntry>)activeStrings)
                    .RowKey(RowKey)
                    .Header(h =>
                    [
                        h.Cell("Offset").Width(SizeHint.Fixed(12)),
                        h.Cell("Value").Width(SizeHint.Fill)
                    ])
                    .Row((r, entry, rowState) =>
                    [
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, AddressColor),
                            c.Text($"0x{entry.Offset:X8}"))),
                        r.Cell(c => HighlightHelper.HighlightCell(c,
                            entry.Value.Length > 200 ? entry.Value[..200] + "..." : entry.Value,
                            query, !string.IsNullOrEmpty(query)))
                    ])
                    .Focus(state.StringsFocusedKey)
                    .OnFocusChanged(key => state.StringsFocusedKey = key)
                    .OnRowActivated((key, entry) =>
                    {
                        state.StringsDetailContent = entry.Value;
                        state.App.Invalidate();
                    })
                    .Fill());

                // Status line
                var statusParts = new List<string> { $"{activeStrings.Count} strings" };
                if (state.StringsSourceTab == StringsSubTabId.RawBinary)
                {
                    statusParts.Add($"Min length: {state.StringsMinLength}");
                }

                var skipped = state.StringsSourceTab switch
                {
                    StringsSubTabId.UserStrings => state.StringExtractor.SkippedUserStringCount,
                    StringsSubTabId.Metadata => state.StringExtractor.SkippedMetadataStringCount,
                    _ => 0
                };
                
                if (skipped > 0)
                {
                    statusParts.Add($"{skipped} malformed skipped");
                }

                widgets.Add(outer.Text($" {string.Join(" | ", statusParts)}").FixedHeight(1));

                return [.. widgets];
            })
            .WithInputBindings(bindings =>
            {
                var isSearchEditing = search.IsActive && !search.IsConfirmed;

                // Left/Right arrows to switch sub-tabs (suppressed during search editing)
                if (!isSearchEditing)
                {
                    bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ =>
                    {
                        if (state.StringsSourceTab > 0)
                        {
                            state.StringsSourceTab--;
                            search.Reset();
                            state.StringsFocusedKey = null;
                            state.App.Invalidate();
                        }
                    }, "Previous sub-tab");

                    bindings.Key(Hex1bKey.RightArrow).Global().Action(_ =>
                    {
                        if (state.StringsSourceTab < StringsSubTabId.Count - 1)
                        {
                            state.StringsSourceTab++;
                            search.Reset();
                            state.StringsFocusedKey = null;
                            state.App.Invalidate();
                        }
                    }, "Next sub-tab");
                }

                bindings.Key(Hex1bKey.OemPlus).Action(_ =>
                {
                    state.StringsMinLength++;
                    state.CachedRawStrings = null;
                    state.App.Invalidate();
                }, "Increase min length");

                bindings.Key(Hex1bKey.Add).Action(_ =>
                {
                    state.StringsMinLength++;
                    state.CachedRawStrings = null;
                    state.App.Invalidate();
                }, "Increase min length");

                bindings.Key(Hex1bKey.OemMinus).Action(_ =>
                {
                    if (state.StringsMinLength > 1)
                    {
                        state.StringsMinLength--;
                        state.CachedRawStrings = null;
                        state.App.Invalidate();
                    }
                }, "Decrease min length");

                bindings.Key(Hex1bKey.Subtract).Action(_ =>
                {
                    if (state.StringsMinLength > 1)
                    {
                        state.StringsMinLength--;
                        state.CachedRawStrings = null;
                        state.App.Invalidate();
                    }
                }, "Decrease min length");

                bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
                {
                    if (search.IsActive)
                    {
                        search.Dismiss();
                        state.App.Invalidate();
                        return;
                    }
                    if (state.StringsDetailContent is not null)
                    {
                        state.StringsDetailContent = null;
                        state.App.Invalidate();
                    }
                }, "Esc");
            })
            .Fill(),

            // Layer 1: String detail popup (conditional)
            state.StringsDetailContent is not null
                ? z.Backdrop(
                    z.Border(
                        z.VScrollPanel(scroll =>
                        [
                            scroll.Text($"  Length: {state.StringsDetailContent.Length}"),
                            scroll.Text(""),
                            scroll.Text($"  {(state.StringsDetailContent.Length > 500
                                ? state.StringsDetailContent[..500] + "..."
                                : state.StringsDetailContent)}")
                        ])
                    ).Title(" String Detail ").FixedWidth(70).FixedHeight(15)
                ).OnClickAway(() =>
                {
                    state.StringsDetailContent = null;
                    state.App.Invalidate();
                })
                : null
        ]).Fill();
    }

    private static string RowKey(Analysis.Models.StringEntry e) =>
        $"{e.Offset}:{e.Source}";

    private static int FindFocusedIndex(IReadOnlyList<Analysis.Models.StringEntry> entries, object? focusedKey)
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
