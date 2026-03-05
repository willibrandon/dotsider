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
public sealed class DotsiderApp
{
    private readonly DotsiderState _state;
    private bool _initialFocusRequested;

    /// <summary>
    /// Creates a new dotsider application with the specified state.
    /// </summary>
    /// <param name="state">The application state holding the analyzer and all UI state.</param>
    public DotsiderApp(DotsiderState state)
    {
        _state = state;
    }

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
                node is EditorNode or TreeNode
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
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(180, 180, 200))),
                bar.Spacer(),
                bar.Section(_state.Analyzer.Architecture).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 180, 100))),
                bar.Separator(" | "),
                bar.Section(_state.FormatSizeToggleable(_state.Analyzer.FileSize)).Theme(t => t
                    .Set(GlobalTheme.ForegroundColor, Hex1bColor.FromRgb(200, 180, 100)))
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

            // Number keys 1-8, s, q suppressed during search editing or jump dialog to let TextBox receive input
            if (!isSearchEditing && !_state.HexJumpDialogOpen)
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
                            node is EditorNode or TreeNode
                            || node.GetType().Name.StartsWith("TableNode"));
                        _state.App.Invalidate();
                    }, $"Tab {tabIndex + 1}");
                }

                bindings.Key(Hex1bKey.S).Global().Action(_ =>
                {
                    _state.HumanReadableSizes = !_state.HumanReadableSizes;
                    _state.App.Invalidate();
                }, "Toggle size format");
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
                        _state.App.Invalidate();
                    }
                }, "Confirm search");
            }

            // Hex tab keybindings: g (jump), e (endianness) — only when not editing search or in jump dialog
            if (!isSearchEditing && !_state.HexJumpDialogOpen && _state.CurrentTab == TabId.HexDump)
            {
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

            // Ctrl+S: Save hex changes (hex tab only)
            if (_state.CurrentTab == TabId.HexDump)
            {
                bindings.Ctrl().Key(Hex1bKey.S).Global().OverridesCapture().Action(_ =>
                {
                    SaveHexChanges(_state);
                    _state.App.Invalidate();
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
                        currentSearch.Dismiss();
                        _state.App.Invalidate();
                    }, "Clear search");
                }
            }

            bindings.Ctrl().Key(Hex1bKey.C).Global().OverridesCapture()
                .Action(ctx => ctx.RequestStop(), "Quit");
        });
    }

    private void SelectTab(int tabIndex)
    {
        if (_state.CurrentTab == tabIndex) return;
        var previousTab = _state.CurrentTab;
        _state.CurrentTab = tabIndex;
        if (previousTab != TabId.IlInspector && tabIndex == TabId.IlInspector)
            _state.IlRestoreDisassemblyScroll = true;
    }

    private Hex1bWidget BuildHintsBar(WidgetContext<VStackWidget> ctx)
    {
        return ctx.InfoBar(s =>
        {
            var hints = new List<IInfoBarChild>();
            hints.Add(s.Section("1-8: Tabs"));

            if (_state.NavigationStack.Count > 0)
                hints.Add(s.Section("Backspace: Back"));

            if (_state.CurrentTab is 1 or 3)
                hints.Add(s.Section("Enter: Detail"));
            else if (_state.CurrentTab == 4)
                hints.Add(s.Section("g: Jump | e: Endian | Ctrl+S: Save"));
            else if (_state.CurrentTab == 6)
                hints.Add(s.Section("Backspace: Up"));
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

            // Search hint — always available on all tabs
            var currentSearch = _state.Search[_state.CurrentTab];
            if (currentSearch.IsActive)
                hints.Add(s.Section("Esc: Clear"));
            hints.Add(s.Section("/: Search"));

            // Show size toggle hint only on tabs that display sizes
            if (_state.CurrentTab is 0 or 1 or 6) // General, PE/Metadata, Size Map
                hints.Add(s.Section(_state.HumanReadableSizes ? "s: Sizes (dec)" : "s: Sizes (hex)"));
            hints.Add(s.Spacer());
            hints.Add(s.Section("q: Quit"));
            return hints;
        }).WithDefaultSeparator(" | ");
    }

    private static void SaveHexChanges(DotsiderState state)
    {
        var filePath = state.Analyzer.FilePath;
        var tempPath = filePath + ".tmp";

        try
        {
            var newBytes = state.HexEditorState.Document.GetBytes().ToArray();
            File.WriteAllBytes(tempPath, newBytes);

            // Build replacement from temp BEFORE disposing old analyzer.
            // This validates the image AND gives us a ready fallback.
            AssemblyAnalyzer newAnalyzer;
            try
            {
                newAnalyzer = new AssemblyAnalyzer(tempPath);
            }
            catch (Exception ex)
            {
                try { File.Delete(tempPath); } catch { }
                state.HexNotification = $"Cannot save: invalid image — {ex.Message}";
                return;
            }

            // Replacement ready — dispose old analyzer to release file lock
            state.Analyzer.Dispose();

            try
            {
                File.Move(tempPath, filePath, overwrite: true);
            }
            catch (Exception moveEx)
            {
                // Move failed — commit the temp analyzer directly
                CommitAnalyzer(state, newAnalyzer);
                state.HexNotification = $"Save failed, working from {tempPath}: {moveEx.Message}";
                return;
            }

            // Move succeeded — reopen from original path for correct FilePath.
            // Keep newAnalyzer as fallback (its FD survived the rename).
            try
            {
                var finalAnalyzer = new AssemblyAnalyzer(filePath);
                newAnalyzer.Dispose();
                CommitAnalyzer(state, finalAnalyzer);
            }
            catch
            {
                CommitAnalyzer(state, newAnalyzer);
            }

            state.HexNotification = "Saved successfully";
        }
        catch (Exception ex)
        {
            state.HexNotification = $"Save failed: {ex.Message}";
        }
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
        state.HexEditorState = new EditorState(hexDoc);
        state.CachedUserStrings = null;
        state.CachedMetadataStrings = null;
        state.CachedRawStrings = null;
        state.CachedGraph = null;
        state.CachedSizeTree = null;
        state.TreemapCurrentLevel = null;
        state.TreemapBreadcrumb.Clear();
        state.IlSelectedMethod = null;
    }
}
