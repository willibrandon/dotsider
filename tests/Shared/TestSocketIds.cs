internal static class TestSocketIds
{
    private static int s_nextPid = 700_000;

    public static int NextPid() => Interlocked.Increment(ref s_nextPid);
}
