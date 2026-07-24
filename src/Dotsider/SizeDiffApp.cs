using Dotsider.Infrastructure;
using Dotsider.Views;
using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Root application class for size-diff mode: two Native AOT builds compared by their mstat
/// size reports. Two tabs — Summary and the delta treemap — with direction filtering,
/// why-chains against either build's dependency graph, and native disassembly when a binary
/// backs a side. This is a separate app from <see cref="DiffApp"/> because size-diff inputs
/// carry no managed metadata; showing the managed diff tabs would show empty tables.
/// </summary>
/// <remarks>
/// Creates a new size-diff application with the specified state.
/// </remarks>
/// <param name="state">The size-diff state holding both inputs and the computed difference.</param>
public sealed class SizeDiffApp(SizeDiffState state)
{
    private readonly SizeDiffState _state = state;

    /// <summary>
    /// Builds the root widget tree for the size-diff view.
    /// </summary>
    /// <param name="ctx">The Hex1b root context for widget construction.</param>
    /// <returns>The root widget of the size-diff application.</returns>
    public Hex1bWidget Build(RootContext ctx)
    {
        _state.PerformEditorYank ??= PerformEditorYank;
        if (!_state.InitialFocusRequested)
        {
            _state.InitialFocusRequested = true;
            _state.RequestContentFocus();
        }

        var summary = _state.Diff.Summary;
        var deltaColor = summary.Delta > 0
            ? Hex1bColor.FromRgb(180, 60, 60)
            : summary.Delta < 0 ? Hex1bColor.FromRgb(40, 130, 60) : Hex1bColor.FromRgb(110, 110, 140);

        return ctx.VStack(outer =>
        [
            // Title bar
            outer.InfoBar(bar =>
            [
                bar.Section(" dotsider size diff ").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Black)
                    .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(200, 200, 80))),
                bar.Divider(" "),
                bar.Section(TerminalText.Escape(_state.LeftName)).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(80, 80, 100))),
                bar.Section(" <> "),
                bar.Section(TerminalText.Escape(_state.RightName)).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(80, 80, 100))),
                bar.Spacer(),
                bar.Section($"Δ {SizeDiffTreemapView.FormatDelta(summary.Delta)}").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, deltaColor))
            ]),

            // Filter indicator
            outer.HStack(h =>
            [
                h.Text($" Filter: {_state.FilterMode}"),
                h.Text($" | {_state.Diff.Contributors.Count} changed entries").Fill()
            ]).FixedHeight(1),

            // Tabs
            outer.TabPanel(tp =>
            [
                tp.Tab("Summary", t => [SizeDiffSummaryView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 0),
                tp.Tab("Size Map", t => [SizeDiffTreemapView.Build(t, _state)])
                    .Selected(_state.CurrentTab == 1)
            ])
            .OnSelectionChanged(e =>
            {
                _state.CurrentTab = e.SelectedIndex;
                _state.RequestContentFocus();
                _state.App.Invalidate();
            })
            .Full()
            .Fill(),

            // Hints bar
            BuildHintsBar(outer)
        ])
        .InputBindings(bindings =>
        {
            var currentSearch = _state.Search[_state.CurrentTab];
            var isSearchEditing = currentSearch.IsActive && !currentSearch.IsConfirmed;

            Action<InputBindingActionContext> VimReset(Action<InputBindingActionContext> action)
                => ctx => { _state.VimPending = VimMotionState.Idle; action(ctx); };

            if (!isSearchEditing)
            {
                for (var i = 0; i < 2; i++)
                {
                    var tabIndex = i;
                    var key = (Hex1bKey)((int)Hex1bKey.D1 + i);
                    bindings.Key(key).Global().Action(VimReset(_ =>
                    {
                        _state.CurrentTab = tabIndex;
                        _state.RequestContentFocus();
                        _state.App.Invalidate();
                    }), $"Tab {tabIndex + 1}");
                }

                bindings.Key(Hex1bKey.Q).Global().Action(VimReset(ctx => ctx.RequestStop()), "Quit");

                // Yank: editors only — the treemap has no row model to yank.
                bindings.Key(Hex1bKey.Y).Global().Action(ctx =>
                {
                    if (_state.VimPending == VimMotionState.WaitingForYMotion
                        && ctx.FocusedNode is EditorNode { State: var yyState } yyEditor
                        && yyState == _state.VimPendingEditor
                        && yyState.Cursor.Position.Value == _state.VimPendingCursorOffset)
                    {
                        _state.VimPending = VimMotionState.Idle;
                        TextObjectHelper.SelectLine(yyState);
                        if (yyState.Cursor.HasSelection)
                            PerformEditorYank(ctx, yyEditor);
                        return;
                    }

                    if (ctx.FocusedNode is EditorNode { State.Cursor.HasSelection: true } editor)
                    {
                        _state.VimPending = VimMotionState.Idle;
                        PerformEditorYank(ctx, editor);
                        return;
                    }

                    if (ctx.FocusedNode is EditorNode noSelEditor)
                    {
                        _state.VimPending = VimMotionState.WaitingForYMotion;
                        _state.VimPendingEditor = noSelEditor.State;
                        _state.VimPendingCursorOffset = noSelEditor.State.Cursor.Position.Value;
                        _state.VimPendingTimestamp = DateTime.UtcNow;
                    }
                }, "Yank");
            }

            bindings.Key(Hex1bKey.F).Action(VimReset(_ =>
            {
                _state.FilterMode = (SizeDiffFilterMode)(((int)_state.FilterMode + 1) % 5);
                _state.App.Invalidate();
            }), "Cycle filter");

            void searchToggle()
            {
                _state.Search[_state.CurrentTab].ActivateOrCycle();
                var s = _state.Search[_state.CurrentTab];
                if (s.IsActive && !s.IsConfirmed)
                    _state.App.RequestFocus(node => node is TextBoxNode);
                _state.App.Invalidate();
            }

            bindings.Key(Hex1bKey.OemQuestion).Global().OverridesCapture()
                .Action(VimReset(_ => searchToggle()), "Search");
            if (!isSearchEditing)
            {
                bindings.Key(Hex1bKey.None).Global().OverridesCapture()
                    .Action(VimReset(_ => searchToggle()), "Search");
            }

            if (isSearchEditing)
            {
                bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(VimReset(_ =>
                {
                    if (!string.IsNullOrEmpty(currentSearch.Query))
                    {
                        currentSearch.Confirm();
                        _state.App.Invalidate();
                    }
                }), "Confirm search");
            }

            if (currentSearch.IsActive && currentSearch.IsConfirmed)
            {
                bindings.Key(Hex1bKey.N).Global().Action(VimReset(_ =>
                {
                    _state.NavigateNextMatch?.Invoke();
                    _state.App.Invalidate();
                }), "Next match");
                bindings.Shift().Key(Hex1bKey.N).Global().Action(VimReset(_ =>
                {
                    _state.NavigatePrevMatch?.Invoke();
                    _state.App.Invalidate();
                }), "Prev match");
            }

            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(VimReset(ctx => ctx.RequestStop()), "Quit");
        });
    }

    private InfoBarWidget BuildHintsBar(WidgetContext<VStackWidget> ctx) =>
        ctx.InfoBar(s =>
        {
            var hints = new List<IInfoBarChild>
            {
                s.Section("1-2: Tabs"),
                s.Section("f: Filter")
            };

            if (_state.CurrentTab == 1)
            {
                hints.Add(s.Section("Enter: Drill | Esc: Up"));
                hints.Add(s.Section("w: Why"));
                if (_state.LeftSource.BinaryPath is not null || _state.RightSource.BinaryPath is not null)
                    hints.Add(s.Section("d: Disasm"));
            }

            var currentSearch = _state.Search[_state.CurrentTab];
            if (currentSearch.IsActive)
                hints.Add(s.Section("Esc: Clear"));
            hints.Add(s.Section("/: Search"));

            try
            {
                if (_state.App.FocusedNode is EditorNode)
                    hints.Add(s.Section("y: Yank | V: Line | iw: Word"));
            }
            catch (NullReferenceException)
            {
                // Focus ring not yet initialized
            }

            hints.Add(s.Spacer());

            if (!string.IsNullOrEmpty(_state.YankNotification))
            {
                hints.Add(s.Section(TerminalText.Escape(_state.YankNotification)).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(120, 180, 120))));
                hints.Add(s.Divider(" "));
            }

            hints.Add(s.Section("q: Quit"));
            return hints;
        }).Divider(" | ");

    private void PerformEditorYank(InputBindingActionContext ctx, EditorNode editor)
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
    }
}
