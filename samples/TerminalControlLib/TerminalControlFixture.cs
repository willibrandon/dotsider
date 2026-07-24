namespace TerminalControlLib;

/// <summary>
/// Supplies compiler-emitted metadata containing terminal and Unicode formatting controls.
/// </summary>
public static class TerminalControlFixture
{
    /// <summary>
    /// Gets a value containing OSC, CSI, C0, DEL, C1, and bidirectional formatting controls.
    /// </summary>
    /// <returns>The hostile terminal payload.</returns>
    public static string GetPayload() =>
        "terminal-prefix\u001B]52;c;cHduZWQ=\u0007-\u001B[31mred\u001B[0m"
        + "\u007F\u0085\u009B\u202E\u2066\u2028\u2029-terminal-suffix";
}
