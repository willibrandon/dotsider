namespace Dotsider.Website.Tests;

/// <summary>
/// MSTest assembly fixture that builds shared sample assemblies once for this test assembly.
/// </summary>
[TestClass]
public static class SampleAssemblyHost
{
    internal static SampleAssemblyFixture Instance { get; private set; } = null!;

    /// <summary>
    /// Initializes shared sample assemblies before the test assembly runs.
    /// </summary>
    /// <param name="context">The MSTest assembly initialization context.</param>
    [AssemblyInitialize]
    public static async Task AssemblyInitialize(TestContext context)
    {
        Instance = new SampleAssemblyFixture();
        await Instance.InitializeAsync();
    }

    /// <summary>
    /// Cleans up shared sample assemblies after the test assembly completes.
    /// </summary>
    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        if (Instance is not null)
            await Instance.DisposeAsync();
    }
}
