using Dotsider.Views;

namespace Dotsider.Tests;

/// <summary>
/// Tests shell-free parsing of configured editor commands.
/// </summary>
[TestClass]
public sealed class EditorCommandTests
{
    /// <summary>
    /// Verifies common configured editor commands preserve their executable and arguments.
    /// </summary>
    /// <param name="value">The configured command.</param>
    /// <param name="expectedExecutable">The expected executable token.</param>
    /// <param name="expectedArguments">The expected arguments joined with a separator.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("code --wait", "code", "--wait")]
    [DataRow(
        "\"C:\\Program Files\\Microsoft VS Code\\Code.exe\" --wait",
        @"C:\Program Files\Microsoft VS Code\Code.exe",
        "--wait")]
    [DataRow(
        "open -a \"Visual Studio Code\"",
        "open",
        "-a\u001fVisual Studio Code")]
    [DataRow(
        "editor pre\"joined value\"post",
        "editor",
        "prejoined valuepost")]
    [DataRow(
        "editor 'single quoted' \"double quoted\"",
        "editor",
        "single quoted\u001fdouble quoted")]
    [DataRow("editor \"\" tail", "editor", "\u001ftail")]
    [DataRow(@"editor escaped\ value", "editor", "escaped value")]
    [DataRow("editor \"escaped\\\"quote\"", "editor", "escaped\"quote")]
    [DataRow("editor 'escaped\\'quote'", "editor", "escaped'quote")]
    [DataRow(@"editor two\\slashes", "editor", @"two\slashes")]
    [DataRow("editor 'double\\\"quote'", "editor", "double\\\"quote")]
    public void TryParse_ValidCommand_PreservesLiteralTokens(
        string value,
        string expectedExecutable,
        string expectedArguments)
    {
        var parsed = EditorCommand.TryParse(value, out var command);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(command);
        Assert.AreEqual(expectedExecutable, command.Executable);
        Assert.AreEqual(expectedArguments, string.Join('\u001f', command.Arguments));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<string>)command.Arguments)[0] = "changed");
    }

    /// <summary>
    /// Verifies malformed commands and unsupported shell operators fail closed.
    /// </summary>
    /// <param name="value">The malformed or shell-dependent command.</param>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("editor \"unterminated")]
    [DataRow("editor trailing\\")]
    [DataRow("editor\0argument")]
    [DataRow("editor\rargument")]
    [DataRow("editor\nargument")]
    [DataRow("editor | pager")]
    [DataRow("editor && other")]
    [DataRow("editor ; other")]
    [DataRow("editor > output")]
    [DataRow("editor < input")]
    [DataRow("editor || other")]
    [DataRow("editor $(other)")]
    [DataRow("editor ${OTHER}")]
    [DataRow("editor `other`")]
    public void TryParse_MalformedOrShellDependentCommand_ReturnsFalse(string value)
    {
        var parsed = EditorCommand.TryParse(value, out var command);

        Assert.IsFalse(parsed);
        Assert.IsNull(command);
    }

    /// <summary>
    /// Verifies variable-like text remains literal and is never expanded by the parser.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryParse_VariableText_PreservesLiteralValue()
    {
        var parsed = EditorCommand.TryParse(
            "editor \"%PATH%\" '$HOME' !VALUE! *.cs",
            out var command);

        Assert.IsTrue(parsed);
        Assert.IsNotNull(command);
        Assert.AreEqual("%PATH%", command.Arguments[0]);
        Assert.AreEqual("$HOME", command.Arguments[1]);
        Assert.AreEqual("!VALUE!", command.Arguments[2]);
        Assert.AreEqual("*.cs", command.Arguments[3]);
    }
}
