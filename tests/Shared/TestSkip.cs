/// <summary>
/// Provides MSTest skip helpers for runtime-dependent test assumptions.
/// MSTest represents these skips as inconclusive outcomes.
/// </summary>
internal static class TestSkip
{
    /// <summary>
    /// Marks the test inconclusive when the condition is true.
    /// Use this for optional runtime or platform capabilities that are absent.
    /// </summary>
    /// <param name="condition">Whether the test should be skipped.</param>
    /// <param name="message">The skip reason.</param>
    public static void When(bool condition, string message)
    {
        if (condition)
            Assert.Inconclusive(message);
    }

    /// <summary>
    /// Marks the test inconclusive unless the condition is true.
    /// Use this for required runtime or platform capabilities that must be present.
    /// </summary>
    /// <param name="condition">Whether the test can continue.</param>
    /// <param name="message">The skip reason.</param>
    public static void Unless(bool condition, string message)
    {
        if (!condition)
            Assert.Inconclusive(message);
    }
}
