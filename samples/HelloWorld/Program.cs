// A simple .NET console application for testing dotsider
Console.WriteLine("Hello from dotsider test assembly!");

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

Console.WriteLine("Done!");
