using Hex1b;
using Hex1b.Documents;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// A text editor view renderer for read-only info panels that renders blank lines
/// instead of vim-style <c>~</c> markers for lines beyond the document content.
/// Delegates all behavior to <see cref="TextEditorViewRenderer"/> and overwrites
/// the tilde markers after rendering.
/// </summary>
public sealed class InfoEditorViewRenderer : IEditorViewRenderer
{
    /// <summary>Shared singleton instance (renderer is stateless).</summary>
    public static InfoEditorViewRenderer Instance { get; } = new();

    /// <inheritdoc />
    public void Render(Hex1bRenderContext context, EditorState state, Rect viewport,
        int scrollOffset, int horizontalScrollOffset, bool isFocused,
        char? pendingNibble = null,
        IReadOnlyList<ITextDecorationProvider>? decorationProviders = null,
        IReadOnlyList<InlineHint>? inlineHints = null,
        bool wordWrap = false,
        IReadOnlyList<FoldingRegion>? foldingRegions = null)
    {
        // Delegate to the standard text renderer
        TextEditorViewRenderer.Instance.Render(context, state, viewport,
            scrollOffset, horizontalScrollOffset, isFocused,
            pendingNibble, decorationProviders, inlineHints, wordWrap, foldingRegions);

        // Overwrite tilde lines with blank space
        var docLineCount = state.Document.LineCount;
        var viewportLines = viewport.Height;
        var firstEmptyViewLine = docLineCount - scrollOffset + 1;

        if (firstEmptyViewLine < 0) firstEmptyViewLine = 0;

        var bg = context.Theme.Get(EditorTheme.BackgroundColor);
        var bgAnsi = !bg.IsDefault ? bg.ToBackgroundAnsi() : "";
        var resetAnsi = bgAnsi.Length > 0 ? "\x1b[0m" : "";

        for (var viewLine = firstEmptyViewLine; viewLine < viewportLines; viewLine++)
        {
            var screenY = viewport.Y + viewLine;
            var blankLine = new string(' ', viewport.Width);
            context.WriteClipped(viewport.X, screenY, $"{bgAnsi}{blankLine}{resetAnsi}");
        }
    }

    /// <inheritdoc />
    public DocumentOffset? HitTest(int localX, int localY, EditorState state,
        int viewportColumns, int viewportLines, int scrollOffset, int horizontalScrollOffset)
        => TextEditorViewRenderer.Instance.HitTest(localX, localY, state,
            viewportColumns, viewportLines, scrollOffset, horizontalScrollOffset);

    /// <inheritdoc />
    public int GetTotalLines(IHex1bDocument document, int viewportColumns)
        => TextEditorViewRenderer.Instance.GetTotalLines(document, viewportColumns);

    /// <inheritdoc />
    public int GetMaxLineWidth(IHex1bDocument document, int scrollOffset, int viewportLines, int viewportColumns)
        => TextEditorViewRenderer.Instance.GetMaxLineWidth(document, scrollOffset, viewportLines, viewportColumns);
}
