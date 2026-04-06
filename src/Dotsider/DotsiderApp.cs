using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// The root application class that builds the entire dotsider widget tree.
/// Manages the top-level layout: title bar, tab panel, and keybinding hints bar.
/// </summary>
/// <remarks>
/// Creates a new dotsider application with the specified state.
/// </remarks>
/// <param name="state">The application state holding the analyzer and all UI state.</param>
public sealed class DotsiderApp(DotsiderState state)
{
    private readonly DotsiderState _state = state;
    private bool _initialFocusRequested;
    private bool _yankDelegateSet;

    /// <summary>
    /// Builds the root widget tree for the current frame.
    /// </summary>
    /// <param name="ctx">The Hex1b root context for widget construction.</param>
    /// <returns>The root widget of the application.</returns>
    public Hex1bWidget Build(RootContext ctx)
    {
        // Drain pending mutations from the diagnostics socket listener
        while (_state.PendingMutations.TryDequeue(out var mutation))
            mutation(_state);

        // On first render, move focus from the tab bar into the content area.
        // Defer if the apphost dialog is open — focus will be seeded on dismiss.
        if (!_initialFocusRequested)
        {
            _initialFocusRequested = true;
            if (!_state.ApphostDialogOpen)
            {
                SeedFocusedRowIfNeeded();
                RequestContentFocus();
            }
        }

        // Wire up the yank delegate for text object support
        if (!_yankDelegateSet)
        {
            _yankDelegateSet = true;
            _state.PerformEditorYank = PerformEditorYank;
        }


        var mainContent = ctx.VStack(outer =>
        [
            // Title bar
            outer.InfoBar(bar =>
            [
                bar.Section($" dotsider ").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.Black)
                    .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(0, 200, 180))),
                bar.Separator(" "),
                bar.Section(_state.NavigationStack.Count > 0
                    ? $"{_state.Analyzer.FileName} (depth {_state.NavigationStack.Count + 1})"
                    : _state.Analyzer.FileName).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(80, 80, 100))),
                bar.Spacer(),
                bar.Section(_state.Analyzer.Architecture).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(140, 120, 40))),
                bar.Separator(" | "),
                bar.Section(_state.FormatSizeToggleable(_state.Analyzer.FileSize)).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(140, 120, 40)))
            ]),

            // Tab bar — uses TabPanel with empty content and fixed height so
            // TabPanelNode (which is focusable) handles mouse clicks on tab headers.
            outer.TabPanel(tp =>
            [
                tp.Tab("General", _ => []).Selected(_state.CurrentTab == TabId.General),
                tp.Tab("PE/Metadata", _ => []).Selected(_state.CurrentTab == TabId.PeMetadata),
                tp.Tab("IL Inspector", _ => []).Selected(_state.CurrentTab == TabId.IlInspector),
                tp.Tab("Strings", _ => []).Selected(_state.CurrentTab == TabId.Strings),
                tp.Tab("Hex Dump", _ => []).Selected(_state.CurrentTab == TabId.HexDump),
                tp.Tab("Dep Graph", _ => []).Selected(_state.CurrentTab == TabId.DepGraph),
                tp.Tab("Size Map", _ => []).Selected(_state.CurrentTab == TabId.SizeMap),
                tp.Tab("Dynamic", _ => []).Selected(_state.CurrentTab == TabId.Dynamic)
            ])
            .OnSelectionChanged(e =>
            {
                SelectTab(e.SelectedIndex);
                SeedFocusedRowIfNeeded();
                RequestContentFocus();
                _state.App.Invalidate();
            })
            .Full()
            .FixedHeight(3),

            // Tab content — Responsive preserves the IL EditorNode across tab switches.
            // Non-IL tabs use Otherwise so only the active tab builds its full widget tree.
            outer.Responsive(r =>
            [
                // IL Inspector always reconciles to preserve EditorNode scroll state
                r.When((_, _) => _state.CurrentTab == TabId.IlInspector,
                    x => x.VStack(v => [IlInspectorView.Build(v, _state)]).Fill()),
                // All other tabs share a single branch — only the active one builds
                r.Otherwise(x => x.VStack(v => [BuildActiveNonIlTab(v, _state)]).Fill())
            ]).Fill(),

            // Keybinding hints bar
            BuildHintsBar(outer)
        ])
        .WithInputBindings(bindings =>
        {
            var currentSearch = _state.Search[_state.CurrentTab];
            var isSearchEditing = currentSearch.IsActive && !currentSearch.IsConfirmed;

            // VimReset wraps all Global bindings to cancel pending vim text-object sequences
            Action<InputBindingActionContext> VimReset(Action<InputBindingActionContext> action)
                => ctx => { _state.VimPending = VimMotionState.Idle; action(ctx); };

            // Number keys 1-8, s, q suppressed during search editing, jump dialog,
            // or hex insert mode to let EditorNode/TextBox receive character input
            var hexInsertMode = _state.CurrentTab == TabId.HexDump && _state.HexMode == HexEditMode.Insert;
            if (!isSearchEditing && !_state.HexJumpDialogOpen && !hexInsertMode
                && !_state.ApphostDialogOpen)
            {
                for (var i = 0; i < 8; i++)
                {
                    var tabIndex = i;
                    var key = (Hex1bKey)((int)Hex1bKey.D1 + i);
                    bindings.Key(key).Global().Action(VimReset(_ =>
                    {
                        SelectTab(tabIndex);
                        SeedFocusedRowIfNeeded();
                        RequestContentFocus();
                        _state.App.Invalidate();
                    }), $"Tab {tabIndex + 1}");
                }

                // Suppress size toggle on Dynamic Events sub-tab (S = Socket filter)
                // and hex tab in insert mode (S = byte input)
                var suppressSizeToggle = (_state.CurrentTab == TabId.Dynamic && _state.DynamicSubTab == DynamicSubTabId.Events)
                    || (_state.CurrentTab == TabId.HexDump && _state.HexMode == HexEditMode.Insert);
                if (!suppressSizeToggle)
                {
                    bindings.Key(Hex1bKey.S).Global().Action(VimReset(_ =>
                    {
                        _state.HumanReadableSizes = !_state.HumanReadableSizes;
                        _state.App.Invalidate();
                    }), "Toggle size format");
                }

                // Suppress Q quit in hex insert mode — let editor receive it as byte input
                if (!(_state.CurrentTab == TabId.HexDump && _state.HexMode == HexEditMode.Insert))
                    bindings.Key(Hex1bKey.Q).Global().Action(VimReset(ctx => ctx.RequestStop()), "Quit");

                // Universal yank — works on all tabs with neovim-style behavior
                bindings.Key(Hex1bKey.Y).Global().Action(ctx =>
                {
                    // Timeout check — reset stale vim pending state
                    if (_state.VimPending != VimMotionState.Idle
                        && (DateTime.UtcNow - _state.VimPendingTimestamp).TotalSeconds > 1.0)
                        _state.VimPending = VimMotionState.Idle;

                    // 1. yy: second y while already armed → yank entire line
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

                    // 2. Any focused editor with selection
                    if (ctx.FocusedNode is EditorNode { State.Cursor.HasSelection: true } editor)
                    {
                        _state.VimPending = VimMotionState.Idle;
                        PerformEditorYank(ctx, editor);
                        return;
                    }

                    // 3. Focused editor WITHOUT selection → arm operator-pending for yiw/yiW/yy
                    if (ctx.FocusedNode is EditorNode noSelEditor)
                    {
                        // Don't arm on hex dump normal mode when the hex editor is focused
                        // (I conflicts with Insert). Allow arming when the data interp editor is focused.
                        if (_state.CurrentTab == TabId.HexDump && _state.HexMode == HexEditMode.Normal
                            && noSelEditor.State == _state.HexEditorState)
                        {
                            _state.VimPending = VimMotionState.Idle;
                            return;
                        }
                        _state.VimPending = VimMotionState.WaitingForYMotion;
                        _state.VimPendingEditor = noSelEditor.State;
                        _state.VimPendingCursorOffset = noSelEditor.State.Cursor.Position.Value;
                        _state.VimPendingTimestamp = DateTime.UtcNow;
                        return;
                    }

                    // 3. Non-editor focus → table row / surface node
                    _state.VimPending = VimMotionState.Idle;
                    var yankText = YankHelper.GetYankText(_state);
                    if (yankText is not null)
                    {
                        ctx.CopyToClipboard(yankText);
                        ShowYankNotification(yankText);

                        // Flash the focused row (150ms)
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

            // Global search toggle — array-indexed, no switch statement
            // OemQuestion works through Hex1b automation/testing (TextInputStep maps '/' to OemQuestion).
            // In real terminals, '/' maps to Hex1bKey.None because the terminal driver doesn't
            // map punctuation to specific key codes. We register a Hex1bKey.None binding as fallback,
            // but only when search is not in editing state to avoid intercepting TextBox input.
            void SearchToggle()
            {
                _state.Search[_state.CurrentTab].ActivateOrCycle();
                var s = _state.Search[_state.CurrentTab];
                if (s.IsActive && !s.IsConfirmed)
                    _state.App.RequestFocus(node => node is TextBoxNode);
                _state.App.Invalidate();
            }
            var detailPopupOpen = _state.PeDetailContent is not null || _state.StringsDetailContent is not null;
            if (!_state.HexJumpDialogOpen && !detailPopupOpen && !_state.ApphostDialogOpen)
            {
                bindings.Key(Hex1bKey.OemQuestion).Global().OverridesCapture().Action(VimReset(_ => SearchToggle()), "Search");
                if (!isSearchEditing)
                {
                    bindings.Key(Hex1bKey.None).Global().OverridesCapture().Action(VimReset(_ => SearchToggle()), "Search");
                }
            }
            if (isSearchEditing)
            {
                bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(VimReset(_ =>
                {
                    if (!string.IsNullOrEmpty(currentSearch.Query))
                    {
                        // Hex dump tab: execute byte search on confirm
                        if (_state.CurrentTab == TabId.HexDump)
                            Views.HexDumpView.ExecuteSearch(_state);
                        currentSearch.Confirm();
                        // Restore focus from the search TextBox back to the content area
                        RequestContentFocus();
                        _state.App.Invalidate();
                    }
                }), "Confirm search");
            }

            // Apphost dialog Enter — navigate to companion managed .dll.
            // Registered as Global so it fires regardless of focus position
            // (the dialog overlay is in a ZStack layer separate from the main content).
            if (_state.ApphostDialogOpen && _state.ApphostCompanionDllPath is not null)
            {
                bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(VimReset(_ =>
                {
                    _state.ApphostDialogOpen = false;
                    _state.PushAssembly(_state.ApphostCompanionDllPath);
                    SeedFocusedRowIfNeeded();
                    RequestContentFocus();
                    _state.App.Invalidate();
                }), "Open managed DLL");
            }

            // Hex + IL Inspector keybindings (shared with NuGetApp).
            // Suppressed while the apphost dialog is open to prevent background shortcuts.
            if (!isSearchEditing && !_state.ApphostDialogOpen)
                DllInspectorBindings.Register(bindings, _state, _state.App,
                    resetVimPending: () => _state.VimPending = VimMotionState.Idle);

            // Ctrl+S: Save hex changes — only in normal mode with pending edits
            if (_state.CurrentTab == TabId.HexDump
                && _state.HexMode == HexEditMode.Normal
                && _state.HexIsDirty)
            {
                bindings.Ctrl().Key(Hex1bKey.S).Global().OverridesCapture().Action(VimReset(_ =>
                {
                    SaveHexChanges(_state);
                    _state.App.Invalidate();
                    ScheduleNotificationClear(_state);
                }), "Save hex changes");
            }

            // n/N only registered when search is confirmed
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

            // Global Escape to dismiss search (editing or confirmed) — must be
            // Global so it fires before built-in widget Escape bindings
            // (ScrollPanel FocusFirst, EditorNode HandleEscape, etc.) that
            // would otherwise consume the key in the focus-based routing walk.
            if (currentSearch.IsActive && !_state.HexJumpDialogOpen)
            {
                bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(VimReset(_ =>
                {
                    // In hex insert mode, Esc exits insert first — search stays active
                    if (_state.CurrentTab == TabId.HexDump && _state.HexMode == HexEditMode.Insert)
                    {
                        _state.HexMode = HexEditMode.Normal;
                        _state.HexEditorState.IsReadOnly = true;
                        _state.App.Invalidate();
                        return;
                    }
                    currentSearch.Dismiss();
                    // Hex tab has additional match state to clear
                    if (_state.CurrentTab == TabId.HexDump)
                    {
                        _state.HexMatchOffsets = [];
                        _state.HexCurrentMatchIndex = -1;
                        _state.HexMatchPatternLength = 0;
                        _state.HexLastSearchQuery = null;
                        _state.HexLiveSearchTooSlow = false;
                    }
                    RequestContentFocus();
                    _state.App.Invalidate();
                }), "Clear search");
            }

            // Hex insert mode without search: Global Escape to exit insert mode —
            // preempts EditorNode's built-in Escape binding.
            if (!currentSearch.IsActive
                && _state.CurrentTab == TabId.HexDump
                && _state.HexMode == HexEditMode.Insert
                && !_state.HexJumpDialogOpen)
            {
                bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(VimReset(_ =>
                {
                    _state.HexMode = HexEditMode.Normal;
                    _state.HexEditorState.IsReadOnly = true;
                    _state.App.Invalidate();
                }), "Exit insert mode");
            }

            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(VimReset(ctx => ctx.RequestStop()), "Quit");

            // Unified Escape: back navigation + IL selection clear in ONE binding.
            // Single Global Escape prevents conflicts between clear-selection and back-nav.
            var hasBackTarget = _state.NavigationStack.Count > 0
                || _state.CrossViewBackTarget is not null
                || _state.IlBackStack.Count > 0;
            var hasIlSelection = _state.CurrentTab == TabId.IlInspector
                && _state.IlEditorState?.Cursor.HasSelection == true;
            var sizeMapUsesEsc = _state.CurrentTab == TabId.SizeMap && _state.TreemapBreadcrumb.Count > 0;
            var dynamicFilterActive = _state.CurrentTab == TabId.Dynamic
                && _state.DynamicSubTab == DynamicSubTabId.Events
                && _state.DynamicCategoryFilter is not null;
            if ((hasBackTarget || hasIlSelection) && !sizeMapUsesEsc && !dynamicFilterActive
                && !currentSearch.IsActive && !_state.HexJumpDialogOpen && !hexInsertMode
                && !_state.ApphostDialogOpen && !_state.DynamicEditingArgs
                && _state.PeDetailContent is null && _state.StringsDetailContent is null)
            {
                bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(VimReset(_ =>
                {
                    // Priority 1: IL go-to-definition back
                    if (_state.CurrentTab == TabId.IlInspector && _state.IlBackStack.Count > 0)
                    {
                        var entry = _state.IlBackStack.Pop();
                        _state.RestoreFromIlBackEntry(entry);
                    }
                    // Priority 2: Cross-view back
                    else if (_state.CrossViewBackTarget is not null)
                    {
                        _state.NavigateBack();
                    }
                    // Priority 3: Assembly stack pop
                    else if (_state.NavigationStack.Count > 0)
                    {
                        var backTab = _state.PopAssembly();
                        if (_state.ApphostCompanionDllPath is not null && !_state.Analyzer.HasMetadata)
                            _state.ApphostDialogOpen = true;
                        _state.NavigateToTab(backTab);
                        _state.RequestContentFocus();
                        _state.App.Invalidate();
                    }
                    // Priority 4: IL selection clear (only when no navigation targets)
                    else if (_state.CurrentTab == TabId.IlInspector
                        && _state.IlEditorState?.Cursor.HasSelection == true)
                    {
                        _state.IlEditorState.Cursor.SelectionAnchor = null;
                        _state.App.Invalidate();
                    }
                }), "Back");
            }
        });

        // Apphost dialog overlay
        if (_state.ApphostDialogOpen && _state.ApphostCompanionDllPath is not null)
        {
            var dllName = Path.GetFileName(_state.ApphostCompanionDllPath);
            return ctx.ZStack(z =>
            [
                mainContent,
                z.Backdrop(
                    z.Border(
                        z.VStack(dlg =>
                        [
                            dlg.Text(""),
                            dlg.Text("  This file is a native apphost executable."),
                            dlg.Text("  It has no .NET metadata to inspect."),
                            dlg.Text(""),
                            dlg.Text("  A managed assembly was found:"),
                            dlg.Text($"  {dllName}"),
                            dlg.Text(""),
                            dlg.Text("  Open the managed .dll instead?"),
                            dlg.Text(""),
                            dlg.Text("  Enter: Yes | Esc: No, keep .exe")
                        ])
                    ).Title(" Apphost Detected ").FixedWidth(55).FixedHeight(12)
                    .WithInputBindings(bindings =>
                    {
                        bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
                        {
                            _state.ApphostDialogOpen = false;
                            SeedFocusedRowIfNeeded();
                            RequestContentFocus();
                            _state.App.Invalidate();
                        }, "Keep .exe");
                    })
                ).OnClickAway(() =>
                {
                    _state.ApphostDialogOpen = false;
                    SeedFocusedRowIfNeeded();
                    RequestContentFocus();
                    _state.App.Invalidate();
                })
            ]).Fill();
        }

        return mainContent;
    }

    private void SelectTab(int tabIndex)
    {
        _state.NavigateToTab(tabIndex);
    }

    /// <summary>
    /// Requests focus on the appropriate content node for the current tab.
    /// IL tab targets the ListNode tree; all other tabs target any content node including TableNode.
    /// </summary>
    private void RequestContentFocus() => _state.RequestContentFocus();

    /// <summary>
    /// Seeds the focused row key for table-backed tabs so Enter works immediately
    /// without requiring DownArrow first.
    /// </summary>
    private void SeedFocusedRowIfNeeded()
    {
        switch (_state.CurrentTab)
        {
            case TabId.General when _state.GeneralFocusedDep is null && _state.Analyzer.AssemblyRefs.Count > 0:
                _state.GeneralFocusedDep = _state.Analyzer.AssemblyRefs[0].Name;
                break;
            case TabId.PeMetadata when _state.PeFocusedKey is null:
                _state.PeFocusedKey = _state.Analyzer.Sections.Count > 0
                    ? _state.Analyzer.Sections[0].Name
                    : null;
                break;
            case TabId.Strings when _state.StringsFocusedKey is null:
                var strings = _state.GetActiveStrings();
                if (strings.Count > 0)
                    _state.StringsFocusedKey = $"{strings[0].Offset}:{strings[0].Source}";
                break;
        }
    }

    /// <summary>
    /// Builds the active non-IL tab content. Only one tab builds per frame.
    /// </summary>
    private static Hex1bWidget BuildActiveNonIlTab(WidgetContext<VStackWidget> ctx, DotsiderState state) =>
        state.CurrentTab switch
        {
            TabId.General => GeneralView.Build(ctx, state),
            TabId.PeMetadata => PeMetadataView.Build(ctx, state),
            TabId.Strings => StringsView.Build(ctx, state),
            TabId.HexDump => HexDumpView.Build(ctx, state),
            TabId.DepGraph => DependencyGraphView.Build(ctx, state),
            TabId.SizeMap => SizeTreemapView.Build(ctx, state),
            TabId.Dynamic => DynamicAnalysisView.Build(ctx, state),
            _ => ctx.Text("").Fill()
        };

    private InfoBarWidget BuildHintsBar(WidgetContext<VStackWidget> ctx) =>
        ctx.InfoBar(s =>
        {
            var hints = new List<IInfoBarChild>();

            // Hex tab: neovim-style mode indicator (left side, only in insert mode)
            if (_state.CurrentTab == 4 && _state.HexMode == HexEditMode.Insert)
            {
                hints.Add(s.Section("-- INSERT --").Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 120, 80))));
                hints.Add(s.Separator(" "));
            }

            hints.Add(s.Section("1-8: Tabs"));

            if (_state.NavigationStack.Count > 0 || _state.IlBackStack.Count > 0)
                hints.Add(s.Section("Esc: Back"));

            if (_state.CurrentTab == 1)
            {
                hints.Add(s.Section("Enter: Detail"));
                if (_state.PeSubTab is PeSubTabId.TypeDef or PeSubTabId.MethodDef)
                    hints.Add(s.Section("g: Go to IL"));
            }
            else if (_state.CurrentTab == 2)
            {
                if (_state.IlSelectedMethod is not null)
                    hints.Add(s.Section("Enter/gd: Go to def"));
                hints.Add(s.Section("l: Focus IL"));
                if (_state.IlSelectedMethod is { Rva: > 0 })
                    hints.Add(s.Section("x: Hex"));
                if (_state.IlEditorState?.Cursor.HasSelection == true)
                    hints.Add(s.Section("y: Yank (IL)"));
            }
            else if (_state.CurrentTab == 3)
                hints.Add(s.Section("Enter: Detail"));
            else if (_state.CurrentTab == 4)
            {
                if (_state.HexMode == HexEditMode.Insert)
                    hints.Add(s.Section("Esc: Normal"));
                else
                {
                    var hexHints = "i: Edit | g: Jump | e: Endian";
                    if (_state.HexIsDirty)
                        hexHints += " | Ctrl+S: Save";
                    hints.Add(s.Section(hexHints));
                }
            }
            else if (_state.CurrentTab == 5)
                hints.Add(s.Section("←→: Select | Enter: Open"));
            else if (_state.CurrentTab == 6)
                hints.Add(s.Section("Enter: Drill | ←→: Select | Esc: Up"));
            else if (_state.CurrentTab == 7)
            {
                if (_state.Tracer?.ProcessState == TraceProcessState.Running)
                    hints.Add(s.Section("Ctrl+K: Stop"));
                else if (_state.Tracer?.ProcessState is TraceProcessState.Exited
                    or TraceProcessState.Error)
                {
                    var dynamicSearch = _state.Search[TabId.Dynamic];
                    var isSearchEditing = dynamicSearch.IsActive && !dynamicSearch.IsConfirmed;
                    var hint = !isSearchEditing
                        && _state.DynamicSubTab == DynamicSubTabId.Events
                        && _state.CanNavigateJitEvent
                        ? "Enter: Go to IL"
                        : "Enter: Re-run";
                    hints.Add(s.Section(hint));
                }
                else if ((_state.HasEntryPoint || _state.IsNativeAot) && !_state.IsNetFramework)
                    hints.Add(s.Section("Enter: Launch"));
            }

            // Cross-view back hint
            if (_state.CrossViewBackTarget is not null)
                hints.Add(s.Section("Esc: Back"));

            // Search hint — always available on all tabs
            var currentSearch = _state.Search[_state.CurrentTab];
            if (currentSearch.IsActive)
                hints.Add(s.Section("Esc: Clear"));
            hints.Add(s.Section("/: Search"));

            // Show size toggle hint only on tabs that display sizes
            if (_state.CurrentTab is 0 or 1 or 6) // General, PE/Metadata, Size Map
                hints.Add(s.Section(_state.HumanReadableSizes ? "s: Sizes (dec)" : "s: Sizes (hex)"));

            // y: Yank hint — show when yankable content exists
            var yankable = _state.CurrentTab switch
            {
                TabId.General => _state.GeneralFocusedDep is not null,
                TabId.PeMetadata => _state.PeDetailContent is not null || _state.PeFocusedKey is not null,
                TabId.IlInspector => false, // shown separately above as "y: Yank (IL)"
                TabId.Strings => _state.StringsDetailContent is not null || _state.StringsFocusedKey is not null,
                TabId.HexDump => true,
                TabId.DepGraph => _state.GraphSelectedNode is not null || _state.GraphSelectedIndex >= 0,
                TabId.SizeMap => _state.TreemapHoveredItem is not null || _state.TreemapSelectedIndex >= 0,
                TabId.Dynamic => _state.DynamicSubTab switch
                {
                    DynamicSubTabId.Events => _state.DynamicEventsFocusedKey is not null,
                    DynamicSubTabId.Output => _state.DynamicOutputFocusedKey is not null,
                    DynamicSubTabId.Counters => _state.DynamicMemoryEditorState is not null,
                    DynamicSubTabId.Summary => _state.DynamicSummaryEditorState is not null,
                    _ => false
                },
                _ => false
            };
            if (yankable)
                hints.Add(s.Section("y: Yank"));

            // iw/iW hint — show when a read-only editor is focused (not hex dump)
            try
            {
                if (_state.App.FocusedNode is EditorNode
                    && _state.CurrentTab != TabId.HexDump)
                    hints.Add(s.Section("V: Line | iw: Word | iW: WORD"));
            }
            catch (NullReferenceException)
            {
                // Focus ring not yet initialized
            }

            hints.Add(s.Spacer());

            // Yank notification (right side, auto-clearing)
            if (!string.IsNullOrEmpty(_state.YankNotification))
            {
                hints.Add(s.Section(_state.YankNotification).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(120, 180, 120))));
                hints.Add(s.Separator(" "));
            }

            // Transient notice (right side, all tabs)
            if (!string.IsNullOrEmpty(_state.TransientNotice))
            {
                hints.Add(s.Section(_state.TransientNotice).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 80, 60))));
                hints.Add(s.Separator(" "));
            }

            // General tab: navigation error (right side)
            if (_state.CurrentTab == 0 && !string.IsNullOrEmpty(_state.NavigationError))
            {
                hints.Add(s.Section(_state.NavigationError).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 80, 60))));
                hints.Add(s.Separator(" "));
            }

            // Hex tab: vim-style save notification (right side)
            if (_state.CurrentTab == 4 && !string.IsNullOrEmpty(_state.HexNotification))
            {
                hints.Add(s.Section(_state.HexNotification).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(120, 110, 30))));
                hints.Add(s.Separator(" "));
            }

            hints.Add(s.Section("q: Quit"));
            return hints;
        }).WithDefaultSeparator(" | ");

    /// <summary>Delegate for the reopen-or-fallback step, injectable for testing.</summary>
    internal delegate (AssemblyAnalyzer Analyzer, string? ResolvedPath) ReopenFunc(
        string[] candidatePaths, byte[] recoveryBytes, string filePath);

    internal static void SaveHexChanges(DotsiderState state, ReopenFunc? reopener = null)
    {
        reopener ??= ReopenOrFallback;
        var filePath = state.Analyzer.FilePath;
        var tempPath = filePath + ".tmp";

        // Phase 1: write and validate. state.Analyzer is still live here,
        // so any exception is safe — just report and return.
        byte[] newBytes;
        try
        {
            newBytes = state.HexEditorState.Document.GetBytes().ToArray();
            File.WriteAllBytes(tempPath, newBytes);
        }
        catch (Exception ex)
        {
            state.HexNotification = $"Save failed: {ex.Message}";
            return;
        }

        try
        {
            using var validator = new AssemblyAnalyzer(tempPath);
        }
        catch (Exception ex)
        {
            try { File.Delete(tempPath); } catch { }
            state.HexNotification = $"Cannot save: invalid image — {ex.Message}";
            return;
        }

        // Phase 2: replace analyzer. After Dispose(), every path must
        // commit a live replacement before returning — no exceptions
        // may propagate without first restoring state.Analyzer.
        state.Analyzer.Dispose();

        // Move temp → original. If move fails, the file is still at tempPath.
        string savedPath;
        try { File.Move(tempPath, filePath, overwrite: true); savedPath = filePath; }
        catch { savedPath = tempPath; }

        // Try reopening from the saved path, then alt path, then recovery.
        string[] candidates =
        [
            savedPath,
            savedPath == filePath ? tempPath : filePath,
            filePath + ".recovery"
        ];

        var (analyzer, resolvedPath) = reopener(candidates, newBytes, filePath);
        CommitAnalyzer(state, analyzer);

        if (resolvedPath is null)
        {
            state.HexNotification = "Saved (working from memory — file may be locked)";
        }
        else
        {
            savedPath = resolvedPath;
            var fileName = Path.GetFileName(savedPath);
            var size = new FileInfo(savedPath).Length;
            state.HexNotification = savedPath == filePath
                ? $"\"{fileName}\" {size}B written"
                : $"Saved to {fileName} (could not overwrite original)";
        }
    }

    /// <summary>
    /// Tries each candidate path in order. If all fail, falls back to an
    /// in-memory analyzer constructed from <paramref name="recoveryBytes"/>.
    /// </summary>
    /// <returns>
    /// The opened analyzer and the resolved path, or <c>null</c> path if
    /// the in-memory fallback was used.
    /// </returns>
    internal static (AssemblyAnalyzer Analyzer, string? ResolvedPath) ReopenOrFallback(
        string[] candidatePaths, byte[] recoveryBytes, string filePath)
    {
        foreach (var path in candidatePaths)
        {
            try
            {
                if (path.EndsWith(".recovery") && !File.Exists(path))
                    File.WriteAllBytes(path, recoveryBytes);

                return (new AssemblyAnalyzer(path), path);
            }
            catch { /* try next candidate */ }
        }

        return (new AssemblyAnalyzer(recoveryBytes, filePath), null);
    }

    /// <summary>
    /// Atomically swaps state to use a new (already-constructed) analyzer.
    /// state.Analyzer is set first so it is never left pointing at a disposed instance.
    /// </summary>
    private static void CommitAnalyzer(DotsiderState state, AssemblyAnalyzer analyzer)
    {
        state.Analyzer = analyzer;
        state.StringExtractor = new StringExtractor(analyzer);
        state.IlDisassembler = analyzer.HasMetadata ? new IlDisassembler(analyzer) : null;
        var hexDoc = new HexRowDocument(new Hex1bDocument(analyzer.RawBytes.ToArray()));
        state.HexRowDoc = hexDoc;
        state.HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        state.HexCleanVersion = hexDoc.Version;
        state.HexMode = HexEditMode.Normal;
        state.CachedUserStrings = null;
        state.CachedMetadataStrings = null;
        state.CachedRawStrings = null;
        state.CachedGraph = null;
        state.CachedSizeTree = null;
        state.TreemapCurrentLevel = null;
        state.TreemapBreadcrumb.Clear();
        state.IlSelectedMethod = null;
        state.IlSelectedField = null;
        state.IlEditorMethod = null;
        state.IlEditorAnalyzer = null;
        state.IlEditorState = null;
        state.IlLastSearchQuery = null;
        state.IlSearchMatches = [];
        state.IlCurrentMatchIndex = -1;
        state.IlTextMatchMethodTokens = null;
        state.IlFocusedTreeKey = null;
        state.IlInstructions = null;
        state.IlHeaderLineCount = 0;
        state.IlNavigationProvider.Instructions = null;
        state.IlBackStack.Clear();
        state.IlGdPending = false;
        state.TransientNotice = null;
        state.GeneralInfoEditorState = null;
        state.GeneralInfoEditorText = null;
        state.PeHeadersEditorState = null;
        state.PeHeadersEditorText = null;
        state.ClrHeaderEditorState = null;
        state.ClrHeaderEditorText = null;
        state.DataInterpEditorState = null;
        state.DataInterpEditorText = null;
    }

    private IlYankDecorationProvider? FindYankProvider(EditorState editorState) =>
        YankHelper.FindYankProvider(_state, editorState);

    /// <summary>
    /// Performs a neovim-style yank on the focused editor's selection or current text-object range.
    /// Handles hex dump byte extraction, cursor collapse, flash, clipboard, and notification.
    /// </summary>
    private void PerformEditorYank(InputBindingActionContext ctx, EditorNode editor)
    {
        string text;
        if (editor.State == _state.HexEditorState)
        {
            // Hex dump: extract bytes as "4D 5A 90 00"
            text = YankHelper.GetHexSelectionText(editor.State) ?? "";
        }
        else
        {
            // All other editors: neovim-style yank
            // Include the cursor character (word-boundary adjustment)
            var range = editor.State.Cursor.SelectionRange;
            var doc = editor.State.Document;
            var yankEnd = new DocumentOffset(Math.Min(
                Math.Max(range.End.Value, editor.State.Cursor.Position.Value + 1),
                doc.Length));
            var yankRange = new DocumentRange(range.Start, yankEnd);
            text = doc.GetText(yankRange);

            // Collapse cursor to last character of yanked range
            var lastChar = new DocumentOffset(Math.Max(0, yankEnd.Value - 1));
            editor.State.SetCursorPosition(lastChar);

            // Flash the yanked range (150ms IncSearch style)
            var yankProvider = FindYankProvider(editor.State);
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

    private static void ScheduleNotificationClear(DotsiderState state)
    {
        _ = Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
        {
            state.HexNotification = null;
            state.App.Invalidate();
        }, TaskScheduler.Default);
    }
}
