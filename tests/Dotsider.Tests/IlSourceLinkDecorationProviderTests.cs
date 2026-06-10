using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Documents;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Source Link marker decorations in the IL editor.
/// </summary>
public sealed class IlSourceLinkDecorationProviderTests
{
    /// <summary>
    /// Verifies Source Link markers are underlined when the instruction has a resolved URL.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetDecorations_SourceLinkMarker_ReturnsUnderlineSpan()
    {
        var line = "// UserService.cs(1,1)-(1,2) [source link]";
        var document = new Hex1bDocument($"{line}\nIL_0000: nop");
        var provider = new IlSourceLinkDecorationProvider
        {
            Instructions =
            [
                new IlInstruction(
                    0,
                    "nop",
                    "",
                    SequenceStartLine: 1,
                    SourceLinkUrl: "https://example.test/UserService.cs",
                    DisplayLine: 2)
            ]
        };

        var spans = provider.GetDecorations(1, 2, document);

        var span = Assert.Single(spans);
        var markerStart = line.IndexOf(IlSourceLinkDecorationProvider.SourceLinkMarker, StringComparison.Ordinal);
        Assert.Equal(new DocumentPosition(1, markerStart + 1), span.Start);
        Assert.Equal(new DocumentPosition(
            1,
            markerStart + IlSourceLinkDecorationProvider.SourceLinkMarker.Length + 1), span.End);
        Assert.Equal(UnderlineStyle.Single, span.Decoration.UnderlineStyle);
        Assert.Equal(12, span.Priority);
    }

    /// <summary>
    /// Verifies markers without resolved Source Link URLs are not decorated.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetDecorations_NoSourceLinkUrl_ReturnsEmpty()
    {
        var document = new Hex1bDocument("// UserService.cs(1,1)-(1,2) [source link]\nIL_0000: nop");
        var provider = new IlSourceLinkDecorationProvider
        {
            Instructions =
            [
                new IlInstruction(
                    0,
                    "nop",
                    "",
                    SequenceStartLine: 1,
                    DisplayLine: 2)
            ]
        };

        var spans = provider.GetDecorations(1, 2, document);

        Assert.Empty(spans);
    }
}
