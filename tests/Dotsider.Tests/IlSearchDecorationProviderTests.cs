using Dotsider.Views;
using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="IlSearchDecorationProvider"/> which highlights search matches
/// in the IL disassembly editor.
/// </summary>
public class IlSearchDecorationProviderTests
{
    /// <summary>
    /// Verifies null query returns empty.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NullQuery_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("hello world");
        var provider = new IlSearchDecorationProvider { Query = null };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Empty(spans);
    }

    /// <summary>
    /// Verifies empty query returns empty.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmptyQuery_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("hello world");
        var provider = new IlSearchDecorationProvider { Query = "" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Empty(spans);
    }

    /// <summary>
    /// Verifies single match returns a readable match span.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SingleMatch_ReturnsReadableMatchSpan()
    {
        var doc = new Hex1bDocument("hello world");
        var provider = new IlSearchDecorationProvider { Query = "world" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Single(spans);
        var span = spans[0];

        // "world" starts at index 6, so 1-based column = 7
        Assert.Equal(new DocumentPosition(1, 7), span.Start);
        Assert.Equal(new DocumentPosition(1, 12), span.End); // exclusive end
        Assert.Equal(10, span.Priority);
        Assert.NotNull(span.Decoration.Background);
        AssertColorEquals(HighlightHelper.MatchBgColor, span.Decoration.Background.Value);
        Assert.NotNull(span.Decoration.Foreground);
        AssertColorEquals(HighlightHelper.MatchFgColor, span.Decoration.Foreground.Value);
    }

    /// <summary>
    /// Verifies current match returns orange span.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void CurrentMatch_ReturnsOrangeSpan()
    {
        var doc = new Hex1bDocument("hello world");
        var provider = new IlSearchDecorationProvider
        {
            Query = "world",
            CurrentMatchStart = new DocumentPosition(1, 7),
            CurrentMatchLength = 5
        };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Single(spans);
        var span = spans[0];

        Assert.Equal(new DocumentPosition(1, 7), span.Start);
        Assert.Equal(new DocumentPosition(1, 12), span.End);
        Assert.Equal(20, span.Priority);
        Assert.NotNull(span.Decoration.Background);
        AssertColorEquals(HighlightHelper.CurrentMatchBgColor, span.Decoration.Background.Value);
        Assert.NotNull(span.Decoration.Foreground);
        AssertColorEquals(HighlightHelper.MatchFgColor, span.Decoration.Foreground.Value);
    }

    /// <summary>
    /// Verifies editor search colors clear WCAG AA contrast for normal and current matches.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SearchMatchColors_ClearWcagAa()
    {
        Assert.True(ContrastRatio(HighlightHelper.MatchFgColor, HighlightHelper.MatchBgColor) >= 4.5);
        Assert.True(ContrastRatio(HighlightHelper.MatchFgColor, HighlightHelper.CurrentMatchBgColor) >= 4.5);
    }

    /// <summary>
    /// Verifies case insensitive finds match.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void CaseInsensitive_FindsMatch()
    {
        var doc = new Hex1bDocument("HELLO");
        var provider = new IlSearchDecorationProvider { Query = "hello" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Single(spans);
        var span = spans[0];
        Assert.Equal(new DocumentPosition(1, 1), span.Start);
        Assert.Equal(new DocumentPosition(1, 6), span.End);
    }

    /// <summary>
    /// Verifies multiple matches per line returns all.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MultipleMatchesPerLine_ReturnsAll()
    {
        var doc = new Hex1bDocument("abc abc abc");
        var provider = new IlSearchDecorationProvider { Query = "abc" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Equal(3, spans.Count);

        // First match: columns 1-4
        Assert.Equal(new DocumentPosition(1, 1), spans[0].Start);
        Assert.Equal(new DocumentPosition(1, 4), spans[0].End);
        Assert.Equal(10, spans[0].Priority);

        // Second match: columns 5-8
        Assert.Equal(new DocumentPosition(1, 5), spans[1].Start);
        Assert.Equal(new DocumentPosition(1, 8), spans[1].End);
        Assert.Equal(10, spans[1].Priority);

        // Third match: columns 9-12
        Assert.Equal(new DocumentPosition(1, 9), spans[2].Start);
        Assert.Equal(new DocumentPosition(1, 12), spans[2].End);
        Assert.Equal(10, spans[2].Priority);
    }

    /// <summary>
    /// Verifies no match returns empty.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NoMatch_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("hello");
        var provider = new IlSearchDecorationProvider { Query = "xyz" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Empty(spans);
    }

    private static void AssertColorEquals(Hex1bColor expected, Hex1bColor actual)
    {
        Assert.Equal(expected.R, actual.R);
        Assert.Equal(expected.G, actual.G);
        Assert.Equal(expected.B, actual.B);
    }

    private static double ContrastRatio(Hex1bColor a, Hex1bColor b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        if (l1 < l2)
            (l1, l2) = (l2, l1);
        return (l1 + 0.05) / (l2 + 0.05);
    }

    private static double RelativeLuminance(Hex1bColor color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R)
            + 0.7152 * Channel(color.G)
            + 0.0722 * Channel(color.B);
    }
}
