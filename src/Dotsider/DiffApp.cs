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

    /// <summary>
    /// Creates a new diff application with the specified state.
    /// </summary>
    /// <param name="state">The diff state holding both analyzers and the diff result.</param>
    public DiffApp(DiffState state) => _state = state;

    /// <summary>
    /// Builds the root widget tree for the diff view.
    /// </summary>
    /// <param name="ctx">The Hex1b root context for widget construction.</param>
    /// <returns>The root widget of the diff application.</returns>
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
            BuildHintsBar(outer)
        ])
        .WithInputBindings(bindings =>
        {
            var currentSearch = _state.Search[_state.CurrentTab];
            var isSearchEditing = currentSearch.IsActive && !currentSearch.IsConfirmed;

            // Number keys 1-4, f, q suppressed during search editing to let TextBox receive input
            if (!isSearchEditing)
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

                bindings.Key(Hex1bKey.Q).Global().Action(ctx => ctx.RequestStop(), "Quit");
            }

            bindings.Key(Hex1bKey.F).Action(_ =>
            {
                _state.FilterMode = (DiffFilterMode)(((int)_state.FilterMode + 1) % 4);
                _state.App.Invalidate();
            }, "Cycle filter");

            // Global search toggle (same dual-binding strategy as DotsiderApp)
            Action searchToggle = () =>
            {
                _state.Search[_state.CurrentTab].ActivateOrCycle();
                var s = _state.Search[_state.CurrentTab];
                if (s.IsActive && !s.IsConfirmed)
                    _state.App.RequestFocus(node => node is TextBoxNode);
                _state.App.Invalidate();
            };
            bindings.Key(Hex1bKey.OemQuestion).Global().Action(_ => searchToggle(), "Search");
            if (!isSearchEditing)
            {
                bindings.Key(Hex1bKey.None).Global().Action(_ => searchToggle(), "Search");
            }
            if (isSearchEditing)
            {
                bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(_ =>
                {
                    if (!string.IsNullOrEmpty(currentSearch.Query))
                    {
                        currentSearch.Confirm();
                        _state.App.Invalidate();
                    }
                }, "Confirm search");
            }

            // n/N only registered when search is confirmed
            if (currentSearch.IsActive && currentSearch.IsConfirmed)
            {
                bindings.Key(Hex1bKey.N).Global().Action(_ =>
                {
                    _state.NavigateNextMatch?.Invoke();
                    _state.App.Invalidate();
                }, "Next match");
                bindings.Shift().Key(Hex1bKey.N).Global().Action(_ =>
                {
                    _state.NavigatePrevMatch?.Invoke();
                    _state.App.Invalidate();
                }, "Prev match");
            }

            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(ctx => ctx.RequestStop(), "Quit");
        });
    }

    private Hex1bWidget BuildHintsBar(WidgetContext<VStackWidget> ctx)
    {
        return ctx.InfoBar(s =>
        {
            var hints = new List<IInfoBarChild>();
            hints.Add(s.Section("1-4: Tabs"));
            hints.Add(s.Section("f: Filter"));

            var currentSearch = _state.Search[_state.CurrentTab];
            if (currentSearch.IsActive)
                hints.Add(s.Section("Esc: Clear"));
            hints.Add(s.Section("/: Search"));

            hints.Add(s.Spacer());
            hints.Add(s.Section("q: Quit"));
            return hints;
        }).WithDefaultSeparator(" | ");
    }

    private static int CountChanges<T>(IReadOnlyList<DiffEntry<T>> diffs)
        => diffs.Count(d => d.Kind != DiffKind.Unchanged);
}
