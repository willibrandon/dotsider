// FROZEN TEST FIXTURE — do not modify without updating Dotsider.Tests.
// This namespace exists only in V2: it is the deterministic "added namespace" node the
// size-regression diff and namespace-budget tests assert on.
using System.Runtime.CompilerServices;

namespace NativeAotConsole.Telemetry;

/// <summary>
/// A small metrics sink that exists only in the V2 build. Every member is
/// <see cref="MethodImplOptions.NoInlining"/> and invoked from the top-level code, so ILC
/// keeps each as a distinct method row in the mstat.
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
