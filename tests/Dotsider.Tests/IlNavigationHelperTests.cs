using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests cursor mapping helpers for the IL editor.
/// </summary>
[TestClass]
public sealed class IlNavigationHelperTests
{
    /// <summary>
    /// Verifies a source comment line resolves to the following instruction's Source Link URL.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        Assert.AreEqual(url, actual);
    }

    /// <summary>
    /// Verifies a source comment line without a marker still resolves to the following instruction's Source Link URL.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetSourceLinkUrlAtCursor_SourceCommentLineWithoutMarker_ReturnsUrl()
    {
        const string url = "https://raw.githubusercontent.com/willibrandon/dotsider/abc/UserService.cs";
        var editorState = CreateEditorState("// UserService.cs(2,1)-(2,2)\nIL_0001: nop");
        var instructions = new[]
        {
            new IlInstruction(
                1,
                "nop",
                "",
                SequenceStartLine: 2,
                SourceLinkUrl: url,
                DisplayLine: 2)
        };

        var actual = IlNavigationHelper.GetSourceLinkUrlAtCursor(editorState, instructions);

        Assert.AreEqual(url, actual);
    }

    /// <summary>
    /// Verifies a hidden source marker line does not resolve as a Source Link target.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetSourceLinkUrlAtCursor_HiddenLine_ReturnsNull()
    {
        var editorState = CreateEditorState("// (hidden)\nIL_0000: nop");
        var instructions = new[]
        {
            new IlInstruction(
                0,
                "nop",
                "",
                SequenceStartLine: 0xFEEFEE,
                SequenceHidden: true,
                SourceLinkUrl: "https://example.test/UserService.cs",
                DisplayLine: 2)
        };

        var actual = IlNavigationHelper.GetSourceLinkUrlAtCursor(editorState, instructions);

        Assert.IsNull(actual);
    }

    /// <summary>
    /// Verifies a source comment line resolves the rendered Source Link marker range when the marker is present.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetSourceLinkYankRangeAtCursor_SourceCommentLineWithMarker_ReturnsMarkerRange()
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

        var actual = IlNavigationHelper.GetSourceLinkYankRangeAtCursor(editorState, instructions);

        var markerStart = line.IndexOf(IlSourceLinkDecorationProvider.SourceLinkMarker, StringComparison.Ordinal);
        Assert.IsNotNull(actual);
        Assert.AreEqual(new DocumentPosition(1, markerStart + 1), actual.Value.Start);
        Assert.AreEqual(
            new DocumentPosition(
                1,
                markerStart + IlSourceLinkDecorationProvider.SourceLinkMarker.Length + 1),
            actual.Value.End);
    }

    /// <summary>
    /// Verifies a source comment line without a marker resolves the source range text for yank flash.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetSourceLinkYankRangeAtCursor_SourceCommentLineWithoutMarker_ReturnsSourceRange()
    {
        const string sourceRange = "UserService.cs(2,1)-(2,2)";
        var editorState = CreateEditorState($"// {sourceRange}\nIL_0001: nop");
        var instructions = new[]
        {
            new IlInstruction(
                1,
                "nop",
                "",
                SequenceStartLine: 2,
                SourceLinkUrl: "https://example.test/UserService.cs",
                DisplayLine: 2)
        };

        var actual = IlNavigationHelper.GetSourceLinkYankRangeAtCursor(editorState, instructions);

        Assert.IsNotNull(actual);
        Assert.AreEqual(new DocumentPosition(1, 4), actual.Value.Start);
        Assert.AreEqual(new DocumentPosition(1, sourceRange.Length + 4), actual.Value.End);
    }

    /// <summary>
    /// Verifies source comment lines do not resolve as instruction lines for go-to-definition.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        Assert.IsNull(actual);
    }

    private static EditorState CreateEditorState(string text)
    {
        var editorState = new EditorState(new Hex1bDocument(text));
        editorState.SetCursorPosition(new DocumentOffset(0));
        return editorState;
    }
}
