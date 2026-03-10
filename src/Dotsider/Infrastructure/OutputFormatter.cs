using System.Text.Json;
using Dotsider.Core.Protocol;

namespace Dotsider.Infrastructure;

/// <summary>
/// Dual-mode output formatter supporting JSON and human-readable text,
/// with optional file output via -o/--output.
/// </summary>
internal sealed class OutputFormatter : IDisposable
{
    private readonly TextWriter _writer;
    private readonly bool _ownsWriter;

    /// <summary>Whether to emit JSON output instead of human-readable text.</summary>
    public bool JsonMode { get; set; }

    /// <summary>Creates a formatter that writes to stdout.</summary>
    public OutputFormatter() : this(null) { }

    /// <summary>
    /// Creates a formatter that writes to the specified file, or stdout if null.
    /// </summary>
    public OutputFormatter(string? outputPath)
    {
        if (outputPath is not null)
        {
            _writer = new StreamWriter(outputPath, append: false);
            _ownsWriter = true;
        }
        else
        {
            _writer = Console.Out;
            _ownsWriter = false;
        }
    }

    /// <summary>Serializes the value as JSON and writes it as a line.</summary>
    public void WriteJson<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DotsiderJsonOptions.Default);
        _writer.WriteLine(json);
    }

    /// <summary>Writes a line of text (suppressed in JSON mode).</summary>
    public void WriteLine(string message)
    {
        if (!JsonMode)
            _writer.WriteLine(message);
    }

    /// <summary>Writes an error message to stderr (always, regardless of mode).</summary>
    public static void WriteError(string message)
    {
        Console.Error.WriteLine(message);
    }

    /// <summary>Writes a column-aligned table with headers (suppressed in JSON mode).</summary>
    public void WriteTable(string[] headers, IEnumerable<string[]> rows)
    {
        if (JsonMode) return;

        var allRows = rows.ToList();
        var widths = new int[headers.Length];

        for (var i = 0; i < headers.Length; i++)
            widths[i] = headers[i].Length;

        foreach (var row in allRows)
        {
            for (var i = 0; i < row.Length && i < widths.Length; i++)
                widths[i] = Math.Max(widths[i], row[i].Length);
        }

        _writer.WriteLine(string.Join("  ", headers.Select((h, i) => h.PadRight(widths[i]))));
        _writer.WriteLine(string.Join("  ", widths.Select(w => new string('-', w))));

        foreach (var row in allRows)
        {
            _writer.WriteLine(string.Join("  ", row.Select((c, i) =>
                i < widths.Length ? c.PadRight(widths[i]) : c)));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsWriter)
            _writer.Dispose();
    }
}
