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
#pragma warning disable CA2255 // ModuleInitializer is intentional — must run before any test
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        ThreadPool.SetMinThreads(64, 64);
    }
}
