#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0

using System.Text.Json;

if (args is not [var directory] || directory.StartsWith('-'))
{
    Console.Error.WriteLine("usage: Verify-CodeQl.cs <sarif-directory>");
    return 2;
}

// Check the complete uploaded results, including quality findings that GitHub does not block by default.
var files = Directory.Exists(directory)
    ? Directory.EnumerateFiles(directory, "*.sarif", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray()
    : [];
if (files.Length == 0)
{
    Console.Error.WriteLine($"CodeQL left no SARIF file under {Path.GetFullPath(directory)}.");
    return 1;
}

var findings = new List<string>();
foreach (var file in files)
{
    using var document = JsonDocument.Parse(File.ReadAllBytes(file));
    if (!document.RootElement.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array
        || runs.GetArrayLength() == 0)
    {
        Console.Error.WriteLine($"{file} has no runs, so it is not a CodeQL result.");
        return 1;
    }

    foreach (var run in runs.EnumerateArray())
    {
        if (run.TryGetProperty("invocations", out var invocations)
            && invocations.EnumerateArray().Any(invocation =>
                invocation.TryGetProperty("executionSuccessful", out var success) && success.ValueKind == JsonValueKind.False))
        {
            Console.Error.WriteLine($"{file} contains an unsuccessful analysis.");
            return 1;
        }

        if (!run.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            Console.Error.WriteLine($"{file} contains a run without results.");
            return 1;
        }

        foreach (var result in results.EnumerateArray())
        {
            var message = result.TryGetProperty("message", out var body) ? Text(body, "text", "") : "";
            findings.Add($"{Location(result)}: {Text(result, "level", "warning")} {Text(result, "ruleId", "unknown rule")}: {message}");
        }
    }
}

foreach (var finding in findings.Order(StringComparer.Ordinal))
{
    Console.Error.WriteLine(finding);
}

Console.WriteLine($"CodeQL reported {findings.Count} finding(s) in {files.Length} result file(s).");
return findings.Count == 0 ? 0 : 1;

static string Text(JsonElement element, string name, string fallback) =>
    element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

static string Location(JsonElement result)
{
    if (!result.TryGetProperty("locations", out var locations) || locations.ValueKind != JsonValueKind.Array
        || locations.GetArrayLength() == 0 || !locations[0].TryGetProperty("physicalLocation", out var place))
    {
        return "unknown location";
    }

    var path = place.TryGetProperty("artifactLocation", out var artifact) ? Text(artifact, "uri", "unknown location") : "unknown location";
    return place.TryGetProperty("region", out var region) && region.TryGetProperty("startLine", out var line)
        && line.TryGetInt32(out var number) ? $"{path}:{number}" : path;
}
