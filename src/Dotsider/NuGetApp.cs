using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Root application class for NuGet package mode. Browse package contents and inspect DLLs.
/// </summary>
/// <remarks>
/// Creates a new NuGet application with the specified state.
/// </remarks>
/// <param name="state">The NuGet state holding the package analyzer and UI state.</param>
public sealed class NuGetApp(NuGetState state)
{
    private readonly NuGetState _state = state;

    /// <summary>
    /// Builds the root widget tree for the NuGet package browser.
    /// </summary>
    /// <param name="ctx">The Hex1b root context for widget construction.</param>
    /// <returns>The root widget of the NuGet application.</returns>
    public Hex1bWidget Build(RootContext ctx)
    {
        _state.PerformEditorYank ??= PerformEditorYank;

        // Wire yank delegate on the embedded DLL state too
        if (_state.SelectedDllState is { PerformEditorYank: null } dllSetup)
            dllSetup.PerformEditorYank = PerformEditorYank;

        // Drain pending mutations from the diagnostics socket listener. Advancing the
        // build generation stops any in-flight extra-frame nudger armed by the listener.
        if (_state.SelectedDllState is { } dllState)
        {
            unchecked { dllState.BuildGeneration++; }
            dllState.ExtraFrameArmed = false;
            while (dllState.PendingMutations.TryDequeue(out var mutation))
                mutation(dllState);
        }

        return ctx.VStack(outer =>
        [
            // Title bar
            outer.InfoBar(bar =>
            [
                bar.Section(" dotsider nupkg ").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Black)
                    .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(160, 100, 200))),
                bar.Divider(" "),
                bar.Section(_state.Package.FileName).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(80, 80, 100))),
                bar.Spacer(),
                bar.Section(_state.IsBrowsingPackage ? "Package Browser" : "DLL Inspector").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(130, 110, 30))),
                bar.Divider(" | "),
                bar.Section($"{_state.Package.DllFiles.Count} DLLs").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(130, 110, 30)))
            ]),

            // Main content: browser or inspector
            _state.IsBrowsingPackage
                ? NuGetBrowserView.Build(outer, _state)
                : BuildDllInspector(outer),

            // Hints bar
            outer.InfoBar(s =>
            {
                var hints = new List<IInfoBarChild>();

                if (_state.IsBrowsingPackage)
                {
                    hints.Add(s.Section("Enter: Open DLL"));
                    if (_state.BrowserSearch.IsActive)
                        hints.Add(s.Section("Esc: Clear"));
                    hints.Add(s.Section("/: Search"));
                    hints.Add(s.Section("y: Yank"));
                }
                else if (_state.SelectedDllState is not null)
                {
                    var dll = _state.SelectedDllState;
                    hints.Add(s.Section("1-5: Tabs"));
                    // Only show "Esc: Back" when no hex/search modal will claim Esc first
                    var hexInsert = dll.CurrentTab == TabId.HexDump && dll.HexMode == HexEditMode.Insert;
                    var dllSearchAct = dll.Search[dll.CurrentTab].IsActive;
                    if (!hexInsert && !dllSearchAct && !dll.HexJumpDialogOpen
                        && dll.PeDetailContent is null && dll.StringsDetailContent is null)
                        hints.Add(s.Section("Esc: Back"));
                    // DLL-inspector-specific hints (shared with DotsiderApp)
                    DllInspectorBindings.AddHints(hints, s, dll);
                }

                // iw/iW hint — show when a read-only editor is focused (not hex dump)
                try
                {
                    var isHexDump = _state.SelectedDllState is
                        { CurrentTab: TabId.HexDump };
                    if (_state.App.FocusedNode is EditorNode && !isHexDump)
                        hints.Add(s.Section("V: Line | iw: Word | iW: WORD"));
                }
                catch (NullReferenceException)
                {
                    // Focus ring not yet initialized
                }

                hints.Add(s.Spacer());

                // Yank notification (right side, auto-clearing)
                var yankNotification = _state.YankNotification
                    ?? _state.SelectedDllState?.YankNotification;
                if (!string.IsNullOrEmpty(yankNotification))
                {
                    hints.Add(s.Section(yankNotification).Theme(t => t
                        .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(120, 180, 120))));
                    hints.Add(s.Divider(" "));
                }

                // Navigation error in DLL inspector (right side)
                if (!_state.IsBrowsingPackage && _state.SelectedDllState is { NavigationError: { } navError })
                {
                    hints.Add(s.Section(navError).Theme(t => t
                        .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 80, 60))));
                    hints.Add(s.Divider(" "));
                }

                hints.Add(s.Section("q: Quit"));
                return hints;
            }).Divider(" | ")
        ])
        .InputBindings(bindings =>
        {
            // VimReset wraps all Global bindings to cancel pending vim text-object sequences
            Action<InputBindingActionContext> VimReset(Action<InputBindingActionContext> action)
                => ctx => { _state.VimPending = VimMotionState.Idle; action(ctx); };

            var browserSearch = _state.BrowserSearch;
            // Gate on BOTH browser and embedded DLL inspector input state
            var dllSearch = _state.SelectedDllState?.Search[_state.SelectedDllState.CurrentTab];
            var dllSearchEditing = dllSearch is { IsActive: true, IsConfirmed: false };
            var hexInsertMode = _state.SelectedDllState is { CurrentTab: TabId.HexDump }
                && _state.SelectedDllState.HexMode == HexEditMode.Insert;
            var hexJumpOpen = _state.SelectedDllState?.HexJumpDialogOpen == true;
            var dllEditingArgs = _state.SelectedDllState?.DynamicEditingArgs == true;
            var isSearchEditing = (browserSearch.IsActive && !browserSearch.IsConfirmed)
                || dllSearchEditing || hexInsertMode || hexJumpOpen || dllEditingArgs;

            if (_state.IsBrowsingPackage)
            {
                // Search toggle (same dual-binding strategy as DotsiderApp/DiffApp)
                void SearchToggle()
                {
                    browserSearch.ActivateOrCycle();
                    if (browserSearch.IsActive && !browserSearch.IsConfirmed)
                        _state.App.RequestFocus(node => node is TextBoxNode);
                    _state.App.Invalidate();
                }
                bindings.Key(Hex1bKey.OemQuestion).Global().OverridesCapture().Action(VimReset(_ => SearchToggle()), "Search");
                if (!isSearchEditing)
                {
                    bindings.Key(Hex1bKey.None).Global().OverridesCapture().Action(VimReset(_ => SearchToggle()), "Search");
                }
                if (isSearchEditing)
                {
                    bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(VimReset(_ =>
                    {
                        if (!string.IsNullOrEmpty(browserSearch.Query))
                        {
                            browserSearch.Confirm();
                            _state.App.Invalidate();
                        }
                    }), "Confirm search");
                }

                bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(VimReset(_ =>
                {
                    if (browserSearch.IsActive)
                    {
                        browserSearch.Dismiss();
                        _state.App.Invalidate();
                    }
                }), "Esc");

                bindings.Key(Hex1bKey.Enter).Action(_ =>
                {
                    // Filter against search query so Enter cannot open a hidden DLL
                    var visibleDlls = (IReadOnlyList<NuGetFileEntry>)_state.Package.DllFiles;
                    var q = browserSearch.Query;
                    if (!string.IsNullOrEmpty(q))
                    {
                        visibleDlls = [.. visibleDlls.Where(d =>
                            d.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            d.Directory.Contains(q, StringComparison.OrdinalIgnoreCase))];
                    }

                    var focusedKey = _state.FileTreeFocusedKey as string;
                    var entry = focusedKey is not null
                        ? visibleDlls.FirstOrDefault(d => d.FullPath == focusedKey)
                        : visibleDlls.Count > 0 ? visibleDlls[0] : null;

                    if (entry is null) return;

                    try
                    {
                        var analyzer = _state.Package.OpenDll(entry);
                        _state.SelectedDllState?.Dispose();
                        _state.SelectedDllState = new DotsiderState(_state.App, analyzer);
                        _state.SelectedDllEntry = entry;
                        _state.IsBrowsingPackage = false;
                        _state.App.Invalidate();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to open DLL: {ex.Message}");
                    }
                }, "Open DLL");
            }

            // Escape handler — search/hex modals are handled by DllInspectorBindings.
            // This handler covers: detail popups, back-to-package, and browser search dismiss.
            // Only register when the shared helper hasn't already claimed Escape for search/hex.
            var dllSearchActive = dllSearch is { IsActive: true };
            var dllHexInsertNoSearch = !dllSearchActive
                && _state.SelectedDllState is { CurrentTab: TabId.HexDump, HexMode: HexEditMode.Insert };
            var dllHexJumpOpen = _state.SelectedDllState?.HexJumpDialogOpen == true;
            if (!dllSearchActive && !dllHexInsertNoSearch && !dllHexJumpOpen)
            {
                bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(VimReset(_ =>
                {
                    if (!_state.IsBrowsingPackage)
                    {
                        var dllState = _state.SelectedDllState;
                        if (dllState is not null)
                        {
                            if (dllState.PeDetailContent is not null)
                            {
                                dllState.PeDetailContent = null;
                                dllState.PeDetailEditorText = null;
                                dllState.PeDetailEditorState = null;
                                dllState.RequestContentFocus();
                                _state.App.Invalidate();
                                return;
                            }
                            
                            if (dllState.StringsDetailContent is not null)
                            {
                                dllState.StringsDetailContent = null;
                                dllState.StringsDetailEditorText = null;
                                dllState.StringsDetailEditorState = null;
                                dllState.RequestContentFocus();
                                _state.App.Invalidate();
                                return;
                            }

                            if (dllState.CurrentTab == TabId.IlInspector && dllState.IlBackStack.Count > 0)
                            {
                                var entry = dllState.IlBackStack.Pop();
                                dllState.RestoreFromIlBackEntry(entry);
                                return;
                            }

                            if (dllState.CrossViewBackTarget is not null)
                            {
                                dllState.NavigateBack();
                                return;
                            }
                        }

                        _state.SelectedDllState?.Dispose();
                        _state.SelectedDllState = null;
                        _state.SelectedDllEntry = null;
                        _state.IsBrowsingPackage = true;
                        _state.FileTreeFocusedKey = _state.SavedFileTreeFocusedKey;
                        _state.App.RequestFocus(node =>
                            node.GetType().Name.StartsWith("TableNode"));
                        _state.App.Invalidate();
                    }
                    else if (_state.BrowserSearch.IsActive)
                    {
                        _state.BrowserSearch.Dismiss();
                        _state.App.Invalidate();
                    }
                }), "Back to package");
            }

            if (!_state.IsBrowsingPackage && _state.SelectedDllState is not null)
            {
                if (!isSearchEditing)
                {
                    for (var i = 0; i < 5; i++)
                    {
                        var tabIndex = i;
                        var key = (Hex1bKey)((int)Hex1bKey.D1 + i);
                        bindings.Key(key).Global().Action(VimReset(_ =>
                        {
                            _state.SelectedDllState!.CurrentTab = tabIndex;
                            _state.SelectedDllState.RequestContentFocus();
                            _state.App.Invalidate();
                        }), $"Tab {tabIndex + 1}");
                    }
                }

                // Hex + IL Inspector + search bindings (shared with DotsiderApp).
                // Must be outside isSearchEditing gate so hex insert Escape works.
                DllInspectorBindings.Register(bindings, _state.SelectedDllState, _state.App,
                    includeSearch: true,
                    resetVimPending: () => _state.VimPending = VimMotionState.Idle);
            }

            if (!isSearchEditing)
            {
                bindings.Key(Hex1bKey.Q).Global().Action(VimReset(ctx => ctx.RequestStop()), "Quit");

                // Universal yank — same behavior as DotsiderApp
                bindings.Key(Hex1bKey.Y).Global().Action(ctx =>
                {
                    // Timeout check — reset stale state on both stores
                    if (_state.VimPending != VimMotionState.Idle
                        && (DateTime.UtcNow - _state.VimPendingTimestamp).TotalSeconds > 1.0)
                        _state.VimPending = VimMotionState.Idle;
                    if (_state.SelectedDllState is { VimPending: not VimMotionState.Idle } dllTimeout
                        && (DateTime.UtcNow - dllTimeout.VimPendingTimestamp).TotalSeconds > 1.0)
                        dllTimeout.VimPending = VimMotionState.Idle;

                    // 1. yy: second y while already armed → yank entire line
                    // Check both state stores (browser vs DLL inspector)
                    if (ctx.FocusedNode is EditorNode { State: var yyState } yyEditor)
                    {
                        var dllPending = _state.SelectedDllState;
                        var isDllYY = dllPending is { VimPending: VimMotionState.WaitingForYMotion }
                            && yyState == dllPending.VimPendingEditor
                            && yyState.Cursor.Position.Value == dllPending.VimPendingCursorOffset;
                        var isBrowserYY = _state.VimPending == VimMotionState.WaitingForYMotion
                            && yyState == _state.VimPendingEditor
                            && yyState.Cursor.Position.Value == _state.VimPendingCursorOffset;

                        if (isDllYY || isBrowserYY)
                        {
                            if (isDllYY) dllPending!.VimPending = VimMotionState.Idle;
                            if (isBrowserYY) _state.VimPending = VimMotionState.Idle;
                            TextObjectHelper.SelectLine(yyState);
                            if (yyState.Cursor.HasSelection)
                                PerformEditorYank(ctx, yyEditor);
                            return;
                        }
                    }

                    // 2. Any focused editor with selection
                    if (ctx.FocusedNode is EditorNode { State.Cursor.HasSelection: true } editor)
                    {
                        _state.VimPending = VimMotionState.Idle;
                        if (_state.SelectedDllState is { } dllSel)
                            dllSel.VimPending = VimMotionState.Idle;
                        PerformEditorYank(ctx, editor);
                        return;
                    }

                    // 3. Focused editor WITHOUT selection → arm operator-pending for yiw/yiW/yy
                    if (ctx.FocusedNode is EditorNode noSelEditor)
                    {
                        // Don't arm on hex dump normal mode (I conflicts with Insert)
                        var isHexNormal = _state.SelectedDllState is
                            { CurrentTab: TabId.HexDump, HexMode: HexEditMode.Normal };
                        if (isHexNormal)
                        {
                            _state.VimPending = VimMotionState.Idle;
                            return;
                        }

                        // Arm on the correct state: DLL inspector views read from
                        // SelectedDllState, browser views read from NuGetState.
                        if (!_state.IsBrowsingPackage && _state.SelectedDllState is { } dllArm)
                        {
                            dllArm.VimPending = VimMotionState.WaitingForYMotion;
                            dllArm.VimPendingEditor = noSelEditor.State;
                            dllArm.VimPendingCursorOffset = noSelEditor.State.Cursor.Position.Value;
                            dllArm.VimPendingTimestamp = DateTime.UtcNow;
                        }
                        else
                        {
                            _state.VimPending = VimMotionState.WaitingForYMotion;
                            _state.VimPendingEditor = noSelEditor.State;
                            _state.VimPendingCursorOffset = noSelEditor.State.Cursor.Position.Value;
                            _state.VimPendingTimestamp = DateTime.UtcNow;
                        }

                        return;
                    }

                    // 3. Non-editor focus → table row
                    _state.VimPending = VimMotionState.Idle;
                    string? yankText = null;
                    if (_state.IsBrowsingPackage)
                    {
                        // Seed focus if not yet set
                        _state.FileTreeFocusedKey ??=
                            _state.Package.DllFiles.Count > 0 ? _state.Package.DllFiles[0].FullPath : null;
                        if (_state.FileTreeFocusedKey is string path)
                            yankText = path;
                    }
                    else if (_state.SelectedDllState is not null)
                    {
                        yankText = YankHelper.GetYankText(_state.SelectedDllState);
                    }

                    if (yankText is not null)
                    {
                        ctx.CopyToClipboard(yankText);
                        ShowYankNotification(yankText);

                        // Flash the focused row
                        if (_state.IsBrowsingPackage)
                        {
                            _state.YankFlashRow = true;
                            _state.App.Invalidate();
                            _ = Task.Delay(TimeSpan.FromMilliseconds(150)).ContinueWith(_ =>
                            {
                                _state.YankFlashRow = false;
                                _state.App.Invalidate();
                            }, TaskScheduler.Default);
                        }
                        else if (_state.SelectedDllState is not null)
                        {
                            var flashTarget = _state.SelectedDllState;
                            flashTarget.YankFlashRow = true;
                            _state.App.Invalidate();
                            _ = Task.Delay(TimeSpan.FromMilliseconds(150)).ContinueWith(_ =>
                            {
                                flashTarget.YankFlashRow = false;
                                _state.App.Invalidate();
                            }, TaskScheduler.Default);
                        }
                    }
                }, "Yank");
            }
            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(VimReset(ctx => ctx.RequestStop()), "Quit");
        });
    }

    /// <summary>
    /// Performs a neovim-style yank on the focused editor's selection.
    /// Handles hex dump byte extraction, cursor collapse, flash, clipboard, and notification.
    /// </summary>
    private void PerformEditorYank(InputBindingActionContext ctx, EditorNode editor)
    {
        string text;
        var hexState = _state.SelectedDllState?.HexEditorState;
        if (hexState is not null && editor.State == hexState)
        {
            text = YankHelper.GetHexSelectionText(editor.State) ?? "";
        }
        else
        {
            var range = editor.State.Cursor.SelectionRange;
            var doc = editor.State.Document;
            var yankEnd = new Hex1b.Documents.DocumentOffset(Math.Min(
                Math.Max(range.End.Value, editor.State.Cursor.Position.Value + 1),
                doc.Length));
            var yankRange = new Hex1b.Documents.DocumentRange(range.Start, yankEnd);
            text = doc.GetText(yankRange);

            var lastChar = new Hex1b.Documents.DocumentOffset(Math.Max(0, yankEnd.Value - 1));
            editor.State.SetCursorPosition(lastChar);

            var yankProvider = YankHelper.FindYankProvider(_state, editor.State);
            if (yankProvider is not null)
            {
                var startPos = doc.OffsetToPosition(yankRange.Start);
                var endPos = doc.OffsetToPosition(yankRange.End);
                yankProvider.HighlightRange = (startPos, endPos);
                _state.App.Invalidate();
                _ = Task.Delay(TimeSpan.FromMilliseconds(150)).ContinueWith(_ =>
                {
                    yankProvider.HighlightRange = null;
                    _state.App.Invalidate();
                }, TaskScheduler.Default);
            }
        }

        if (!string.IsNullOrEmpty(text))
        {
            ctx.CopyToClipboard(text);
            ShowYankNotification(text);
        }
    }

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

    private Hex1bWidget BuildDllInspector(WidgetContext<VStackWidget> outer)
    {
        if (_state.SelectedDllState is null)
            return outer.Text("  No DLL selected").Fill();

        var dllState = _state.SelectedDllState;

        return outer.TabPanel(tp =>
        [
            tp.Tab("General", t => [GeneralView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 0),
            tp.Tab("PE/Metadata", t => [PeMetadataView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 1),
            tp.Tab("IL Inspector", t => [IlInspectorView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 2),
            tp.Tab("Strings", t => [StringsView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 3),
            tp.Tab("Hex Dump", t => [HexDumpView.Build(t, dllState)])
                .Selected(dllState.CurrentTab == 4)
        ])
        .OnSelectionChanged(e =>
        {
            dllState.CurrentTab = e.SelectedIndex;
            _state.App.Invalidate();
        })
        .Full()
        .Fill();
    }
}
