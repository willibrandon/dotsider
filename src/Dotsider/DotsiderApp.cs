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

        // On first render, move focus from the tab bar into the content area
        if (!_initialFocusRequested)
        {
            _initialFocusRequested = true;
            RequestContentFocus();
        }


        return ctx.VStack(outer =>
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

            // Number keys 1-8, s, q suppressed during search editing, jump dialog,
            // or hex insert mode to let EditorNode/TextBox receive character input
            var hexInsertMode = _state.CurrentTab == TabId.HexDump && _state.HexMode == HexEditMode.Insert;
            if (!isSearchEditing && !_state.HexJumpDialogOpen && !hexInsertMode)
            {
                for (var i = 0; i < 8; i++)
                {
                    var tabIndex = i;
                    var key = (Hex1bKey)((int)Hex1bKey.D1 + i);
                    bindings.Key(key).Global().Action(_ =>
                    {
                        SelectTab(tabIndex);
                        SeedFocusedRowIfNeeded();
                        RequestContentFocus();
                        _state.App.Invalidate();
                    }, $"Tab {tabIndex + 1}");
                }

                // Suppress size toggle on Dynamic Events sub-tab (S = Socket filter)
                // and hex tab in insert mode (S = byte input)
                var suppressSizeToggle = (_state.CurrentTab == TabId.Dynamic && _state.DynamicSubTab == DynamicSubTabId.Events)
                    || (_state.CurrentTab == TabId.HexDump && _state.HexMode == HexEditMode.Insert);
                if (!suppressSizeToggle)
                {
                    bindings.Key(Hex1bKey.S).Global().Action(_ =>
                    {
                        _state.HumanReadableSizes = !_state.HumanReadableSizes;
                        _state.App.Invalidate();
                    }, "Toggle size format");
                }

                // Suppress Q quit in hex insert mode — let editor receive it as byte input
                if (!(_state.CurrentTab == TabId.HexDump && _state.HexMode == HexEditMode.Insert))
                    bindings.Key(Hex1bKey.Q).Global().Action(ctx => ctx.RequestStop(), "Quit");
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
            if (!_state.HexJumpDialogOpen && !detailPopupOpen)
            {
                bindings.Key(Hex1bKey.OemQuestion).Global().OverridesCapture().Action(_ => SearchToggle(), "Search");
                if (!isSearchEditing)
                {
                    bindings.Key(Hex1bKey.None).Global().OverridesCapture().Action(_ => SearchToggle(), "Search");
                }
            }
            if (isSearchEditing)
            {
                bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(_ =>
                {
                    if (!string.IsNullOrEmpty(currentSearch.Query))
                    {
                        // Hex dump tab: execute byte search on confirm
                        if (_state.CurrentTab == TabId.HexDump)
                            Views.HexDumpView.ExecuteSearch(_state);
                        currentSearch.Confirm();
                        // Restore focus from the search TextBox back to the content area.
                        // Restore focus from the search TextBox back to the content area
                        RequestContentFocus();
                        _state.App.Invalidate();
                    }
                }, "Confirm search");
            }

            // Hex tab keybindings — normal mode only, registered global because
            // EditorNode's AnyCharacter() binding consumes letter keys in path-based routing
            if (!isSearchEditing && !_state.HexJumpDialogOpen
                && _state.CurrentTab == TabId.HexDump
                && _state.HexMode == HexEditMode.Normal)
            {
                bindings.Key(Hex1bKey.I).Global().Action(_ =>
                {
                    _state.HexMode = HexEditMode.Insert;
                    _state.HexEditorState.IsReadOnly = false;
                    _state.HexNotification = null;
                    _state.App.Invalidate();
                }, "Insert mode");

                bindings.Key(Hex1bKey.G).Global().Action(_ =>
                {
                    _state.HexJumpDialogOpen = true;
                    _state.HexJumpInput = "";
                    _state.HexNotification = null;
                    _state.App.RequestFocus(node => node is TextBoxNode);
                    _state.App.Invalidate();
                }, "Jump to offset");

                bindings.Key(Hex1bKey.E).Global().Action(_ =>
                {
                    _state.HexEndianness = _state.HexEndianness == HexEndianness.Little
                        ? HexEndianness.Big : HexEndianness.Little;
                    _state.App.Invalidate();
                }, "Toggle endianness");

                bindings.Key(Hex1bKey.H).Global().Action(_ =>
                {
                    _state.HexEditorState.MoveCursor(CursorDirection.Left);
                    _state.App.Invalidate();
                }, "Left");
                bindings.Key(Hex1bKey.L).Global().Action(_ =>
                {
                    _state.HexEditorState.MoveCursor(CursorDirection.Right);
                    _state.App.Invalidate();
                }, "Right");
                bindings.Key(Hex1bKey.K).Global().Action(_ =>
                {
                    _state.HexEditorState.MoveCursor(CursorDirection.Up);
                    _state.App.Invalidate();
                }, "Up");
                bindings.Key(Hex1bKey.J).Global().Action(_ =>
                {
                    _state.HexEditorState.MoveCursor(CursorDirection.Down);
                    _state.App.Invalidate();
                }, "Down");
            }

            // Jump dialog Enter — global so it fires above TextBox capture
            if (_state.HexJumpDialogOpen)
            {
                bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(_ =>
                {
                    Views.HexDumpView.ProcessJumpInput(_state);
                    _state.App.Invalidate();
                }, "Jump");
            }

            // Ctrl+S: Save hex changes — only in normal mode with pending edits
            if (_state.CurrentTab == TabId.HexDump
                && _state.HexMode == HexEditMode.Normal
                && _state.HexIsDirty)
            {
                bindings.Ctrl().Key(Hex1bKey.S).Global().OverridesCapture().Action(_ =>
                {
                    SaveHexChanges(_state);
                    _state.App.Invalidate();
                    ScheduleNotificationClear(_state);
                }, "Save hex changes");
            }

            // IL Inspector tab keybindings — Global because EditorNode's AnyCharacter() consumes letters
            if (!isSearchEditing && _state.CurrentTab == TabId.IlInspector)
            {
                // Escape clears editor selection — only when no search is active
                // to avoid conflicting with the global search Escape binding
                if (!currentSearch.IsActive && _state.IlEditorState?.Cursor.HasSelection == true)
                {
                    bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
                    {
                        _state.IlEditorState.Cursor.SelectionAnchor = null;
                        _state.App.Invalidate();
                    }, "Clear selection");
                }
                if (_state.IlSelectedMethod is { Rva: > 0 } ilMethod)
                {
                    bindings.Key(Hex1bKey.X).Global().Action(_ =>
                    {
                        _state.NavigateToHexOffset(ilMethod.Rva);
                    }, "View in hex");
                }

                bindings.Key(Hex1bKey.L).Global().Action(_ =>
                {
                    _state.App.RequestFocus(node => node is EditorNode);
                    _state.App.Invalidate();
                }, "Focus IL");

                bindings.Key(Hex1bKey.Y).Global().Action(ctx =>
                {
                    if (_state.IlEditorState?.Cursor.HasSelection == true)
                    {
                        var range = _state.IlEditorState.Cursor.SelectionRange;
                        // Include the cursor character — after word-boundary adjustment
                        // the cursor sits on the last word char outside the selection range
                        var doc = _state.IlEditorState.Document;
                        var yankEnd = new DocumentOffset(Math.Min(
                            Math.Max(range.End.Value, _state.IlEditorState.Cursor.Position.Value + 1),
                            doc.Length));
                        var yankRange = new DocumentRange(range.Start, yankEnd);
                        var text = doc.GetText(yankRange);
                        ctx.CopyToClipboard(text);

                        // Collapse cursor to last character of yanked range (neovim behavior)
                        var lastChar = new DocumentOffset(Math.Max(0, yankEnd.Value - 1));
                        _state.IlEditorState.SetCursorPosition(lastChar);

                        // Flash the yanked range (neovim IncSearch style, 150ms)
                        var startPos = doc.OffsetToPosition(yankRange.Start);
                        var endPos = doc.OffsetToPosition(yankRange.End);
                        _state.IlYankProvider.HighlightRange = (startPos, endPos);
                        _state.App.Invalidate();
                        _ = Task.Delay(TimeSpan.FromMilliseconds(150)).ContinueWith(_ =>
                        {
                            _state.IlYankProvider.HighlightRange = null;
                            _state.App.Invalidate();
                        }, TaskScheduler.Default);
                    }
                }, "Yank");
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

            // Global Escape to dismiss search (editing or confirmed) — must be
            // Global so it fires before built-in widget Escape bindings
            // (ScrollPanel FocusFirst, EditorNode HandleEscape, etc.) that
            // would otherwise consume the key in the focus-based routing walk.
            if (currentSearch.IsActive && !_state.HexJumpDialogOpen)
            {
                bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
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
                }, "Clear search");
            }

            // Hex insert mode without search: Global Escape to exit insert mode —
            // preempts EditorNode's built-in Escape binding.
            if (!currentSearch.IsActive
                && _state.CurrentTab == TabId.HexDump
                && _state.HexMode == HexEditMode.Insert
                && !_state.HexJumpDialogOpen)
            {
                bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
                {
                    _state.HexMode = HexEditMode.Normal;
                    _state.HexEditorState.IsReadOnly = true;
                    _state.App.Invalidate();
                }, "Exit insert mode");
            }

            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(ctx => ctx.RequestStop(), "Quit");

            // Cross-view back navigation — suppressed when any text input is active
            // (search editing, hex jump dialog, hex insert mode, dynamic args editing)
            // or when the current tab locally consumes Backspace
            var tabUsesBackspace = (_state.CurrentTab == TabId.General && _state.NavigationStack.Count > 0)
                || (_state.CurrentTab == TabId.SizeMap && _state.TreemapBreadcrumb.Count > 0);
            if (!tabUsesBackspace && !isSearchEditing && !_state.HexJumpDialogOpen && !hexInsertMode
                && !_state.DynamicEditingArgs && _state.CrossViewBackTarget is not null)
            {
                bindings.Key(Hex1bKey.Backspace).Global().Action(_ =>
                {
                    _state.NavigateBack();
                }, "Back");
            }
        });
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

            if (_state.NavigationStack.Count > 0)
                hints.Add(s.Section("Backspace: Back"));

            if (_state.CurrentTab == 1)
            {
                hints.Add(s.Section("Enter: Detail"));
                if (_state.PeSubTab is PeSubTabId.TypeDef or PeSubTabId.MethodDef)
                    hints.Add(s.Section("g: Go to IL"));
            }
            else if (_state.CurrentTab == 2)
            {
                hints.Add(s.Section("l: Focus IL"));
                if (_state.IlSelectedMethod is { Rva: > 0 })
                    hints.Add(s.Section("x: Hex"));
                if (_state.IlEditorState?.Cursor.HasSelection == true)
                    hints.Add(s.Section("y: Yank"));
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
                hints.Add(s.Section("Enter: Drill | ←→: Select | Backspace: Up"));
            else if (_state.CurrentTab == 7)
            {
                if (_state.Tracer?.ProcessState == TraceProcessState.Running)
                    hints.Add(s.Section("Ctrl+K: Stop"));
                else if (_state.Tracer?.ProcessState is TraceProcessState.Exited
                    or TraceProcessState.Error)
                    hints.Add(s.Section("Enter: Re-run"));
                else if (_state.HasEntryPoint)
                    hints.Add(s.Section("Enter: Launch"));
            }

            // Cross-view back hint
            if (_state.CrossViewBackTarget is not null)
                hints.Add(s.Section("Backspace: Back"));

            // Search hint — always available on all tabs
            var currentSearch = _state.Search[_state.CurrentTab];
            if (currentSearch.IsActive)
                hints.Add(s.Section("Esc: Clear"));
            hints.Add(s.Section("/: Search"));

            // Show size toggle hint only on tabs that display sizes
            if (_state.CurrentTab is 0 or 1 or 6) // General, PE/Metadata, Size Map
                hints.Add(s.Section(_state.HumanReadableSizes ? "s: Sizes (dec)" : "s: Sizes (hex)"));

            hints.Add(s.Spacer());

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

    private static void SaveHexChanges(DotsiderState state)
    {
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

        foreach (var path in candidates)
        {
            try
            {
                // Recovery path needs the bytes written first
                if (path.EndsWith(".recovery") && !File.Exists(path))
                    File.WriteAllBytes(path, newBytes);

                CommitAnalyzer(state, new AssemblyAnalyzer(path));
                savedPath = path;

                var fileName = Path.GetFileName(savedPath);
                var size = new FileInfo(savedPath).Length;
                state.HexNotification = savedPath == filePath
                    ? $"\"{fileName}\" {size}B written"
                    : $"Saved to {fileName} (could not overwrite original)";
                return;
            }
            catch { /* try next candidate */ }
        }

        // All disk candidates exhausted. Fall back to in-memory analyzer
        // constructed from the validated bytes — no filesystem I/O required.
        CommitAnalyzer(state, new AssemblyAnalyzer(newBytes, filePath));
        state.HexNotification = "Saved (working from memory — file may be locked)";
    }

    /// <summary>
    /// Atomically swaps state to use a new (already-constructed) analyzer.
    /// state.Analyzer is set first so it is never left pointing at a disposed instance.
    /// </summary>
    private static void CommitAnalyzer(DotsiderState state, AssemblyAnalyzer analyzer)
    {
        state.Analyzer = analyzer;
        state.IlDisassembler = new IlDisassembler(analyzer);
        state.StringExtractor = new StringExtractor(analyzer);
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
        state.IlEditorMethod = null;
        state.IlEditorAnalyzer = null;
        state.IlEditorState = null;
        state.IlLastSearchQuery = null;
        state.IlSearchMatches = [];
        state.IlCurrentMatchIndex = -1;
        state.IlTextMatchMethodTokens = null;
        state.IlFocusedTreeKey = null;
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
