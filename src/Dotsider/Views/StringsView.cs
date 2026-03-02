using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Strings tab (Tab 4), showing string entries extracted from the assembly
/// across three sub-tabs: User Strings (#US), Metadata Strings (#Strings), and Raw Binary.
/// </summary>
public static class StringsView
{
    private static readonly string[] SourceTabs = ["User Strings (#US)", "Metadata (#Strings)", "Raw Binary"];

    /// <summary>
    /// Builds the Strings view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context from the parent tab panel.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the Strings tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var activeStrings = state.GetActiveStrings();

        return ctx.ZStack(z =>
        [
            // Layer 0: Main content
            z.VStack(outer =>
            {
                var widgets = new List<Hex1bWidget>();

                // Sub-tab selector for string sources
                widgets.Add(outer.TabPanel(tp =>
                [
                    tp.Tab(SourceTabs[0], t => [t.Text("")]),
                    tp.Tab(SourceTabs[1], t => [t.Text("")]),
                    tp.Tab(SourceTabs[2], t => [t.Text("")])
                ])
                .Compact()
                .OnSelectionChanged(e =>
                {
                    state.StringsSourceTab = e.SelectedIndex;
                    state.StringsSearchQuery = null;
                    state.StringsSearchActive = false;
                    state.StringsFocusedKey = null;
                    state.App.Invalidate();
                })
                .FixedHeight(1));

                // Search bar (visible when active)
                if (state.StringsSearchActive)
                {
                    widgets.Add(outer.HStack(row =>
                    [
                        row.Text(" Search: ").FixedWidth(9),
                        row.TextBox(state.StringsSearchQuery ?? "")
                            .OnTextChanged(e =>
                            {
                                state.StringsSearchQuery = e.NewText;
                                state.App.Invalidate();
                            })
                            .Fill()
                    ]).FixedHeight(1));
                }

                // Strings table
                widgets.Add(outer.Table((IReadOnlyList<Analysis.Models.StringEntry>)activeStrings)
                    .RowKey(e => $"{e.Offset}:{e.Source}")
                    .Header(h =>
                    [
                        h.Cell("Offset").Width(SizeHint.Fixed(12)),
                        h.Cell("Value").Width(SizeHint.Fill)
                    ])
                    .Row((r, entry, rowState) =>
                    [
                        r.Cell($"0x{entry.Offset:X8}"),
                        r.Cell(entry.Value.Length > 200 ? entry.Value[..200] + "..." : entry.Value)
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
                if (state.StringsSourceTab == 2)
                {
                    statusParts.Add($"Min length: {state.StringsMinLength}");
                }
                widgets.Add(outer.Text($" {string.Join(" | ", statusParts)}").FixedHeight(1));

                return widgets.ToArray();
            })
            .WithInputBindings(bindings =>
            {
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

                bindings.Key(Hex1bKey.OemQuestion).Action(_ =>
                {
                    state.StringsSearchActive = !state.StringsSearchActive;
                    if (!state.StringsSearchActive) state.StringsSearchQuery = null;
                    state.App.Invalidate();
                }, "Toggle search");

                bindings.Key(Hex1bKey.Escape).Action(_ =>
                {
                    if (state.StringsDetailContent is not null)
                    {
                        state.StringsDetailContent = null;
                        state.App.Invalidate();
                    }
                }, "Close detail");
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
}
