/// <summary>
/// Provides shared assertion helpers used by MSTest projects.
/// The helpers preserve useful xUnit-style diagnostics where MSTest has no direct equivalent.
/// </summary>
internal static class TestAssert
{
    /// <summary>
    /// Applies an assertion to every value and prefixes failures with the item index.
    /// This mirrors the useful diagnostic shape of xUnit's collection assertions.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="values">The values to inspect.</param>
    /// <param name="assertion">The assertion to run for each value.</param>
    public static void All<T>(IEnumerable<T> values, Action<T> assertion)
    {
        var index = 0;
        foreach (var value in values)
        {
            string? failureMessage = null;
            Exception? failure = null;
            try
            {
                assertion(value);
            }
            catch (Exception ex) when (ex is AssertFailedException or AssertInconclusiveException)
            {
                failureMessage = $"Item {index}: {ex.Message}";
                failure = ex;
            }

            if (failureMessage is not null)
                throw new AssertFailedException(failureMessage, failure!);

            index++;
        }
    }
}
