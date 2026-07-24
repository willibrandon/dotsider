namespace Dotsider.Tests;

/// <summary>
/// Tests terminal-safe search highlighting.
/// </summary>
[TestClass]
public sealed class HighlightHelperTests
{
    /// <summary>
    /// Verifies text is escaped even when no search query is active.
    /// </summary>
    [TestMethod]
    public void HighlightSubstring_NoQuery_EscapesUntrustedText()
    {
        var result = HighlightHelper.HighlightSubstring("before\u001B[31mafter", null);

        Assert.AreEqual("before␛[31mafter", result);
    }

    /// <summary>
    /// Verifies untrusted text is escaped before trusted highlight ANSI is added.
    /// </summary>
    [TestMethod]
    public void HighlightSubstring_MatchingQuery_EscapesEveryEmittedSpan()
    {
        var result = HighlightHelper.HighlightSubstring(
            "before\u001B[31mMATCH\u0007after",
            "match");

        Assert.Contains("before␛[31m", result);
        Assert.Contains("MATCH", result);
        Assert.Contains("␇after", result);
        Assert.DoesNotContain("\u001B[31mMATCH", result);
    }
}
