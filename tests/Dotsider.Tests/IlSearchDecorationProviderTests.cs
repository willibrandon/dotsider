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
    [Fact(Timeout = 30_000)]
    public void NullQuery_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("hello world");
        var provider = new IlSearchDecorationProvider { Query = null };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Empty(spans);
    }

    [Fact(Timeout = 30_000)]
    public void EmptyQuery_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("hello world");
        var provider = new IlSearchDecorationProvider { Query = "" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Empty(spans);
    }

    [Fact(Timeout = 30_000)]
    public void SingleMatch_ReturnsMatchBgSpan()
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
        Assert.Null(span.Decoration.Foreground);
    }

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
        AssertColorEquals(Hex1bColor.FromRgb(255, 165, 0), span.Decoration.Background.Value);
        Assert.NotNull(span.Decoration.Foreground);
        AssertColorEquals(Hex1bColor.Black, span.Decoration.Foreground.Value);
    }

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
}
