using Dotsider.Views;

namespace Dotsider.Tests;

/// <summary>
/// Tests terminal-safe presentation of untrusted text.
/// </summary>
[TestClass]
public sealed class UntrustedTerminalTextTests
{
    /// <summary>
    /// Verifies truncation never leaves an unmatched surrogate at the preview boundary.
    /// </summary>
    [TestMethod]
    public void TruncateWithEllipsis_SupplementaryRuneAtBoundary_PreservesValidUtf16()
    {
        var value = new string('a', 36) + "\U0001F600tail";

        var result = UntrustedTerminalText.TruncateWithEllipsis(value, 40);

        Assert.AreEqual(new string('a', 36) + "...", result);
    }
}
