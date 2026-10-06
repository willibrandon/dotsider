/// <summary>
/// Provides shared assertion helpers used by MSTest projects.
/// The helpers preserve useful xUnit-style diagnostics where MSTest has no direct equivalent.
/// </summary>
internal static class TestAssert
{
    /// <summary>Returns a nullable value or fails the test with a useful assertion error.</summary>
    /// <typeparam name="T">The value type being asserted.</typeparam>
    /// <param name="value">The value that must be present.</param>
    /// <returns>The unwrapped value.</returns>
    public static T HasValue<T>(T? value) where T : struct =>
        value ?? throw new AssertFailedException("Expected a non-null value.");

    /// <summary>Returns a reference or fails the test before it can be dereferenced.</summary>
    /// <typeparam name="T">The reference type being asserted.</typeparam>
    /// <param name="value">The reference that must be present.</param>
    /// <returns>The non-null reference.</returns>
    public static T NotNull<T>(T? value) where T : class =>
        value ?? throw new AssertFailedException("Expected a non-null reference.");

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
