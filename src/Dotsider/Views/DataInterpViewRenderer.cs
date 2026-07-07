using Hex1b;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;
using System.Text;

namespace Dotsider.Views;

/// <summary>
/// Custom editor renderer for the Data Interpretation panel. The editor document
/// has 4 lines (one per visual row), each containing 4 tab-separated fields.
/// This renderer splits the fields and draws them at proportional column widths
/// using <c>viewport.Width</c>, so the layout is always correct for the current
/// terminal size. Handles label coloring, cursor, and selection rendering directly.
/// </summary>
public sealed class DataInterpViewRenderer : IEditorViewRenderer
{
    /// <summary>Shared singleton instance (renderer is stateless).</summary>
    public static DataInterpViewRenderer Instance { get; } = new();

    private static readonly Hex1bColor LabelColor = Hex1bColor.FromRgb(100, 130, 160);
    private static readonly string LabelFgAnsi = LabelColor.ToForegroundAnsi();

    private const int Rows = 4;
    private const int Cols = 4;

    /// <inheritdoc />
    public void Render(Hex1bRenderContext context, EditorState state, Rect viewport,
        int scrollOffset, int horizontalScrollOffset, bool isFocused,
        char? pendingNibble = null,
        IReadOnlyList<ITextDecorationProvider>? decorationProviders = null,
        IReadOnlyList<InlineHint>? inlineHints = null,
        bool wordWrap = false,
        IReadOnlyList<FoldingRegion>? foldingRegions = null)
    {
        var theme = context.Theme;
        var fg = theme.Get(EditorTheme.ForegroundColor);
        var bg = theme.Get(EditorTheme.BackgroundColor);
        var selFg = theme.Get(EditorTheme.SelectionForegroundColor);
        var selBg = theme.Get(EditorTheme.SelectionBackgroundColor);
        var cursorFg = theme.Get(EditorTheme.CursorForegroundColor);
        var cursorBg = theme.Get(EditorTheme.CursorBackgroundColor);

        var fgAnsi = fg.ToForegroundAnsi();
        var bgAnsi = bg.ToBackgroundAnsi();
        var selFgAnsi = !selFg.IsDefault ? selFg.ToForegroundAnsi() : fgAnsi;
        var selBgAnsi = selBg.ToBackgroundAnsi();
        var cursorFgAnsi = cursorFg.ToForegroundAnsi();
        var cursorBgAnsi = cursorBg.ToBackgroundAnsi();

        var doc = state.Document;
        var colW = viewport.Width / Cols;

        // Selection range (document offsets)
        var hasSelection = isFocused && state.Cursor.HasSelection;
        var selStart = hasSelection ? state.Cursor.SelectionStart.Value : -1;
        var selEnd = hasSelection ? state.Cursor.SelectionEnd.Value : -1;
        var cursorPos = isFocused ? state.Cursor.Position.Value : -1;

        // Yank flash range and colors (from decoration providers)
        var yankStart = -1;
        var yankEnd = -1;
        var yankFgAnsi = fgAnsi;
        var yankBgAnsi = bgAnsi;
        if (decorationProviders is not null)
        {
            foreach (var provider in decorationProviders)
            {
                var spans = provider.GetDecorations(1, doc.LineCount, doc);
                foreach (var span in spans)
                {
                    if (span.Decoration.Background is { IsDefault: false } yankBg)
                    {
                        yankStart = doc.PositionToOffset(span.Start).Value;
                        yankEnd = doc.PositionToOffset(span.End).Value;
                        yankBgAnsi = yankBg.ToBackgroundAnsi();
                        if (span.Decoration.Foreground is { IsDefault: false } yankFgColor)
                            yankFgAnsi = yankFgColor.ToForegroundAnsi();
                    }
                }
            }
        }

        // Compute line start offsets for document offset mapping
        var lineStartOffsets = new int[Rows];
        var offset = 0;
        for (var line = 0; line < Rows; line++)
        {
            lineStartOffsets[line] = offset;
            offset += (doc.GetLineText(line + 1)?.Length ?? 0) + 1; // +1 for newline
        }

        for (var row = 0; row < Rows && row < viewport.Height; row++)
        {
            var screenY = viewport.Y + row;
            var lineText = doc.GetLineText(row + 1) ?? "";
            var fields = lineText.Split('\t');
            var sb = new StringBuilder(viewport.Width * 3);

            // Track document column offset within the line
            var docCol = 0;

            for (var col = 0; col < Cols; col++)
            {
                var field = col < fields.Length ? fields[col] : "";
                var cellWidth = col < Cols - 1 ? colW : viewport.Width - col * colW;

                // Find colon in field for label coloring
                var colonIdx = field.IndexOf(':');

                for (var ci = 0; ci < cellWidth; ci++)
                {
                    var isText = ci < field.Length;
                    var isCursorSlot = ci == field.Length; // one past last char, valid cursor position
                    var ch = isText ? field[ci] : ' ';

                    if (!isText && !isCursorSlot)
                    {
                        // Deep padding — always default styling
                        sb.Append(fgAnsi).Append(bgAnsi);
                    }
                    else
                    {
                        var docOffset = lineStartOffsets[row] + docCol + Math.Min(ci, field.Length);

                        if (docOffset == cursorPos)
                            sb.Append(cursorFgAnsi).Append(cursorBgAnsi);
                        else if (!isText)
                            sb.Append(fgAnsi).Append(bgAnsi); // cursor slot without cursor
                        else if (docOffset >= yankStart && docOffset < yankEnd && yankStart >= 0)
                            sb.Append(yankFgAnsi).Append(yankBgAnsi);
                        else if (docOffset >= selStart && docOffset < selEnd)
                            sb.Append(selFgAnsi).Append(selBgAnsi);
                        else if (colonIdx >= 0 && ci <= colonIdx)
                            sb.Append(LabelFgAnsi).Append(bgAnsi);
                        else
                            sb.Append(fgAnsi).Append(bgAnsi);
                    }

                    sb.Append(ch);
                }

                // Advance document column past the field + tab separator
                docCol += field.Length + 1; // +1 for the \t
            }

            context.WriteClipped(viewport.X, screenY, sb.ToString());
        }

        // Blank remaining lines
        for (var row = Rows; row < viewport.Height; row++)
        {
            var screenY = viewport.Y + row;
            context.WriteClipped(viewport.X, screenY,
                $"{fgAnsi}{bgAnsi}{new string(' ', viewport.Width)}");
        }
    }

    /// <inheritdoc />
    public DocumentOffset? HitTest(int localX, int localY, EditorState state,
        int viewportColumns, int viewportLines, int scrollOffset, int horizontalScrollOffset)
    {
        if (localY < 0 || localY >= Rows || localY >= state.Document.LineCount
            || localX < 0)
            return null;

        var colW = viewportColumns / Cols;
        var col = Math.Min(localX / Math.Max(1, colW), Cols - 1);
        var charInCell = Math.Max(0, localX - col * colW);

        var lineText = state.Document.GetLineText(localY + 1) ?? "";
        var fields = lineText.Split('\t');

        // Compute document offset: sum of field lengths + tab separators before this column
        var docCol = 0;
        for (var c = 0; c < col && c < fields.Length; c++)
            docCol += fields[c].Length + 1; // +1 for \t

        var field = col < fields.Length ? fields[col] : "";
        charInCell = Math.Min(charInCell, field.Length);
        docCol += charInCell;

        // Compute absolute document offset
        var lineOffset = 0;
        for (var line = 0; line < localY; line++)
            lineOffset += (state.Document.GetLineText(line + 1)?.Length ?? 0) + 1;

        return new DocumentOffset(Math.Clamp(lineOffset + docCol, 0, state.Document.Length));
    }

    /// <inheritdoc />
    public int GetTotalLines(IHex1bDocument document, int viewportColumns) => Rows;

    /// <inheritdoc />
    public int GetMaxLineWidth(IHex1bDocument document, int scrollOffset,
        int viewportLines, int viewportColumns) => viewportColumns;
}
