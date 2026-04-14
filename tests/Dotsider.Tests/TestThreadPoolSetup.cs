using System.Runtime.CompilerServices;

namespace Dotsider.Tests;

/// <summary>
/// Ensures the .NET thread pool has enough threads for test execution.
/// Each Hex1bTerminal spawns 2 Task.Run pump threads (input + output). Without
/// sufficient min threads, tests cause thread pool starvation on CI runners
/// with few cores (e.g., 2-core GitHub Actions runners), leading to render
/// loop stalls and WaitUntil timeouts.
/// </summary>
internal static class TestThreadPoolSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ThreadPool.SetMinThreads(32, 32);
    }
}
