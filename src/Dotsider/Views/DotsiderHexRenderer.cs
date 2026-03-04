using System.Text;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Custom hex editor renderer with byte category coloring (binsider-style),
/// search match highlighting via binary search, and dim address column.
/// Delegates all non-Render methods to the built-in HexEditorViewRenderer.
/// </summary>
public sealed class DotsiderHexRenderer : IEditorViewRenderer
{
    private const int AddressWidth = 8;
    private const int SeparatorWidth = 2;

    // Byte category foreground colors (binsider parity)
    private static readonly Hex1bColor NullColor = Hex1bColor.FromRgb(80, 80, 100);
    private static readonly Hex1bColor PrintableColor = Hex1bColor.FromRgb(0, 200, 200);
    private static readonly Hex1bColor WhitespaceColor = Hex1bColor.FromRgb(100, 200, 100);
    private static readonly Hex1bColor ControlColor = Hex1bColor.FromRgb(200, 100, 200);
    private static readonly Hex1bColor HighByteColor = Hex1bColor.FromRgb(200, 200, 100);
    private static readonly Hex1bColor AddressColor = Hex1bColor.FromRgb(100, 100, 130);

    // Pre-computed ANSI strings for category colors
    private static readonly string NullFgAnsi = NullColor.ToForegroundAnsi();
    private static readonly string PrintableFgAnsi = PrintableColor.ToForegroundAnsi();
    private static readonly string WhitespaceFgAnsi = WhitespaceColor.ToForegroundAnsi();
    private static readonly string ControlFgAnsi = ControlColor.ToForegroundAnsi();
    private static readonly string HighByteFgAnsi = HighByteColor.ToForegroundAnsi();
    private static readonly string AddressFgAnsi = AddressColor.ToForegroundAnsi();
    private static readonly string MatchBgAnsi = HighlightHelper.MatchBgColor.ToBackgroundAnsi();
    private static readonly string MatchFgAnsi = Hex1bColor.Black.ToForegroundAnsi();

    // Snap points and max must match between _inner and CalculateLayout
    private static readonly int[] Snaps = [1, 8, 16, 32];
    private const int MaxBytesPerRow = 32;

    private readonly HexEditorViewRenderer _inner = new()
    {
        MaxBytesPerRow = MaxBytesPerRow,
        SnapPoints = Snaps
    };
    private readonly DotsiderState _state;

    public DotsiderHexRenderer(DotsiderState state)
    {
        _state = state;
    }

    public bool HandlesCharInput => true;

    // --- Delegation to inner renderer (safe: all 5 methods independently compute layout) ---

    public bool HandleCharInput(char c, EditorState state, ref char? pendingNibble, int viewportColumns)
    {
        var handled = _inner.HandleCharInput(c, state, ref pendingNibble, viewportColumns);
        if (handled) ClampByteCursor(state);
        return handled;
    }

    public bool HandleNavigation(CursorDirection direction, EditorState state, bool extend, int viewportColumns)
    {
        var handled = _inner.HandleNavigation(direction, state, extend, viewportColumns);
        if (handled) ClampByteCursor(state);
        return handled;
    }

    /// <summary>
    /// Ensures ByteCursorOffset stays on a valid byte (0..ByteCount-1).
    /// The built-in hex renderer allows cursor at ByteCount (past-the-end) for Down/Right,
    /// but hex cursors should always sit on a cell — matching the Up/Left behavior at byte 0.
    /// </summary>
    private static void ClampByteCursor(EditorState state)
    {
        var maxByte = state.Document.ByteCount - 1;
        if (maxByte < 0 || state.ByteCursorOffset is not int bco || bco <= maxByte)
            return;

        state.ByteCursorOffset = maxByte;
        var map = state.Document.GetByteMap();
        var (charIdx, _) = map.ByteToChar(maxByte);
        state.Cursor.Position = new DocumentOffset(charIdx);
    }

    public DocumentOffset? HitTest(int localX, int localY, EditorState state,
        int viewportColumns, int viewportLines, int scrollOffset, int horizontalScrollOffset)
    {
        scrollOffset = ApplyScrollOverride(scrollOffset, viewportColumns, viewportLines);
        return _inner.HitTest(localX, localY, state, viewportColumns, viewportLines,
            scrollOffset, horizontalScrollOffset);
    }

    public int GetTotalLines(IHex1bDocument document, int viewportColumns)
    {
        // Sync BytesPerRow BEFORE EnsureCursorVisible runs (GetTotalLines is called
        // in ArrangeCore before EnsureCursorVisible). This ensures the wrapper document's
        // OffsetToPosition returns hex rows consistent with the current viewport width.
        _state.HexRowDoc.BytesPerRow = CalculateLayout(viewportColumns);
        return _inner.GetTotalLines(document, viewportColumns);
    }

    public int GetMaxLineWidth(IHex1bDocument document, int scrollOffset, int viewportLines, int viewportColumns)
        => _inner.GetMaxLineWidth(document, scrollOffset, viewportLines, viewportColumns);

    // --- Layout (duplicated: CalculateLayout is internal to hex1b) ---

    private static int CalculateLayout(int availableWidth)
    {
        // Row width = 4N + 11 (address:8 + sep:2 + hex:3N-1 + sep:2 + ascii:N)
        var maxFit = Math.Max(1, (availableWidth - 11) / 4);
        var bytesPerRow = Math.Clamp(maxFit, 1, MaxBytesPerRow);

        // Snap down to the largest snap point that fits
        var snapped = Snaps[0];
        foreach (var sp in Snaps)
        {
            if (sp <= bytesPerRow) snapped = sp;
            else break;
        }
        return snapped;
    }

    // --- Scroll override for programmatic navigation (jump/search) ---
    // HexRowDocument fixes EnsureCursorVisible for normal cursor movement,
    // but programmatic SetCursorPosition doesn't trigger _cursorDirty, so
    // EditorNode's scroll doesn't update. We override until it catches up.

    private int ApplyScrollOverride(int scrollOffset, int viewportColumns, int viewportLines)
    {
        if (_state.HexScrollTarget is not { } target) return scrollOffset;

        // EditorNode's scroll changed (user interacted or EnsureCursorVisible ran) — clear override
        if (scrollOffset != _state.HexLastEditorScrollOffset
            && _state.HexLastEditorScrollOffset != 0)
        {
            _state.HexScrollTarget = null;
            return scrollOffset;
        }

        var bytesPerRow = CalculateLayout(viewportColumns);
        var targetRow = (int)(target / bytesPerRow) + 1;
        return Math.Max(1, targetRow - viewportLines / 2);
    }

    // --- Byte category coloring ---

    private static string GetByteCategoryFgAnsi(byte b)
    {
        if (b == 0x00) return NullFgAnsi;
        if (b is 0x09 or 0x0A or 0x0D) return WhitespaceFgAnsi;
        if (b <= 0x1F) return ControlFgAnsi;
        if (b <= 0x7E) return PrintableFgAnsi;
        return HighByteFgAnsi;
    }

    // --- Match checking via binary search on sorted offsets: O(log n) per byte ---

    private bool IsMatchByte(long byteOffset)
    {
        var offsets = _state.HexMatchOffsets;
        var patternLen = _state.HexMatchPatternLength;
        if (offsets.Count == 0 || patternLen <= 0) return false;

        int lo = 0, hi = offsets.Count - 1, result = -1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (offsets[mid] <= byteOffset) { result = mid; lo = mid + 1; }
            else hi = mid - 1;
        }

        return result >= 0 && byteOffset < offsets[result] + patternLen;
    }

    // --- Custom Render ---

    public void Render(Hex1bRenderContext context, EditorState state, Rect viewport,
        int scrollOffset, int horizontalScrollOffset, bool isFocused, char? pendingNibble = null)
    {
        var editorScroll = scrollOffset;
        scrollOffset = ApplyScrollOverride(scrollOffset, viewport.Width, viewport.Height);
        _state.HexLastEditorScrollOffset = editorScroll;
        var bytesPerRow = CalculateLayout(viewport.Width);
        var theme = context.Theme;
        var fg = theme.Get(EditorTheme.ForegroundColor);
        var bg = theme.Get(EditorTheme.BackgroundColor);
        var cursorFg = theme.Get(EditorTheme.CursorForegroundColor);
        var cursorBg = theme.Get(EditorTheme.CursorBackgroundColor);
        var selFg = theme.Get(EditorTheme.SelectionForegroundColor);
        var selBg = theme.Get(EditorTheme.SelectionBackgroundColor);

        var fgAnsi = fg.ToForegroundAnsi();
        var bgAnsi = bg.ToBackgroundAnsi();
        var cursorFgAnsi = cursorFg.ToForegroundAnsi();
        var cursorBgAnsi = cursorBg.ToBackgroundAnsi();
        var selFgAnsi = selFg.ToForegroundAnsi();
        var selBgAnsi = selBg.ToBackgroundAnsi();

        var doc = state.Document;
        var docBytes = doc.GetBytes().Span;
        var totalBytes = docBytes.Length;

        var byteMap = doc.GetByteMap();
        var selectionRanges = new List<(int Start, int End)>();
        var cursorByteOffset = -1;

        if (isFocused)
        {
            if (state.ByteCursorOffset is int bco)
                cursorByteOffset = Math.Clamp(bco, 0, Math.Max(0, totalBytes - 1));
            else
            {
                var cursorDocOffset = Math.Min(state.Cursor.Position.Value, byteMap.CharCount);
                cursorByteOffset = cursorDocOffset < byteMap.CharCount
                    ? Math.Min(byteMap.CharToByteStart(cursorDocOffset), Math.Max(0, totalBytes - 1))
                    : Math.Max(0, totalBytes - 1);
            }

            foreach (var cursor in state.Cursors)
            {
                if (cursor.HasSelection)
                {
                    var selStart = Math.Min(cursor.SelectionStart.Value, byteMap.CharCount);
                    var selEnd = Math.Min(cursor.SelectionEnd.Value, byteMap.CharCount);
                    var startByte = selStart < byteMap.CharCount
                        ? byteMap.CharToByteStart(selStart) : totalBytes;
                    var endByte = selEnd < byteMap.CharCount
                        ? byteMap.CharToByteStart(selEnd) : totalBytes;
                    selectionRanges.Add((startByte, endByte));
                }
            }
        }

        for (var viewLine = 0; viewLine < viewport.Height; viewLine++)
        {
            var row = (scrollOffset - 1) + viewLine;
            var screenY = viewport.Y + viewLine;
            var screenX = viewport.X;
            var rowByteStart = row * bytesPerRow;

            if (rowByteStart >= totalBytes)
            {
                context.WriteClipped(screenX, screenY,
                    $"{fgAnsi}{bgAnsi}{"~".PadRight(viewport.Width)}");
                continue;
            }

            var rowByteEnd = Math.Min(rowByteStart + bytesPerRow, totalBytes);
            var rowByteCount = rowByteEnd - rowByteStart;
            var sb = new StringBuilder(viewport.Width * 3);

            // Address column (dim gray)
            sb.Append(AddressFgAnsi).Append(bgAnsi);
            sb.Append(rowByteStart.ToString("X8"));
            sb.Append(fgAnsi).Append("  ");

            // Hex bytes
            for (var i = 0; i < bytesPerRow; i++)
            {
                if (i < rowByteCount)
                {
                    var byteIdx = rowByteStart + i;
                    var b = docBytes[byteIdx];
                    AppendByteAnsi(sb, byteIdx, b, cursorByteOffset, selectionRanges,
                        isFocused, cursorFgAnsi, cursorBgAnsi, selFgAnsi, selBgAnsi, bgAnsi);

                    if (pendingNibble.HasValue && byteIdx == cursorByteOffset)
                    {
                        sb.Append(char.ToUpper(pendingNibble.Value));
                        sb.Append('_');
                    }
                    else
                    {
                        sb.Append(b.ToString("X2"));
                    }
                }
                else
                {
                    sb.Append(fgAnsi).Append(bgAnsi).Append("  ");
                }

                if (i < bytesPerRow - 1)
                    sb.Append(fgAnsi).Append(bgAnsi).Append(' ');
            }

            // Hex-ASCII separator
            sb.Append(fgAnsi).Append(bgAnsi).Append("  ");

            // ASCII sidebar
            for (var i = 0; i < bytesPerRow; i++)
            {
                if (i < rowByteCount)
                {
                    var byteIdx = rowByteStart + i;
                    var b = docBytes[byteIdx];
                    var c = (b >= 0x20 && b < 0x7F) ? (char)b : '.';
                    AppendByteAnsi(sb, byteIdx, b, cursorByteOffset, selectionRanges,
                        isFocused, cursorFgAnsi, cursorBgAnsi, selFgAnsi, selBgAnsi, bgAnsi);
                    sb.Append(c);
                }
                else
                {
                    sb.Append(fgAnsi).Append(bgAnsi).Append(' ');
                }
            }

            // Reset to theme colors at end of line
            sb.Append(fgAnsi).Append(bgAnsi);
            context.WriteClipped(screenX, screenY, sb.ToString());
        }
    }

    /// <summary>
    /// Appends ANSI fg+bg codes for a byte based on priority: Cursor > Selection > Match > Category.
    /// </summary>
    private void AppendByteAnsi(StringBuilder sb, int byteOffset, byte b,
        int cursorByteOffset, List<(int Start, int End)> selectionRanges, bool isFocused,
        string cursorFgAnsi, string cursorBgAnsi,
        string selFgAnsi, string selBgAnsi, string defaultBgAnsi)
    {
        // Cursor (highest priority)
        if (isFocused && byteOffset == cursorByteOffset)
        {
            sb.Append(cursorFgAnsi).Append(cursorBgAnsi);
            return;
        }

        // Selection
        if (isFocused)
        {
            foreach (var (start, end) in selectionRanges)
            {
                if (byteOffset >= start && byteOffset < end)
                {
                    sb.Append(selFgAnsi).Append(selBgAnsi);
                    return;
                }
            }
        }

        // Match
        if (IsMatchByte(byteOffset))
        {
            sb.Append(MatchFgAnsi).Append(MatchBgAnsi);
            return;
        }

        // Category coloring
        sb.Append(GetByteCategoryFgAnsi(b)).Append(defaultBgAnsi);
    }
}
