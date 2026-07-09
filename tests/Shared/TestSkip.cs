internal static class TestSkip
{
    public static void When(bool condition, string message)
    {
        if (condition)
            Assert.Inconclusive(message);
    }

    public static void Unless(bool condition, string message)
    {
        if (!condition)
            Assert.Inconclusive(message);
    }
}

