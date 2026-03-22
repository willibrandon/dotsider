using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Root application class for diff mode. Compares two assemblies side-by-side.
/// </summary>
/// <remarks>
/// Creates a new diff application with the specified state.
/// </remarks>
/// <param name="state">The diff state holding both analyzers and the diff result.</param>
public sealed class DiffApp(DiffState state)
{
    private readonly DiffState _state = state;

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
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(80, 80, 100))),
                bar.Section(" <> "),
                bar.Section(_state.Right.FileName).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(80, 80, 100))),
                bar.Spacer(),
                bar.Section($"+{summary.TypesAdded + summary.MethodsAdded}").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(20, 100, 50))),
                bar.Section($" -{summary.TypesRemoved + summary.MethodsRemoved}").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(140, 30, 30))),
                bar.Section($" ~{summary.TypesChanged + summary.MethodsChanged}").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(130, 110, 30)))
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
                        _state.DiffFocusedKey = null;
                        // Summary tab has editors; other tabs have tables
                        if (tabIndex > 0)
                            _state.App.RequestFocus(node =>
                                node.GetType().Name.StartsWith("TableNode"));
                        _state.App.Invalidate();
                    }, $"Tab {tabIndex + 1}");
                }

                bindings.Key(Hex1bKey.Q).Global().Action(ctx => ctx.RequestStop(), "Quit");

                // Left/Right arrows to cycle tabs (not registered when editor is focused)
                if (_state.App.FocusedNode is not EditorNode)
                {
                    bindings.Key(Hex1bKey.LeftArrow).Global().Action(_ =>
                    {
                        if (_state.CurrentTab > 0)
                        {
                            _state.CurrentTab--;
                            _state.DiffFocusedKey = null;
                            if (_state.CurrentTab > 0)
                                _state.App.RequestFocus(node =>
                                    node.GetType().Name.StartsWith("TableNode"));
                            _state.App.Invalidate();
                        }
                    }, "Previous tab");

                    bindings.Key(Hex1bKey.RightArrow).Global().Action(_ =>
                    {
                        if (_state.CurrentTab < 3)
                        {
                            _state.CurrentTab++;
                            _state.DiffFocusedKey = null;
                            _state.App.RequestFocus(node =>
                                node.GetType().Name.StartsWith("TableNode"));
                            _state.App.Invalidate();
                        }
                    }, "Next tab");
                }

                // Universal yank
                bindings.Key(Hex1bKey.Y).Global().Action(ctx =>
                {
                    // Editor with selection
                    if (ctx.FocusedNode is EditorNode { State.Cursor.HasSelection: true } editor)
                    {
                        var range = editor.State.Cursor.SelectionRange;
                        var doc = editor.State.Document;
                        var yankEnd = new Hex1b.Documents.DocumentOffset(Math.Min(
                            Math.Max(range.End.Value, editor.State.Cursor.Position.Value + 1),
                            doc.Length));
                        var yankRange = new Hex1b.Documents.DocumentRange(range.Start, yankEnd);
                        var text = doc.GetText(yankRange);

                        var lastChar = new Hex1b.Documents.DocumentOffset(Math.Max(0, yankEnd.Value - 1));
                        editor.State.SetCursorPosition(lastChar);

                        if (!string.IsNullOrEmpty(text))
                        {
                            ctx.CopyToClipboard(text);
                            ShowYankNotification(text);
                        }
                        return;
                    }

                    // Editor without selection → do nothing
                    if (ctx.FocusedNode is EditorNode) return;

                    // Table row
                    var yankText = YankHelper.GetYankText(_state);
                    if (yankText is not null)
                    {
                        ctx.CopyToClipboard(yankText);
                        ShowYankNotification(yankText);

                        _state.YankFlashRow = true;
                        _state.App.Invalidate();
                        _ = Task.Delay(TimeSpan.FromMilliseconds(150)).ContinueWith(_ =>
                        {
                            _state.YankFlashRow = false;
                            _state.App.Invalidate();
                        }, TaskScheduler.Default);
                    }
                }, "Yank");
            }

            bindings.Key(Hex1bKey.F).Action(_ =>
            {
                _state.FilterMode = (DiffFilterMode)(((int)_state.FilterMode + 1) % 4);
                _state.App.Invalidate();
            }, "Cycle filter");

            // Global search toggle (same dual-binding strategy as DotsiderApp)
            void searchToggle()
            {
                _state.Search[_state.CurrentTab].ActivateOrCycle();
                var s = _state.Search[_state.CurrentTab];
                if (s.IsActive && !s.IsConfirmed)
                    _state.App.RequestFocus(node => node is TextBoxNode);
                _state.App.Invalidate();
            }

            bindings.Key(Hex1bKey.OemQuestion).Global().OverridesCapture().Action(_ => searchToggle(), "Search");
            if (!isSearchEditing)
            {
                bindings.Key(Hex1bKey.None).Global().OverridesCapture().Action(_ => searchToggle(), "Search");
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

    private InfoBarWidget BuildHintsBar(WidgetContext<VStackWidget> ctx) =>
        ctx.InfoBar(s =>
        {
            var hints = new List<IInfoBarChild>
            {
                s.Section("1-4/←→: Tabs"),
                s.Section("f: Filter")
            };

            var currentSearch = _state.Search[_state.CurrentTab];
            if (currentSearch.IsActive)
                hints.Add(s.Section("Esc: Clear"));
            hints.Add(s.Section("/: Search"));

            var yankable = _state.CurrentTab == 0 || _state.DiffFocusedKey is not null;
            if (yankable)
                hints.Add(s.Section("y: Yank"));

            hints.Add(s.Spacer());

            if (!string.IsNullOrEmpty(_state.YankNotification))
            {
                hints.Add(s.Section(_state.YankNotification).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(120, 180, 120))));
                hints.Add(s.Separator(" "));
            }

            hints.Add(s.Section("q: Quit"));
            return hints;
        }).WithDefaultSeparator(" | ");

    private void ShowYankNotification(string text)
    {
        var gen = ++_state.YankGeneration;
        _state.YankNotification = text.Contains('\n')
            ? $"Yanked {text.Count(c => c == '\n') + 1} lines"
            : $"Yanked: {(text.Length > 40 ? text[..37] + "..." : text)}";
        _state.App.Invalidate();
        _ = Task.Delay(TimeSpan.FromMilliseconds(1500)).ContinueWith(_ =>
        {
            if (_state.YankGeneration == gen)
            {
                _state.YankNotification = null;
                _state.App.Invalidate();
            }
        }, TaskScheduler.Default);
    }

    private static int CountChanges<T>(IReadOnlyList<DiffEntry<T>> diffs) =>
        diffs.Count(d => d.Kind != DiffKind.Unchanged);
}
