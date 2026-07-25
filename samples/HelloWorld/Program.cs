// A simple .NET console application for testing dotsider
Console.WriteLine("Hello from dotsider test assembly!");
for (var argumentIndex = 0; argumentIndex < args.Length; argumentIndex++)
{
    Console.WriteLine($"ARG[{argumentIndex}]={args[argumentIndex]}");
}

// Allocate some objects to trigger GC events
var list = new List<byte[]>();
for (int i = 0; i < 50; i++)
{
    list.Add(new byte[100_000]); // 100KB allocations
    Console.WriteLine($"  Iteration {i}: {DateTime.Now}");
    if (i % 10 == 0)
    {
        list.Clear(); // let GC reclaim
        GC.Collect();
        await Task.Delay(500); // give EventPipe time to deliver events + counters
    }
}

// Trigger an exception (caught)
try { throw new InvalidOperationException("Test exception for tracing"); }
catch { /* intentional */ }

// Exercise overloaded methods so they JIT-compile (used by disambiguation tests)
Console.WriteLine(Formatter.Format(42));
Console.WriteLine(Formatter.Format("dotsider"));

Console.WriteLine("Done!");

/// <summary>
/// Provides overloaded Format methods to exercise JIT overload disambiguation.
/// </summary>
static class Formatter
{
    public static string Format(int value) => $"int:{value}";
    public static string Format(string value) => $"str:{value}";
}
