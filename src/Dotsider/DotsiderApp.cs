using Dotsider.Analysis;
using Dotsider.Views;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Nodes;
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
        // On first render, move focus from the tab bar into the content area
        if (!_initialFocusRequested)
        {
            _initialFocusRequested = true;
            _state.App.RequestFocus(node =>
                node is EditorNode or TreeNode or InteractableNode
                || node.GetType().Name.StartsWith("TableNode"));
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

            // Main content: Tab panel with 7 tabs (controlled via CurrentTab)
            outer.TabPanel(tp =>
            [
                tp.Tab("General", t => [GeneralView.Build(t, _state)])
                    .Selected(_state.CurrentTab == TabId.General),
                tp.Tab("PE/Metadata", t => [PeMetadataView.Build(t, _state)])
                    .Selected(_state.CurrentTab == TabId.PeMetadata),
                tp.Tab("IL Inspector", t => [IlInspectorView.Build(t, _state)])
                    .Selected(_state.CurrentTab == TabId.IlInspector),
                tp.Tab("Strings", t => [StringsView.Build(t, _state)])
                    .Selected(_state.CurrentTab == TabId.Strings),
                tp.Tab("Hex Dump", t => [HexDumpView.Build(t, _state)])
                    .Selected(_state.CurrentTab == TabId.HexDump),
                tp.Tab("Dep Graph", t => [DependencyGraphView.Build(t, _state)])
                    .Selected(_state.CurrentTab == TabId.DepGraph),
                tp.Tab("Size Map", t => [SizeTreemapView.Build(t, _state)])
                    .Selected(_state.CurrentTab == TabId.SizeMap),
                tp.Tab("Dynamic", t => [DynamicAnalysisView.Build(t, _state)])
                    .Selected(_state.CurrentTab == TabId.Dynamic)
            ])
            .OnSelectionChanged(e =>
            {
                SelectTab(e.SelectedIndex);
                _state.App.RequestFocus(node =>
                    node is EditorNode or TreeNode or InteractableNode
                    || node.GetType().Name.StartsWith("TableNode"));
                _state.App.Invalidate();
            })
            .Full()
            .Fill(),

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
                        // Move focus from tab bar into content so arrow keys work immediately
                        _state.App.RequestFocus(node =>
                            node is EditorNode or TreeNode or InteractableNode
                            || node.GetType().Name.StartsWith("TableNode"));
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
            Action searchToggle = () =>
            {
                _state.Search[_state.CurrentTab].ActivateOrCycle();
                var s = _state.Search[_state.CurrentTab];
                if (s.IsActive && !s.IsConfirmed)
                    _state.App.RequestFocus(node => node is TextBoxNode);
                _state.App.Invalidate();
            };
            if (!_state.HexJumpDialogOpen)
            {
                bindings.Key(Hex1bKey.OemQuestion).Global().OverridesCapture().Action(_ => searchToggle(), "Search");
                if (!isSearchEditing)
                {
                    bindings.Key(Hex1bKey.None).Global().OverridesCapture().Action(_ => searchToggle(), "Search");
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
                        // Restore focus from the search TextBox back to the content area
                        _state.App.RequestFocus(node =>
                            node is EditorNode or TreeNode or InteractableNode
                            || node.GetType().Name.StartsWith("TableNode"));
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

                // Global Escape to dismiss confirmed search — after confirmation the
                // TextBox is removed and EnsureFocus() moves focus to the main TabPanel,
                // which is outside each tab's VStack, so local Escape bindings won't fire.
                if (!_state.HexJumpDialogOpen)
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
                        _state.App.Invalidate();
                    }, "Clear search");
                }
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
                if (_state.IlSelectedMethod is { Rva: > 0 })
                    hints.Add(s.Section("x: Hex"));
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
                if (_state.Tracer?.ProcessState == Analysis.Models.TraceProcessState.Running)
                    hints.Add(s.Section("Ctrl+K: Stop"));
                else if (_state.Tracer?.ProcessState is Analysis.Models.TraceProcessState.Exited
                    or Analysis.Models.TraceProcessState.Error)
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
