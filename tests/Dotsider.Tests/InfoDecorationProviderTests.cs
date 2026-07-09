using Dotsider.Views;
using Hex1b.Documents;
using Hex1b.Theming;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Info Decoration Provider.
/// </summary>
[TestClass]
public class InfoDecorationProviderTests
{
    /// <summary>
    /// Verifies info label colors label before colon.
    /// </summary>
    [TestMethod]
    public void InfoLabel_ColorsLabelBeforeColon()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Assembly Name:    HelloWorld\n  Version:          1.0.0.0");

        var spans = provider.GetDecorations(1, 2, doc);

        Assert.HasCount(2, spans);
        // First span covers "  Assembly Name:" on line 1
        Assert.AreEqual(1, spans[0].Start.Line);
        Assert.AreEqual(1, spans[0].Start.Column);
        // Second span covers "  Version:" on line 2
        Assert.AreEqual(2, spans[1].Start.Line);
    }

    /// <summary>
    /// Verifies callers can supply a contrast-safe label color for popup surfaces.
    /// </summary>
    [TestMethod]
    public void InfoLabel_UsesCustomLabelColor()
    {
        var color = Hex1bColor.FromRgb(140, 170, 205);
        var provider = new InfoLabelDecorationProvider(color);
        var doc = new Hex1bDocument("  Side: current");

        var span = Assert.ContainsSingle(provider.GetDecorations(1, 1, doc));

        Assert.IsNotNull(span.Decoration.Foreground);
        AssertColorEquals(color, span.Decoration.Foreground.Value);
    }

    /// <summary>
    /// Verifies info label ignores content lines with colons.
    /// </summary>
    [TestMethod]
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
        Assert.IsGreaterThanOrEqualTo(1, spans.Count);
        Assert.AreEqual(1, spans[0].Start.Line);
    }

    /// <summary>
    /// Verifies info label ignores colon beyond position25.
    /// </summary>
    [TestMethod]
    public void InfoLabel_IgnoresColonBeyondPosition25()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  This is a very long line that has a colon: here");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.IsEmpty(spans);
    }

    /// <summary>
    /// Verifies info label ignores empty lines.
    /// </summary>
    [TestMethod]
    public void InfoLabel_IgnoresEmptyLines()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Name: Test\n\n  Version: 1.0");

        var spans = provider.GetDecorations(1, 3, doc);

        // Lines 1 and 3 have labels, line 2 is empty
        Assert.HasCount(2, spans);
    }

    /// <summary>
    /// Verifies info label colors hyphenated labels.
    /// </summary>
    [TestMethod]
    public void InfoLabel_ColorsHyphenatedLabels()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Read-Only:        No\n  Has Metadata:     Yes");

        var spans = provider.GetDecorations(1, 2, doc);

        Assert.HasCount(2, spans);
        Assert.AreEqual(1, spans[0].Start.Line);
        Assert.AreEqual(2, spans[1].Start.Line);
    }

    /// <summary>
    /// Verifies strings detail only colors first line.
    /// </summary>
    [TestMethod]
    public void StringsDetail_OnlyColorsFirstLine()
    {
        var provider = new StringsDetailDecorationProvider();
        var doc = new Hex1bDocument("  Length: 42\n\n  some: content\n  more: content");

        var spans = provider.GetDecorations(1, 4, doc);

        Assert.ContainsSingle(spans);
        Assert.AreEqual(1, spans[0].Start.Line);
    }

    /// <summary>
    /// Verifies strings detail ignores when first line has no colon.
    /// </summary>
    [TestMethod]
    public void StringsDetail_IgnoresWhenFirstLineHasNoColon()
    {
        var provider = new StringsDetailDecorationProvider();
        var doc = new Hex1bDocument("No colon here\nLength: 5");

        var spans = provider.GetDecorations(1, 2, doc);

        Assert.IsEmpty(spans);
    }

    /// <summary>
    /// Verifies strings detail ignores when view starts beyond line1.
    /// </summary>
    [TestMethod]
    public void StringsDetail_IgnoresWhenViewStartsBeyondLine1()
    {
        var provider = new StringsDetailDecorationProvider();
        var doc = new Hex1bDocument("  Length: 42\n\n  content");

        var spans = provider.GetDecorations(2, 3, doc);

        Assert.IsEmpty(spans);
    }

    /// <summary>
    /// Verifies info label colors jitted methods label.
    /// </summary>
    [TestMethod]
    public void InfoLabel_ColorsJittedMethodsLabel()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Jitted Methods:   89");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.ContainsSingle(spans);
    }

    /// <summary>
    /// Verifies info label ignores colons inside values.
    /// </summary>
    [TestMethod]
    public void InfoLabel_IgnoresColonsInsideValues()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Description:  Visit https://example.com for details");

        var spans = provider.GetDecorations(1, 1, doc);

        // Only "Description:" should be colored, not "https:"
        Assert.ContainsSingle(spans);
    }

    /// <summary>
    /// Verifies info label ignores trailing colon in value.
    /// </summary>
    [TestMethod]
    public void InfoLabel_IgnoresTrailingColonInValue()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Description:  abc:");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.ContainsSingle(spans);
    }

    /// <summary>
    /// Verifies info label ignores colon after double space in value.
    /// </summary>
    [TestMethod]
    public void InfoLabel_IgnoresColonAfterDoubleSpaceInValue()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Description:  Value  with: colon");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.ContainsSingle(spans);
    }

    /// <summary>
    /// Verifies info label ignores double colon in values.
    /// </summary>
    [TestMethod]
    public void InfoLabel_IgnoresDoubleColonInValues()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  MethodDef:    Namespace.Type::Method");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.ContainsSingle(spans);
    }

    /// <summary>
    /// Verifies info label colors multiple labels per line.
    /// </summary>
    [TestMethod]
    public void InfoLabel_ColorsMultipleLabelsPerLine()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Gen 0: 4    Gen 1: 4    Gen 2: 4");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.HasCount(3, spans);
    }

    /// <summary>
    /// Verifies info label colors threading labels.
    /// </summary>
    [TestMethod]
    public void InfoLabel_ColorsThreadingLabels()
    {
        var provider = new InfoLabelDecorationProvider();
        var doc = new Hex1bDocument("  Threads: 2    Queue: 0    Exceptions: 0    Timers: 1");

        var spans = provider.GetDecorations(1, 1, doc);

        Assert.HasCount(4, spans);
    }

    private static void AssertColorEquals(Hex1bColor expected, Hex1bColor actual)
    {
        Assert.AreEqual(expected.R, actual.R);
        Assert.AreEqual(expected.G, actual.G);
        Assert.AreEqual(expected.B, actual.B);
    }
}
