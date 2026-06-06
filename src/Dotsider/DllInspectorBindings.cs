using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Registers key bindings for the DLL inspector views (hex dump, IL inspector, search)
/// that are shared between DotsiderApp and NuGetApp.
/// </summary>
public static class DllInspectorBindings
{
    /// <summary>
    /// Registers hex dump, IL inspector, and search key bindings for the given DLL state.
    /// Called from both DotsiderApp and NuGetApp to avoid duplication.
    /// </summary>
    /// <param name="bindings">The input bindings builder.</param>
    /// <param name="state">The DLL inspector state.</param>
    /// <param name="app">The Hex1b app instance for invalidation and focus.</param>
    /// <param name="includeSearch">Whether to register search toggle/confirm/dismiss bindings.
    /// DotsiderApp registers its own search bindings, so this is false for DotsiderApp.</param>
    /// <param name="resetVimPending">Optional callback to reset the vim text-object pending state to idle.</param>
    public static void Register(InputBindingsBuilder bindings, DotsiderState state, Hex1bApp app,
        bool includeSearch = false, Action? resetVimPending = null)
    {
        var currentSearch = state.Search[state.CurrentTab];
        var isSearchEditing = currentSearch.IsActive && !currentSearch.IsConfirmed;

        // Search bindings (only for NuGetApp — DotsiderApp registers its own)
        if (includeSearch)
        {
            var detailPopupOpen = state.PeDetailContent is not null || state.StringsDetailContent is not null;
            if (!state.HexJumpDialogOpen && !detailPopupOpen)
            {
                void SearchToggle()
                {
                    state.Search[state.CurrentTab].ActivateOrCycle();
                    var s = state.Search[state.CurrentTab];
                    if (s.IsActive && !s.IsConfirmed)
                        app.RequestFocus(node => node is TextBoxNode);
                    app.Invalidate();
                }

                bindings.Key(Hex1bKey.OemQuestion).Global().OverridesCapture().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    SearchToggle();
                }, "Search");
                if (!isSearchEditing)
                {
                    bindings.Key(Hex1bKey.None).Global().OverridesCapture().Action(_ =>
                    {
                        resetVimPending?.Invoke();
                        SearchToggle();
                    }, "Search");
                }
            }

            if (isSearchEditing)
            {
                bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    if (!string.IsNullOrEmpty(currentSearch.Query))
                    {
                        if (state.CurrentTab == TabId.HexDump)
                            Views.HexDumpView.ExecuteSearch(state);
                        currentSearch.Confirm();
                        state.RequestContentFocus();
                        app.Invalidate();
                    }
                }, "Confirm search");
            }

            // n/N match navigation
            if (currentSearch.IsActive && currentSearch.IsConfirmed)
            {
                bindings.Key(Hex1bKey.N).Global().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    state.NavigateNextMatch?.Invoke();
                    app.Invalidate();
                }, "Next match");
                bindings.Shift().Key(Hex1bKey.N).Global().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    state.NavigatePrevMatch?.Invoke();
                    app.Invalidate();
                }, "Prev match");
            }

            // Escape to dismiss search
            if (currentSearch.IsActive && !state.HexJumpDialogOpen)
            {
                bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    if (state.CurrentTab == TabId.HexDump && state.HexMode == HexEditMode.Insert)
                    {
                        state.HexMode = HexEditMode.Normal;
                        state.HexEditorState.IsReadOnly = true;
                        app.Invalidate();
                        return;
                    }
                    currentSearch.Dismiss();
                    if (state.CurrentTab == TabId.HexDump)
                    {
                        state.HexMatchOffsets = [];
                        state.HexCurrentMatchIndex = -1;
                        state.HexMatchPatternLength = 0;
                        state.HexLastSearchQuery = null;
                        state.HexLiveSearchTooSlow = false;
                    }
                    state.RequestContentFocus();
                    app.Invalidate();
                }, "Clear search");
            }

            // Hex insert mode Escape without search
            if (!currentSearch.IsActive
                && state.CurrentTab == TabId.HexDump
                && state.HexMode == HexEditMode.Insert
                && !state.HexJumpDialogOpen)
            {
                bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    state.HexMode = HexEditMode.Normal;
                    state.HexEditorState.IsReadOnly = true;
                    app.Invalidate();
                }, "Exit insert mode");
            }
        }

        // Hex Dump tab — Normal mode bindings
        if (state.CurrentTab == TabId.HexDump && !state.HexJumpDialogOpen
            && state.HexMode == HexEditMode.Normal)
        {
            // g (jump) and e (endianness) work from any editor on the tab
            bindings.Key(Hex1bKey.G).Global().Action(_ =>
            {
                resetVimPending?.Invoke();
                state.HexJumpDialogOpen = true;
                state.HexJumpInput = "";
                state.HexNotification = null;
                app.RequestFocus(node => node is TextBoxNode);
                app.Invalidate();
            }, "Jump to offset");

            bindings.Key(Hex1bKey.E).Global().Action(_ =>
            {
                resetVimPending?.Invoke();
                state.HexEndianness = state.HexEndianness == HexEndianness.Little
                    ? HexEndianness.Big : HexEndianness.Little;
                app.Invalidate();
            }, "Toggle endianness");

            // i/h/j/k/l only register when the hex editor is focused so the data
            // interpretation editor's local ConfigureReadOnlyEditorBindings can
            // handle those keys for vim navigation and text objects.
            var hexEditorFocused = app.FocusedNode is not EditorNode focusedEd
                || focusedEd.State == state.HexEditorState;
            if (hexEditorFocused)
            {
                bindings.Key(Hex1bKey.I).Global().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    state.HexMode = HexEditMode.Insert;
                    state.HexEditorState.IsReadOnly = false;
                    state.HexNotification = null;
                    app.Invalidate();
                }, "Insert mode");

                bindings.Key(Hex1bKey.H).Global().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    state.HexEditorState.MoveCursor(CursorDirection.Left);
                    app.Invalidate();
                }, "Left");
                bindings.Key(Hex1bKey.L).Global().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    state.HexEditorState.MoveCursor(CursorDirection.Right);
                    app.Invalidate();
                }, "Right");
                bindings.Key(Hex1bKey.K).Global().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    state.HexEditorState.MoveCursor(CursorDirection.Up);
                    app.Invalidate();
                }, "Up");
                bindings.Key(Hex1bKey.J).Global().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    state.HexEditorState.MoveCursor(CursorDirection.Down);
                    app.Invalidate();
                }, "Down");
            }
        }

        // Hex jump dialog Enter
        if (state.HexJumpDialogOpen)
        {
            bindings.Key(Hex1bKey.Enter).Global().OverridesCapture().Action(_ =>
            {
                resetVimPending?.Invoke();
                Views.HexDumpView.ProcessJumpInput(state);
                app.Invalidate();
            }, "Jump");
        }

        // IL Inspector tab bindings
        if (state.CurrentTab == TabId.IlInspector)
        {
            var ilSearch = state.Search[TabId.IlInspector];
            // Selection clearing moved to DotsiderApp's unified Escape handler
            // to avoid Global Escape binding conflicts with back navigation.

            if (state.IlSelectedMethod is { Rva: > 0 } ilMethod)
            {
                bindings.Key(Hex1bKey.X).Global().Action(_ =>
                {
                    resetVimPending?.Invoke();
                    state.NavigateToHexOffset(ilMethod.Rva);
                }, "View in hex");
            }

            bindings.Key(Hex1bKey.L).Global().Action(_ =>
            {
                resetVimPending?.Invoke();
                app.RequestFocus(node => node is EditorNode);
                app.Invalidate();
            }, "Focus IL");
        }
    }

    /// <summary>
    /// Adds DLL-inspector-specific hints to the hints bar.
    /// Called from both DotsiderApp and NuGetApp to avoid duplication.
    /// </summary>
    /// <param name="hints">The hints list to append to.</param>
    /// <param name="s">The info bar context for creating hint sections.</param>
    /// <param name="state">The DLL inspector state.</param>
    public static void AddHints(List<IInfoBarChild> hints, InfoBarContext s, DotsiderState state)
    {
        if (state.CurrentTab == TabId.PeMetadata)
        {
            hints.Add(s.Section("Enter: Detail"));
            if (state.PeSubTab is PeSubTabId.TypeDef or PeSubTabId.MethodDef)
                hints.Add(s.Section("g: Go to IL"));
        }
        else if (state.CurrentTab == TabId.IlInspector)
        {
            if (state.IlSelectedMethod is not null)
                hints.Add(s.Section("Enter/gd: Go to def"));
            hints.Add(s.Section("l: Focus IL"));
            if (state.IlSelectedMethod is { Rva: > 0 })
                hints.Add(s.Section("x: Hex"));
            if (state.IlSelectedMethod is { } method
                && state.Analyzer.GetMethodDebugInfo(method).SequencePoints.Any(p => p.HasEmbeddedSource))
                hints.Add(s.Section("o: Source"));
            if (state.IlEditorState?.Cursor.HasSelection == true)
                hints.Add(s.Section("y: Yank (IL)"));
        }
        else if (state.CurrentTab == TabId.Strings)
            hints.Add(s.Section("Enter: Detail"));
        else if (state.CurrentTab == TabId.HexDump)
        {
            if (state.HexMode == HexEditMode.Insert)
                hints.Add(s.Section("Esc: Normal"));
            else
            {
                var hexHints = "i: Edit | g: Jump | e: Endian";
                if (state.HexIsDirty)
                    hexHints += " | Ctrl+S: Save";
                hints.Add(s.Section(hexHints));
            }
        }

        // Cross-view back hint
        if (state.CrossViewBackTarget is not null)
            hints.Add(s.Section("Esc: Back"));

        // Search hint
        var currentSearch = state.Search[state.CurrentTab];
        if (currentSearch.IsActive)
            hints.Add(s.Section("Esc: Clear"));
        hints.Add(s.Section("/: Search"));

        // Size toggle
        if (state.CurrentTab is TabId.General or TabId.PeMetadata)
            hints.Add(s.Section(state.HumanReadableSizes ? "s: Sizes (dec)" : "s: Sizes (hex)"));

        // Yank hint
        var yankable = state.CurrentTab switch
        {
            TabId.General => state.GeneralFocusedDep is not null,
            TabId.PeMetadata => state.PeDetailContent is not null || state.PeFocusedKey is not null,
            TabId.IlInspector => false,
            TabId.Strings => state.StringsDetailContent is not null || state.StringsFocusedKey is not null,
            TabId.HexDump => true,
            _ => false
        };
        if (yankable)
            hints.Add(s.Section("y: Yank"));
    }
}
