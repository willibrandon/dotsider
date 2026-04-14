using Dotsider.Views;
using Hex1b.Documents;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Diff Decoration Provider.
/// </summary>
public class DiffDecorationProviderTests
{
    // --- DiffSearchDecorationProvider ---

    /// <summary>
    /// Verifies diff search highlights all matches case insensitive.
    /// </summary>
    [Fact]
    public void DiffSearch_HighlightsAllMatches_CaseInsensitive()
    {
        var provider = new DiffSearchDecorationProvider { Query = "rich" };
        var doc = new Hex1bDocument("  Name:       RichLibrary\n  Version:    2.5.1\n  References: richData");

        var spans = provider.GetDecorations(1, 3, doc);

        // "Rich" on line 1, "rich" on line 3
        Assert.Equal(2, spans.Count);
        Assert.Equal(1, spans[0].Start.Line);
        Assert.Equal(3, spans[1].Start.Line);
    }

    /// <summary>
    /// Verifies diff search multiple matches on same line.
    /// </summary>
    [Fact]
    public void DiffSearch_MultipleMatchesOnSameLine()
    {
        var provider = new DiffSearchDecorationProvider { Query = "ab" };
        var doc = new Hex1bDocument("ab cd ab ef ab");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Equal(3, spans.Count);
    }

    /// <summary>
    /// Verifies diff search null query returns empty.
    /// </summary>
    [Fact]
    public void DiffSearch_NullQuery_ReturnsEmpty()
    {
        var provider = new DiffSearchDecorationProvider { Query = null };
        var doc = new Hex1bDocument("some text");

        Assert.Empty(provider.GetDecorations(1, 1, doc));
    }

    /// <summary>
    /// Verifies diff search empty query returns empty.
    /// </summary>
    [Fact]
    public void DiffSearch_EmptyQuery_ReturnsEmpty()
    {
        var provider = new DiffSearchDecorationProvider { Query = "" };
        var doc = new Hex1bDocument("some text");

        Assert.Empty(provider.GetDecorations(1, 1, doc));
    }

    /// <summary>
    /// Verifies diff search no match returns empty.
    /// </summary>
    [Fact]
    public void DiffSearch_NoMatch_ReturnsEmpty()
    {
        var provider = new DiffSearchDecorationProvider { Query = "xyz" };
        var doc = new Hex1bDocument("abc def");

        Assert.Empty(provider.GetDecorations(1, 1, doc));
    }

    // --- DiffStatsDecorationProvider ---

    /// <summary>
    /// Verifies diff stats colors stats lines.
    /// </summary>
    [Fact]
    public void DiffStats_ColorsStatsLines()
    {
        var provider = new DiffStatsDecorationProvider();
        var doc = new Hex1bDocument("  Types:      +3  -1  ~2\n  Methods:    +10  -5  ~8");

        var spans = provider.GetDecorations(1, 2, doc);

        // Each line has 3 colored values (+N, -N, ~N) = 6 total
        Assert.Equal(6, spans.Count);
    }

    /// <summary>
    /// Verifies diff stats does not color size delta line.
    /// </summary>
    [Fact]
    public void DiffStats_DoesNotColorSizeDeltaLine()
    {
        var provider = new DiffStatsDecorationProvider();
        var doc = new Hex1bDocument("  Size delta: +10.0 KB");

        var spans = provider.GetDecorations(1, 1, doc);

        // Line has no ~ so it's skipped entirely
        Assert.Empty(spans);
    }

    /// <summary>
    /// Verifies diff stats ignores lines without tilde.
    /// </summary>
    [Fact]
    public void DiffStats_IgnoresLinesWithoutTilde()
    {
        var provider = new DiffStatsDecorationProvider();
        var doc = new Hex1bDocument("  Some text +5 -3 no tilde here");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Empty(spans);
    }

    /// <summary>
    /// Verifies diff stats requires digit after prefix.
    /// </summary>
    [Fact]
    public void DiffStats_RequiresDigitAfterPrefix()
    {
        var provider = new DiffStatsDecorationProvider();
        // +abc should not match, but ~2 should
        var doc = new Hex1bDocument("  +abc -def ~2");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Single(spans);
    }

    /// <summary>
    /// Verifies diff stats empty lines returns empty.
    /// </summary>
    [Fact]
    public void DiffStats_EmptyLines_ReturnsEmpty()
    {
        var provider = new DiffStatsDecorationProvider();
        var doc = new Hex1bDocument("\n\n");

        Assert.Empty(provider.GetDecorations(1, 3, doc));
    }
}
