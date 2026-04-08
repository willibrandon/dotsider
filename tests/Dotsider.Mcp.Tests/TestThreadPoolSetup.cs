using System.Runtime.CompilerServices;

namespace Dotsider.Mcp.Tests;

/// <summary>
/// Ensures the .NET thread pool has enough threads for test execution.
/// MCP session tests start real Hex1b terminals which spawn pump threads.
/// Without sufficient min threads, tests cause thread pool starvation on
/// CI runners with few cores.
/// </summary>
internal static class TestThreadPoolSetup
{
#pragma warning disable CA2255 // ModuleInitializer is intentional — must run before any test
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        ThreadPool.SetMinThreads(32, 32);
    }
}
