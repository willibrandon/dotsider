internal static class TestAssert
{
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
