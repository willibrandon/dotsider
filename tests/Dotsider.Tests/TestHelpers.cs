using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Dotsider.Tests;

internal static class TestHelpers
{
    /// <summary>
    /// Polls <paramref name="condition"/> at <paramref name="interval"/> until true or <paramref name="timeout"/>.
    /// Calls Assert.Fail with the condition expression on timeout.
    /// </summary>
    internal static async Task WaitUntilAsync(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? interval = null,
        [CallerArgumentExpression(nameof(condition))] string? expr = null)
    {
        var poll = interval ?? TimeSpan.FromMilliseconds(250);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (condition()) return;
            await Task.Delay(poll, TestContext.Current.CancellationToken);
        }

        if (!condition())
            Assert.Fail($"Timed out after {timeout.TotalSeconds:F1}s waiting for: {expr}");
    }

    /// <summary>
    /// Resolves the repo root directory by walking up from the test assembly location.
    /// </summary>
    internal static string GetRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Dotsider.slnx")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("Could not find repo root (Dotsider.slnx)");
    }
}
