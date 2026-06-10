using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests cursor mapping helpers for the IL editor.
/// </summary>
public sealed class IlNavigationHelperTests
{
    /// <summary>
    /// Verifies a source comment line resolves to the following instruction's Source Link URL.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetSourceLinkUrlAtCursor_SourceCommentLine_ReturnsUrl()
    {
        const string url = "https://raw.githubusercontent.com/willibrandon/dotsider/abc/UserService.cs";
        var editorState = CreateEditorState("// UserService.cs(1,1)-(1,2) [source link]\nIL_0000: nop");
        var instructions = new[]
        {
            new IlInstruction(
                0,
                "nop",
                "",
                SequenceStartLine: 1,
                SourceLinkUrl: url,
                DisplayLine: 2)
        };

        var actual = IlNavigationHelper.GetSourceLinkUrlAtCursor(editorState, instructions);

        Assert.Equal(url, actual);
    }

    /// <summary>
    /// Verifies a source comment line resolves the rendered Source Link marker range.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetSourceLinkMarkerRangeAtCursor_SourceCommentLine_ReturnsMarkerRange()
    {
        const string line = "// UserService.cs(1,1)-(1,2) [source link]";
        var editorState = CreateEditorState($"{line}\nIL_0000: nop");
        var instructions = new[]
        {
            new IlInstruction(
                0,
                "nop",
                "",
                SequenceStartLine: 1,
                SourceLinkUrl: "https://example.test/UserService.cs",
                DisplayLine: 2)
        };

        var actual = IlNavigationHelper.GetSourceLinkMarkerRangeAtCursor(editorState, instructions);

        var markerStart = line.IndexOf(IlSourceLinkDecorationProvider.SourceLinkMarker, StringComparison.Ordinal);
        Assert.NotNull(actual);
        Assert.Equal(new DocumentPosition(1, markerStart + 1), actual.Value.Start);
        Assert.Equal(
            new DocumentPosition(
                1,
                markerStart + IlSourceLinkDecorationProvider.SourceLinkMarker.Length + 1),
            actual.Value.End);
    }

    /// <summary>
    /// Verifies source comment lines do not resolve as instruction lines for go-to-definition.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetInstructionAtCursor_SourceCommentLine_ReturnsNull()
    {
        var editorState = CreateEditorState("// UserService.cs(1,1)-(1,2) [source link]\nIL_0000: call Foo::Bar");
        var instructions = new[]
        {
            new IlInstruction(
                0,
                "call",
                "Foo::Bar",
                MetadataToken: 0x06000001,
                SequenceStartLine: 1,
                SourceLinkUrl: "https://example.test/UserService.cs",
                DisplayLine: 2)
        };

        var actual = IlNavigationHelper.GetInstructionAtCursor(editorState, instructions, headerLineCount: 0);

        Assert.Null(actual);
    }

    private static EditorState CreateEditorState(string text)
    {
        var editorState = new EditorState(new Hex1bDocument(text));
        editorState.SetCursorPosition(new DocumentOffset(0));
        return editorState;
    }
}
