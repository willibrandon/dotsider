using Dotsider.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Charts;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Root application class for diff mode. Compares two assemblies side-by-side.
/// </summary>
public sealed class DiffApp
{
    private readonly DiffState _state;

    public DiffApp(DiffState state) => _state = state;

    public Hex1bWidget Build(RootContext ctx)
    {
        var summary = _state.DiffResult.MetadataSummary;
        var changeCount = summary.TypesAdded + summary.TypesRemoved + summary.TypesChanged +
                          summary.MethodsAdded + summary.MethodsRemoved + summary.MethodsChanged;

        return ctx.VStack(outer =>
        [
            // Title bar
            outer.InfoBar(bar =>
            [
                bar.Section(" dotsider diff ").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Black)
                    .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(200, 200, 80))),
                bar.Separator(" "),
                bar.Section(_state.Left.FileName).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(180, 180, 200))),
                bar.Section(" <> "),
                bar.Section(_state.Right.FileName).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(180, 180, 200))),
                bar.Spacer(),
                bar.Section($"+{summary.TypesAdded + summary.MethodsAdded}").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(80, 200, 120))),
                bar.Section($" -{summary.TypesRemoved + summary.MethodsRemoved}").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 80, 80))),
                bar.Section($" ~{summary.TypesChanged + summary.MethodsChanged}").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 200, 80)))
            ]),

            // Filter indicator
            outer.HStack(h =>
            [
                h.Text($" Filter: {_state.FilterMode}"),
                h.Text($" | {changeCount} changes").Fill()
            ]).FixedHeight(1),

            // Diff tabs
            outer.TabPanel(tp =>
            [
                tp.Tab("Summary", t => [DiffSummaryView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 0),
                tp.Tab($"Types ({CountChanges(_state.DiffResult.TypeDiffs)})",
                    t => [DiffTypesView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 1),
                tp.Tab($"Methods ({CountChanges(_state.DiffResult.MethodDiffs)})",
                    t => [DiffMethodsView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 2),
                tp.Tab($"References ({CountChanges(_state.DiffResult.AssemblyRefDiffs)})",
                    t => [DiffRefsView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 3)
            ])
            .OnSelectionChanged(e =>
            {
                _state.CurrentTab = e.SelectedIndex;
                _state.DiffFocusedKey = null;
                _state.App.Invalidate();
            })
            .Full()
            .Fill(),

            // Hints bar
            outer.InfoBar(s =>
            [
                s.Section("1-4: Tabs"),
                s.Separator(" | "),
                s.Section("f: Filter"),
                s.Separator(" | "),
                s.Section("/: Search"),
                s.Spacer(),
                s.Section("q: Quit")
            ]).WithDefaultSeparator(" | ")
        ])
        .WithInputBindings(bindings =>
        {
            for (var i = 0; i < 4; i++)
            {
                var tabIndex = i;
                var key = (Hex1bKey)((int)Hex1bKey.D1 + i);
                bindings.Key(key).Global().Action(_ =>
                {
                    _state.CurrentTab = tabIndex;
                    _state.App.Invalidate();
                }, $"Tab {tabIndex + 1}");
            }

            bindings.Key(Hex1bKey.F).Action(_ =>
            {
                _state.FilterMode = (DiffFilterMode)(((int)_state.FilterMode + 1) % 4);
                _state.App.Invalidate();
            }, "Cycle filter");

            bindings.Key(Hex1bKey.Q).Global().Action(ctx => ctx.RequestStop(), "Quit");
            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(ctx => ctx.RequestStop(), "Quit");
        });
    }

    private static int CountChanges<T>(IReadOnlyList<DiffEntry<T>> diffs)
        => diffs.Count(d => d.Kind != DiffKind.Unchanged);
}
