using System.Diagnostics;
using System.Globalization;
using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Hex Dump tab (Tab 4), displaying the raw assembly bytes
/// in a full-size hex editor with byte category coloring, search match highlighting,
/// data interpretation panel, jump-to-byte dialog, and vim navigation.
/// Supports explicit text/hex search mode toggle via Ctrl+T.
/// </summary>
public static class HexDumpView
{
    /// <summary>
    /// Builds the Hex Dump view widget tree.
    /// </summary>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var search = state.Search[TabId.HexDump];

        // Clean up hex match state when search was dismissed externally (global Escape)
        if (!search.IsActive && state.HexMatchOffsets.Count > 0)
        {
            state.HexMatchOffsets = [];
            state.HexCurrentMatchIndex = -1;
            state.HexMatchPatternLength = 0;
            state.HexLastSearchQuery = null;
            state.HexLiveSearchTooSlow = false;
        }

        // --- Live search: adaptive throttle ---
        if (!string.IsNullOrEmpty(search.Query) && search.Query != state.HexLastSearchQuery
            && search.IsActive && !search.IsConfirmed)
        {
            if (!state.HexLiveSearchTooSlow)
            {
                var sw = Stopwatch.StartNew();
                ExecuteSearch(state);
                sw.Stop();
                if (sw.ElapsedMilliseconds > 8)
                    state.HexLiveSearchTooSlow = true;
            }
            state.HexLastSearchQuery = search.Query;
        }

        // Set up match navigation
        state.NavigateNextMatch = () =>
        {
            if (state.HexMatchOffsets.Count == 0) return;
            state.HexCurrentMatchIndex = (state.HexCurrentMatchIndex + 1) % state.HexMatchOffsets.Count;
            NavigateToOffset(state, state.HexMatchOffsets[state.HexCurrentMatchIndex]);
        };
        state.NavigatePrevMatch = () =>
        {
            if (state.HexMatchOffsets.Count == 0) return;
            state.HexCurrentMatchIndex = state.HexCurrentMatchIndex <= 0
                ? state.HexMatchOffsets.Count - 1
                : state.HexCurrentMatchIndex - 1;
            NavigateToOffset(state, state.HexMatchOffsets[state.HexCurrentMatchIndex]);
        };

        var isSearchEditing = search.IsActive && !search.IsConfirmed;

        return ctx.ZStack(z =>
        [
            // Layer 0: Main content
            z.VStack(outer =>
            {
                var widgets = new List<Hex1bWidget>();

                // Search bar (with hex/text mode indicator)
                SearchBarHelper.AddSearchBar(widgets, outer, search, state.App,
                    isHexTab: true, hexModeHex: state.HexSearchModeHex);

                // Match info bar (shown when search is active and has results)
                if (search.IsActive && state.HexMatchOffsets.Count > 0)
                {
                    var idx = state.HexCurrentMatchIndex + 1;
                    var total = state.HexMatchOffsets.Count;
                    var offset = state.HexMatchOffsets[Math.Max(0, state.HexCurrentMatchIndex)];
                    widgets.Add(outer.Text($" Match {idx}/{total} at offset 0x{offset:X8}").FixedHeight(1));
                }

                // Hex editor with custom renderer
                widgets.Add(outer.Editor(state.HexEditorState)
                    .WithViewRenderer(new DotsiderHexRenderer(state))
                    .FillWidth()
                    .FillHeight());

                // Data interpretation panel
                widgets.Add(DataInterpretationPanel.Build(outer, state));

                return [.. widgets];
            })
            .WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
                {
                    if (state.HexJumpDialogOpen)
                    {
                        state.HexJumpDialogOpen = false;
                        state.HexJumpInput = "";
                        state.App.Invalidate();
                        return;
                    }
                    if (state.HexMode == HexEditMode.Insert)
                    {
                        state.HexMode = HexEditMode.Normal;
                        state.HexEditorState.IsReadOnly = true;
                        state.App.Invalidate();
                        return;
                    }
                    if (search.IsActive)
                    {
                        search.Dismiss();
                        state.HexMatchOffsets = [];
                        state.HexCurrentMatchIndex = -1;
                        state.HexMatchPatternLength = 0;
                        state.HexLastSearchQuery = null;
                        state.HexLiveSearchTooSlow = false;
                        state.App.Invalidate();
                    }
                }, "Esc");

                // Ctrl+T: Toggle hex/text search mode
                bindings.Ctrl().Key(Hex1bKey.T).OverridesCapture().Action(_ =>
                {
                    state.HexSearchModeHex = !state.HexSearchModeHex;
                    state.App.Invalidate();
                }, "Toggle hex/text mode");

                // Vim navigation and mode switching — only when not editing search
                if (!isSearchEditing)
                {
                    // hjkl navigation — normal mode only
                    // Note: i/h/j/k/l are registered as Global in DotsiderApp because
                    // EditorNode's AnyCharacter() binding consumes letter keys before
                    // parent VStack bindings in the path-based routing.

                }
            })
            .Fill(),

            // Layer 1: Jump-to-byte dialog (conditional overlay)
            state.HexJumpDialogOpen
                ? z.Backdrop(
                    z.Border(
                        z.VStack(dlg =>
                        [
                            dlg.Text("  Jump to offset (hex):"),
                            dlg.Text(""),
                            dlg.HStack(row =>
                            [
                                row.Text("  0x").FixedWidth(4),
                                row.TextBox(state.HexJumpInput)
                                    .OnTextChanged(e =>
                                    {
                                        state.HexJumpInput = e.NewText;
                                        state.App.Invalidate();
                                    })
                                    .Fill()
                            ]).FixedHeight(1),
                            dlg.Text(""),
                            dlg.Text("  Enter: Jump | Esc: Cancel")
                        ])
                    ).Title(" Jump to Byte ").FixedWidth(40).FixedHeight(7)
                    .WithInputBindings(bindings =>
                    {
                        bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
                        {
                            state.HexJumpDialogOpen = false;
                            state.HexJumpInput = "";
                            state.App.Invalidate();
                        }, "Cancel");
                    })
                ).OnClickAway(() =>
                {
                    state.HexJumpDialogOpen = false;
                    state.HexJumpInput = "";
                    state.App.Invalidate();
                })
                : null
        ]).Fill();
    }

    /// <summary>
    /// Performs a hex dump search. Supports ASCII text search (default) and hex byte search.
    /// Respects <see cref="DotsiderState.HexSearchModeHex"/> for mode selection.
    /// </summary>
    public static string? ExecuteSearch(DotsiderState state)
    {
        var search = state.Search[TabId.HexDump];
        var query = search.Query;
        state.HexMatchOffsets = [];
        state.HexCurrentMatchIndex = -1;
        state.HexMatchPatternLength = 0;

        if (string.IsNullOrEmpty(query)) return null;

        var rawBytes = state.Analyzer.RawBytes.Span;

        if (state.HexSearchModeHex)
        {
            // Hex byte search: parse raw input as hex bytes
            var (Bytes, Error) = ParseHexPattern(query);
            if (Error is not null) return Error;
            state.HexMatchOffsets = FindBytePattern(rawBytes, Bytes!);
            state.HexMatchPatternLength = Bytes!.Length;
        }
        else if (query.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            // Text mode with 0x prefix: hex byte search
            var (Bytes, Error) = ParseHexPattern(query[2..]);
            if (Error is not null) return Error;
            state.HexMatchOffsets = FindBytePattern(rawBytes, Bytes!);
            state.HexMatchPatternLength = Bytes!.Length;
        }
        else
        {
            // ASCII text search
            var textBytes = System.Text.Encoding.ASCII.GetBytes(query);
            state.HexMatchOffsets = FindBytePattern(rawBytes, textBytes);
            state.HexMatchPatternLength = textBytes.Length;
        }

        search.SetMatchCount(state.HexMatchOffsets.Count);
        if (state.HexMatchOffsets.Count > 0)
        {
            state.HexCurrentMatchIndex = 0;
            NavigateToOffset(state, state.HexMatchOffsets[0]);
        }

        return null;
    }

    /// <summary>
    /// Parses a hex pattern string into a byte array.
    /// </summary>
    public static (byte[]? Bytes, string? Error) ParseHexPattern(string hex)
    {
        hex = hex.Replace(" ", "");
        if (hex.Length == 0) return (null, "Invalid hex: empty pattern");
        if (hex.Length % 2 != 0) return (null, "Invalid hex: odd number of digits");

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, null, out bytes[i]))
                return (null, "Invalid hex pattern");
        }

        return (bytes, null);
    }

    /// <summary>
    /// Finds all occurrences of a byte pattern in the raw bytes.
    /// </summary>
    public static List<long> FindBytePattern(ReadOnlySpan<byte> data, byte[] pattern)
    {
        var offsets = new List<long>();
        if (pattern.Length == 0 || data.Length < pattern.Length) return offsets;

        for (var i = 0; i <= data.Length - pattern.Length; i++)
        {
            if (data.Slice(i, pattern.Length).SequenceEqual(pattern))
                offsets.Add(i);
        }

        return offsets;
    }

    /// <summary>
    /// Processes the jump dialog input: parses hex offset, navigates, closes dialog.
    /// Called from DotsiderApp's global Enter binding.
    /// </summary>
    public static void ProcessJumpInput(DotsiderState state)
    {
        var input = state.HexJumpInput.Trim();
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            input = input[2..];

        if (long.TryParse(input, NumberStyles.HexNumber, null, out var offset))
        {
            var doc = state.HexEditorState.Document;
            offset = Math.Clamp(offset, 0, doc.ByteCount - 1);
            NavigateToOffset(state, offset);
            state.HexJumpDialogOpen = false;
            state.HexJumpInput = "";
            state.HexNotification = null;
            state.App.RequestFocus(node => node is EditorNode);
        }
        else
        {
            state.HexNotification = $"Invalid hex offset: {state.HexJumpInput}";
        }
    }

    private static void NavigateToOffset(DotsiderState state, long offset)
    {
        var doc = state.HexEditorState.Document;
        if (offset < doc.ByteCount)
        {
            var byteMap = doc.GetByteMap();
            var (charIdx, _) = byteMap.ByteToChar((int)offset);
            state.HexEditorState.SetCursorPosition(
                new Hex1b.Documents.DocumentOffset(charIdx));
            state.HexEditorState.ByteCursorOffset = (int)offset;
            state.HexScrollTarget = offset;
        }
    }
}
