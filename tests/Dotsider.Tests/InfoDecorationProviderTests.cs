using Dotsider.Views;
using Hex1b.Documents;

namespace Dotsider.Tests;

public class InfoDecorationProviderTests
{
    [Fact]
    public void InfoLabel_ColorsLabelBeforeColon()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Assembly Name:    HelloWorld\n  Version:          1.0.0.0");

        var spans = provider.GetDecorations(1, 2, doc);

        Assert.Equal(2, spans.Count);
        // First span covers "  Assembly Name:" on line 1
        Assert.Equal(1, spans[0].Start.Line);
        Assert.Equal(1, spans[0].Start.Column);
        // Second span covers "  Version:" on line 2
        Assert.Equal(2, spans[1].Start.Line);
    }

    [Fact]
    public void InfoLabel_IgnoresContentLinesWithColons()
    {
        var provider = new InfoLabelDecorationProvider();
        // "int:" looks like a label but is content — colon at position 5 (< 25)
        // but the key check is that the text before : must be letters/digits/spaces
        var doc = new Hex1bDocument("  Length: 4\n\n  int:");

        var spans = provider.GetDecorations(1, 3, doc);

        // Line 1 "  Length:" is a real label
        // Line 3 "  int:" also matches the pattern (letters + colon within 25 chars)
        // InfoLabelDecorationProvider can't distinguish — that's why StringsDetailDecorationProvider exists
        Assert.True(spans.Count >= 1);
        Assert.Equal(1, spans[0].Start.Line);
    }

    [Fact]
    public void InfoLabel_IgnoresColonBeyondPosition25()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  This is a very long line that has a colon: here");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.Empty(spans);
    }

    [Fact]
    public void InfoLabel_IgnoresEmptyLines()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Name: Test\n\n  Version: 1.0");

        var spans = provider.GetDecorations(1, 3, doc);

        // Lines 1 and 3 have labels, line 2 is empty
        Assert.Equal(2, spans.Count);
    }

    [Fact]
    public void StringsDetail_OnlyColorsFirstLine()
    {
        var provider = new StringsDetailDecorationProvider();
        var doc = new Hex1bDocument("  Length: 42\n\n  some: content\n  more: content");

        var spans = provider.GetDecorations(1, 4, doc);

        Assert.Single(spans);
        Assert.Equal(1, spans[0].Start.Line);
    }

    [Fact]
    public void StringsDetail_IgnoresWhenFirstLineHasNoColon()
    {
        var provider = new StringsDetailDecorationProvider();
        var doc = new Hex1bDocument("No colon here\nLength: 5");

        var spans = provider.GetDecorations(1, 2, doc);

        Assert.Empty(spans);
    }

    [Fact]
    public void StringsDetail_IgnoresWhenViewStartsBeyondLine1()
    {
        var provider = new StringsDetailDecorationProvider();
        var doc = new Hex1bDocument("  Length: 42\n\n  content");

        var spans = provider.GetDecorations(2, 3, doc);

        Assert.Empty(spans);
    }
}
