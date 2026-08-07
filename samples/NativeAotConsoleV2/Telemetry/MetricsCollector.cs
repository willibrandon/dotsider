// FROZEN TEST FIXTURE — do not modify without updating Dotsider.Tests.
// This namespace exists only in V2: it is the deterministic "added namespace" node the
// size-regression diff and namespace-budget tests assert on.
using System.Runtime.CompilerServices;

namespace NativeAotConsole.Telemetry;

/// <summary>
/// A metrics sink retained as distinct method rows in the V2 Native AOT size fixture.
/// Produces deterministic methods and strings for size-regression comparisons.
/// Remains stable so fixture-based budget and namespace tests stay meaningful.
/// </summary>
internal sealed class MetricsCollector
{
    private readonly Dictionary<string, long> _counts = [];

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Record(string metric, long value)
    {
        ArgumentException.ThrowIfNullOrEmpty(metric);
        _counts[metric] = _counts.GetValueOrDefault(metric) + value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string Summarize()
    {
        var parts = _counts
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value}");
        return $"metrics: {string.Join(", ", parts)}";
    }
}
