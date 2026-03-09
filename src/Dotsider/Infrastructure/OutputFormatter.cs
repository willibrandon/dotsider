using System.Text.Json;
using Dotsider.Core.Protocol;

namespace Dotsider.Infrastructure;

/// <summary>
/// Dual-mode output formatter supporting JSON and human-readable text.
/// </summary>
internal sealed class OutputFormatter
{
    public bool JsonMode { get; set; }

    public void WriteJson<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, DotsiderJsonOptions.Default);
        Console.WriteLine(json);
    }

    public void WriteLine(string message)
    {
        if (!JsonMode)
            Console.WriteLine(message);
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

        Console.WriteLine(string.Join("  ", headers.Select((h, i) => h.PadRight(widths[i]))));
        Console.WriteLine(string.Join("  ", widths.Select(w => new string('-', w))));

        foreach (var row in allRows)
        {
            Console.WriteLine(string.Join("  ", row.Select((c, i) =>
                i < widths.Length ? c.PadRight(widths[i]) : c)));
        }
    }
}
