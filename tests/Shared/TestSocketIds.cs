/// <summary>
/// Allocates unique pseudo-process identifiers for diagnostics socket tests.
/// The values avoid collisions between MethodLevel-parallel test cases.
/// </summary>
internal static class TestSocketIds
{
    private static int s_nextPid = 700_000;

    /// <summary>
    /// Returns the next unique socket identifier for the current test process.
    /// The value is process-local and intentionally does not need to match a real OS process id.
    /// </summary>
    /// <returns>A unique pseudo-process id.</returns>
    public static int NextPid() => Interlocked.Increment(ref s_nextPid);
}
