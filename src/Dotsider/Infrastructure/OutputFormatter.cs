using System.Text.Json;

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
    public OutputFormatter() : this((string?)null) { }

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

    /// <summary>
    /// Creates a formatter that writes to the supplied writer.
    /// </summary>
    /// <param name="writer">The destination writer.</param>
    internal OutputFormatter(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        _writer = writer;
        _ownsWriter = false;
    }

    /// <summary>Serializes the value as JSON and writes it as a line.</summary>
    public void WriteJson<T>(T value)
    {
        if (value is null)
        {
            _writer.WriteLine("null");
            return;
        }

        var jsonTypeInfo = DotsiderAppJsonContext.Application.GetTypeInfo(value.GetType())
            ?? DotsiderAppJsonContext.Application.GetTypeInfo(typeof(T))
            ?? throw new InvalidOperationException(
                $"No source-generated JSON metadata is registered for {value.GetType()}.");
        var json = JsonSerializer.Serialize(value, jsonTypeInfo);
        _writer.WriteLine(json);
    }

    /// <summary>Writes a line of text (suppressed in JSON mode).</summary>
    public void WriteLine(string message)
    {
        if (!JsonMode)
            _writer.WriteLine(TerminalText.Escape(message));
    }

    /// <summary>
    /// Writes a multiline text block while preserving logical line boundaries and escaping
    /// terminal controls within each line.
    /// </summary>
    /// <param name="message">The multiline text.</param>
    public void WriteBlock(string message)
    {
        if (JsonMode)
        {
            return;
        }

        var escaped = TerminalText.EscapeMultiline(message);
        var lineStart = 0;
        while (lineStart < escaped.Length)
        {
            var lineEnd = escaped.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                _writer.WriteLine(escaped.AsSpan(lineStart));
                return;
            }

            _writer.WriteLine(escaped.AsSpan(lineStart, lineEnd - lineStart));
            lineStart = lineEnd + 1;
        }

        if (escaped.Length == 0)
        {
            _writer.WriteLine();
        }
    }

    /// <summary>Writes an error message to stderr (always, regardless of mode).</summary>
    public static void WriteError(string message)
    {
        WriteError(Console.Error, message);
    }

    /// <summary>
    /// Writes a terminal-safe error message to the supplied writer.
    /// </summary>
    /// <param name="writer">The error destination.</param>
    /// <param name="message">The error message.</param>
    internal static void WriteError(TextWriter writer, string message)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine(TerminalText.Escape(message));
    }

    /// <summary>Writes a column-aligned table with headers (suppressed in JSON mode).</summary>
    public void WriteTable(string[] headers, IEnumerable<string[]> rows)
    {
        if (JsonMode) return;

        var escapedHeaders = headers.Select(TerminalText.Escape).ToArray();
        var allRows = rows
            .Select(row => row.Select(TerminalText.Escape).ToArray())
            .ToList();
        var widths = new int[escapedHeaders.Length];

        for (var i = 0; i < escapedHeaders.Length; i++)
            widths[i] = escapedHeaders[i].Length;

        foreach (var row in allRows)
        {
            for (var i = 0; i < row.Length && i < widths.Length; i++)
                widths[i] = Math.Max(widths[i], row[i].Length);
        }

        var last = escapedHeaders.Length - 1;
        _writer.WriteLine(string.Join("  ", escapedHeaders.Select((h, i) => i < last ? h.PadRight(widths[i]) : h)));
        _writer.WriteLine(string.Join("  ", widths.Select((w, i) => new string('-', i < last ? w : escapedHeaders[last].Length))));

        foreach (var row in allRows)
        {
            _writer.WriteLine(string.Join("  ", row.Select((c, i) =>
                i < last && i < widths.Length ? c.PadRight(widths[i]) : c)));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsWriter)
            _writer.Dispose();
    }
}
