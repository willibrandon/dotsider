using Dotsider.Core.Analysis.Models;
using System.Text.Json;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads a size-budget document: <c>{ "budgets": [ ... ] }</c> where each entry is either a
/// spec string in the <see cref="SizeBudgetParser"/> grammar or an object
/// <c>{ "name", "description", "scope", "max", "growth", "severity", "topN" }</c> — the object
/// form is how a team names its budgets, downgrades one to a warning, or pins a per-budget
/// contributor count. Both forms mix freely in one document. The CLI's <c>--budget-file</c>
/// and the MCP server's inline budget JSON share this one parser.
/// </summary>
public static class SizeBudgetFile
{
    /// <summary>
    /// Loads a budget document from a file.
    /// </summary>
    /// <param name="path">The path of the JSON document.</param>
    /// <returns>The parsed budgets, in document order.</returns>
    /// <exception cref="FormatException">The document is not valid JSON or an entry is malformed.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    public static IReadOnlyList<SizeBudget> Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>
    /// Parses a budget document from its JSON text.
    /// </summary>
    /// <param name="json">The document text.</param>
    /// <returns>The parsed budgets, in document order.</returns>
    /// <exception cref="FormatException">The document is not valid JSON or an entry is malformed.</exception>
    public static IReadOnlyList<SizeBudget> Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Budget document is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("budgets", out var budgets)
                || budgets.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("Budget document must be an object with a 'budgets' array.");
            }

            var result = new List<SizeBudget>();
            var position = 0;
            foreach (var entry in budgets.EnumerateArray())
            {
                result.Add(entry.ValueKind switch
                {
                    JsonValueKind.String => SizeBudgetParser.Parse(entry.GetString()!),
                    JsonValueKind.Object => ParseObject(entry, position),
                    _ => throw new FormatException($"budgets[{position}]: expected a spec string or a budget object."),
                });
                position++;
            }

            return result;
        }
    }

    private static SizeBudget ParseObject(JsonElement entry, int position)
    {
        string? GetString(string name)
        {
            if (!entry.TryGetProperty(name, out var value)) return null;
            if (value.ValueKind != JsonValueKind.String)
                throw new FormatException($"budgets[{position}].{name}: expected a string.");
            return value.GetString();
        }

        foreach (var property in entry.EnumerateObject())
        {
            if (property.Name is not ("name" or "description" or "scope" or "max" or "growth" or "severity" or "topN"))
                throw new FormatException($"budgets[{position}]: unknown property '{property.Name}'.");
        }

        var max = GetString("max");
        var growth = GetString("growth");
        if (max is null && growth is null)
            throw new FormatException($"budgets[{position}]: a budget object needs 'max' and/or 'growth'.");

        // Reassemble the entry into spec-grammar form so both input shapes share one parser.
        var limits = new List<string>(2);
        if (max is not null) limits.Add($"max={max}");
        if (growth is not null) limits.Add($"growth={growth}");
        var scope = GetString("scope") ?? "total";
        SizeBudget parsed;
        try
        {
            parsed = SizeBudgetParser.Parse($"{scope}:{string.Join(",", limits)}");
        }
        catch (FormatException ex)
        {
            throw new FormatException($"budgets[{position}]: {ex.Message}");
        }

        var severity = GetString("severity");
        var parsedSeverity = severity?.ToLowerInvariant() switch
        {
            null or "error" => SizeBudgetSeverity.Error,
            "warning" => SizeBudgetSeverity.Warning,
            _ => throw new FormatException($"budgets[{position}].severity: '{severity}' is not 'error' or 'warning'."),
        };

        int? topN = null;
        if (entry.TryGetProperty("topN", out var topNValue))
        {
            if (topNValue.ValueKind != JsonValueKind.Number || !topNValue.TryGetInt32(out var n) || n < 0)
                throw new FormatException($"budgets[{position}].topN: expected a non-negative integer.");
            topN = n;
        }

        return parsed with
        {
            Severity = parsedSeverity,
            Name = GetString("name"),
            Description = GetString("description"),
            TopN = topN,
        };
    }
}
