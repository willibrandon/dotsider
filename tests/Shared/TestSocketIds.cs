/// <summary>
/// Allocates unique pseudo-process identifiers for diagnostics socket tests.
/// Each test process owns a disjoint range so concurrently running test projects
/// cannot delete or replace one another's Unix domain sockets.
/// </summary>
internal static class TestSocketIds
{
    private const int SlotsPerProcess = 256;
    private const int TestSocketIdBase = 536_870_912;

    private static int s_nextSlot;

    /// <summary>
    /// Returns the next socket identifier from the current test process's range.
    /// </summary>
    /// <returns>A unique pseudo-process id.</returns>
    public static int NextPid()
    {
        var slot = Interlocked.Increment(ref s_nextSlot);
        if (slot >= SlotsPerProcess)
            throw new InvalidOperationException("The test process exhausted its diagnostics socket identifiers.");

        return checked(TestSocketIdBase + (Environment.ProcessId * SlotsPerProcess) + slot);
    }
}
