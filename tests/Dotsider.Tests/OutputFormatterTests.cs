using Dotsider.Infrastructure;
using System.Text.Json;

namespace Dotsider.Tests;

/// <summary>
/// Tests the human-readable and JSON output presentation boundary.
/// </summary>
[TestClass]
public sealed class OutputFormatterTests
{
    /// <summary>
    /// Verifies a text line cannot emit terminal control sequences.
    /// </summary>
    [TestMethod]
    public void WriteLine_ControlPayload_WritesVisibleRepresentation()
    {
        using var writer = CreateWriter();
        using var formatter = new OutputFormatter(writer);

        formatter.WriteLine("before\u001B[2Jafter\u009B31m");

        Assert.AreEqual("before␛[2Jafter\\u009B31m\n", writer.ToString());
    }

    /// <summary>
    /// Verifies block output keeps logical lines while escaping controls within them.
    /// </summary>
    [TestMethod]
    public void WriteBlock_MixedLineEndings_WritesSanitizedLines()
    {
        using var writer = CreateWriter();
        using var formatter = new OutputFormatter(writer);

        formatter.WriteBlock("first\r\nsecond\u001B]0;owned\u0007\rlast\n");

        Assert.AreEqual("first\nsecond␛]0;owned␇\nlast\n", writer.ToString());
    }

    /// <summary>
    /// Verifies table widths are calculated from the escaped display projection.
    /// </summary>
    [TestMethod]
    public void WriteTable_ControlPayload_AlignsEscapedCells()
    {
        using var writer = CreateWriter();
        using var formatter = new OutputFormatter(writer);
        string[][] rows =
        [
            ["A\u009B", "one"],
            ["Long", "two"]
        ];

        formatter.WriteTable(["Name", "Value"], rows);

        Assert.AreEqual(
            "Name     Value\n"
            + "-------  -----\n"
            + "A\\u009B  one\n"
            + "Long     two\n",
            writer.ToString());
    }

    /// <summary>
    /// Verifies stderr presentation uses the same inline escaping policy.
    /// </summary>
    [TestMethod]
    public void WriteError_ControlPayload_WritesVisibleRepresentation()
    {
        using var writer = CreateWriter();

        OutputFormatter.WriteError(writer, "bad\u001B[31m\r\nnext");

        Assert.AreEqual("bad␛[31m␍␊next\n", writer.ToString());
    }

    /// <summary>
    /// Verifies JSON mode preserves the exact value after JSON decoding.
    /// </summary>
    [TestMethod]
    public void WriteJson_ControlPayload_RoundTripsExactValue()
    {
        const string Value = "before\u001B]52;c;payload\u0007\u009Bafter";
        using var writer = CreateWriter();
        using var formatter = new OutputFormatter(writer) { JsonMode = true };

        formatter.WriteJson(new { Value });

        using var document = JsonDocument.Parse(writer.ToString());
        Assert.AreEqual(Value, document.RootElement.GetProperty("value").GetString());
        Assert.DoesNotContain("\u001B", writer.ToString());
        Assert.DoesNotContain("\u0007", writer.ToString());
        Assert.DoesNotContain("\u009B", writer.ToString());
    }

    private static StringWriter CreateWriter() => new()
    {
        NewLine = "\n"
    };
}
