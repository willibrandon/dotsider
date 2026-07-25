using Dotsider.Views;

namespace Dotsider.Tests;

/// <summary>
/// Verifies shell-free trace argument tokenization and formatting.
/// </summary>
[TestClass]
public sealed class ShellFreeArgumentTokenizerTests
{
    /// <summary>
    /// Verifies whitespace, quotes, adjacent segments, empty values, and escapes.
    /// </summary>
    [TestMethod]
    public void TryTokenize_ValidText_PreservesLiteralArguments()
    {
        var success = ShellFreeArgumentTokenizer.TryTokenize(
            """alpha "two words" '' pre"joined"post escaped\ value quote\"value slash\\value""",
            rejectShellOperators: false,
            out var arguments);

        Assert.IsTrue(success);
        AssertArguments(
            [
                "alpha",
                "two words",
                "",
                "prejoinedpost",
                "escaped value",
                "quote\"value",
                "slash\\value"
            ],
            arguments);
    }

    /// <summary>
    /// Verifies shell syntax has no special meaning in trace arguments.
    /// </summary>
    [TestMethod]
    public void TryTokenize_ShellMetacharacters_TreatsValuesLiterally()
    {
        var success = ShellFreeArgumentTokenizer.TryTokenize(
            "& ; | > < ` $(whoami) ${HOME} *.dll",
            rejectShellOperators: false,
            out var arguments);

        Assert.IsTrue(success);
        AssertArguments(
            ["&", ";", "|", ">", "<", "`", "$(whoami)", "${HOME}", "*.dll"],
            arguments);
    }

    /// <summary>
    /// Verifies malformed quoting and escaping are rejected.
    /// </summary>
    /// <param name="value">The malformed text.</param>
    [TestMethod]
    [DataRow("\"unterminated")]
    [DataRow("'unterminated")]
    [DataRow("trailing\\")]
    [DataRow("line\nbreak")]
    public void TryTokenize_MalformedText_Rejects(string value)
    {
        Assert.IsFalse(ShellFreeArgumentTokenizer.TryTokenize(
            value,
            rejectShellOperators: false,
            out _));
    }

    /// <summary>
    /// Verifies formatting produces text that round-trips every literal value.
    /// </summary>
    [TestMethod]
    public void Format_LiteralArguments_RoundTrips()
    {
        string[] expected =
        [
            "",
            "plain",
            "two words",
            "single'quote",
            "double\"quote",
            "slash\\value",
            "a&b"
        ];

        var formatted = ShellFreeArgumentTokenizer.Format(expected);
        var success = ShellFreeArgumentTokenizer.TryTokenize(
            formatted,
            rejectShellOperators: false,
            out var actual);

        Assert.IsTrue(success);
        AssertArguments(expected, actual);
    }

    private static void AssertArguments(
        string[] expected,
        string[]? actual)
    {
        Assert.IsNotNull(actual);
        Assert.HasCount(expected.Length, actual);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.AreEqual(expected[index], actual[index], $"Argument {index} differs.");
        }
    }
}
