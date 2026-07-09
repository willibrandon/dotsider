using Dotsider.Views;
using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="IlSearchDecorationProvider"/> which highlights search matches
/// in the IL disassembly editor.
/// </summary>
[TestClass]
public class IlSearchDecorationProviderTests
{
    /// <summary>
    /// Verifies null query returns empty.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NullQuery_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("hello world");
        var provider = new IlSearchDecorationProvider { Query = null };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.IsEmpty(spans);
    }

    /// <summary>
    /// Verifies empty query returns empty.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EmptyQuery_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("hello world");
        var provider = new IlSearchDecorationProvider { Query = "" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.IsEmpty(spans);
    }

    /// <summary>
    /// Verifies single match returns a readable match span.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SingleMatch_ReturnsReadableMatchSpan()
    {
        var doc = new Hex1bDocument("hello world");
        var provider = new IlSearchDecorationProvider { Query = "world" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.ContainsSingle(spans);
        var span = spans[0];

        // "world" starts at index 6, so 1-based column = 7
        Assert.AreEqual(new DocumentPosition(1, 7), span.Start);
        Assert.AreEqual(new DocumentPosition(1, 12), span.End); // exclusive end
        Assert.AreEqual(10, span.Priority);
        Assert.IsNotNull(span.Decoration.Background);
        AssertColorEquals(HighlightHelper.MatchBgColor, span.Decoration.Background.Value);
        Assert.IsNotNull(span.Decoration.Foreground);
        AssertColorEquals(HighlightHelper.MatchFgColor, span.Decoration.Foreground.Value);
    }

    /// <summary>
    /// Verifies current match returns orange span.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        Assert.ContainsSingle(spans);
        var span = spans[0];

        Assert.AreEqual(new DocumentPosition(1, 7), span.Start);
        Assert.AreEqual(new DocumentPosition(1, 12), span.End);
        Assert.AreEqual(20, span.Priority);
        Assert.IsNotNull(span.Decoration.Background);
        AssertColorEquals(HighlightHelper.CurrentMatchBgColor, span.Decoration.Background.Value);
        Assert.IsNotNull(span.Decoration.Foreground);
        AssertColorEquals(HighlightHelper.MatchFgColor, span.Decoration.Foreground.Value);
    }

    /// <summary>
    /// Verifies editor search colors clear WCAG AA contrast for normal and current matches.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SearchMatchColors_ClearWcagAa()
    {
        Assert.IsGreaterThanOrEqualTo(4.5, ContrastRatio(HighlightHelper.MatchFgColor, HighlightHelper.MatchBgColor));
        Assert.IsGreaterThanOrEqualTo(4.5, ContrastRatio(HighlightHelper.MatchFgColor, HighlightHelper.CurrentMatchBgColor));
    }

    /// <summary>
    /// Verifies case insensitive finds match.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CaseInsensitive_FindsMatch()
    {
        var doc = new Hex1bDocument("HELLO");
        var provider = new IlSearchDecorationProvider { Query = "hello" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.ContainsSingle(spans);
        var span = spans[0];
        Assert.AreEqual(new DocumentPosition(1, 1), span.Start);
        Assert.AreEqual(new DocumentPosition(1, 6), span.End);
    }

    /// <summary>
    /// Verifies multiple matches per line returns all.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MultipleMatchesPerLine_ReturnsAll()
    {
        var doc = new Hex1bDocument("abc abc abc");
        var provider = new IlSearchDecorationProvider { Query = "abc" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.HasCount(3, spans);

        // First match: columns 1-4
        Assert.AreEqual(new DocumentPosition(1, 1), spans[0].Start);
        Assert.AreEqual(new DocumentPosition(1, 4), spans[0].End);
        Assert.AreEqual(10, spans[0].Priority);

        // Second match: columns 5-8
        Assert.AreEqual(new DocumentPosition(1, 5), spans[1].Start);
        Assert.AreEqual(new DocumentPosition(1, 8), spans[1].End);
        Assert.AreEqual(10, spans[1].Priority);

        // Third match: columns 9-12
        Assert.AreEqual(new DocumentPosition(1, 9), spans[2].Start);
        Assert.AreEqual(new DocumentPosition(1, 12), spans[2].End);
        Assert.AreEqual(10, spans[2].Priority);
    }

    /// <summary>
    /// Verifies no match returns empty.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NoMatch_ReturnsEmpty()
    {
        var doc = new Hex1bDocument("hello");
        var provider = new IlSearchDecorationProvider { Query = "xyz" };

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.IsEmpty(spans);
    }

    private static void AssertColorEquals(Hex1bColor expected, Hex1bColor actual)
    {
        Assert.AreEqual(expected.R, actual.R);
        Assert.AreEqual(expected.G, actual.G);
        Assert.AreEqual(expected.B, actual.B);
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
