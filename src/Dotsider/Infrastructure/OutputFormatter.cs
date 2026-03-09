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

    public bool JsonMode { get; set; }

    public OutputFormatter() : this(null) { }

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

    public void WriteJson<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DotsiderJsonOptions.Default);
        _writer.WriteLine(json);
    }

    public void WriteLine(string message)
    {
        if (!JsonMode)
            _writer.WriteLine(message);
    }

    public void WriteError(string message)
    {
        Console.Error.WriteLine(message);
    }

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

    public void Dispose()
    {
        if (_ownsWriter)
            _writer.Dispose();
    }
}
